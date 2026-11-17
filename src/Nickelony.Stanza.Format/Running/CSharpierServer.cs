using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Nickelony.Stanza.Format.Running;

/// <summary>A running <c>csharpier server</c>, which formats any number of sources in one host.</summary>
/// <remarks>
/// The server finds the configuration of a file from the path it is given, as <c>csharpier format</c> does.
/// </remarks>
internal sealed partial class CSharpierServer : IDisposable
{
	private static readonly TimeSpan s_startTimeout = TimeSpan.FromSeconds(60);

	private readonly Process _process;
	private readonly HttpClient _client;
	private readonly CancellationToken _cancellationToken;
	private readonly CancellationTokenRegistration _registration;

	private CSharpierServer(Process process, int port, TimeSpan requestTimeout, CancellationToken cancellationToken)
	{
		_process = process;

		// The server is on this machine, so no proxy of the environment may carry the sources it is sent.
		_client = new HttpClient(new SocketsHttpHandler { UseProxy = false })
		{
			BaseAddress = new Uri($"http://127.0.0.1:{port}/"),
			Timeout = requestTimeout,
		};

		_cancellationToken = cancellationToken;

		// A canceled run stops the server too, so no host outlives the tool.
		_registration = cancellationToken.Register(() => ProcessRunner.Stop(process));
	}

	[GeneratedRegex(@"^Started on (?<port>\d+)\s*$", RegexOptions.CultureInvariant)]
	private static partial Regex StartedLine();

	/// <summary>Starts the server and waits for its port, and ends the run if it exits or is silent first.</summary>
	public static CSharpierServer Start(
		ImmutableArray<string> command,
		string workingDirectory,
		TimeSpan requestTimeout,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		ProcessStartInfo start = new("dotnet")
		{
			WorkingDirectory = workingDirectory,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};

		foreach (string argument in command)
			start.ArgumentList.Add(argument);

		Process process = ProcessRunner.Start(start);

		try
		{
			process.StandardInput.Close();

			TaskCompletionSource<int> port = new(TaskCreationOptions.RunContinuationsAsynchronously);
			StringBuilder said = new();

			void ReadLine(string? line)
			{
				if (line is null)
					return;

				lock (said)
					_ = said.AppendLine(line);

				if (StartedLine().Match(line) is { Success: true } started)
					_ = port.TrySetResult(int.Parse(started.Groups["port"].Value, CultureInfo.InvariantCulture));
			}

			process.OutputDataReceived += (_, received) => ReadLine(received.Data);
			process.ErrorDataReceived += (_, received) => ReadLine(received.Data);

			process.EnableRaisingEvents = true;

			process.Exited += (_, _) =>
			{
				// The wait lets the streams end, so the lines the server printed before it exited are in the message.
				process.WaitForExit();

				string first;

				lock (said)
					first = said.ToString().Split('\n')[0].Trim();

				string message = $"The CSharpier server exited with code {process.ExitCode} before it started. {first}";
				_ = port.TrySetException(new FormatRunException(message.TrimEnd()));
			};

			process.BeginOutputReadLine();
			process.BeginErrorReadLine();

			if (Task.WaitAny([port.Task], (int)s_startTimeout.TotalMilliseconds, cancellationToken) < 0)
				throw new FormatRunException($"The CSharpier server printed no port within {s_startTimeout.TotalSeconds:0} seconds.");

			return new CSharpierServer(process, port: port.Task.GetAwaiter().GetResult(), requestTimeout, cancellationToken);
		}
		catch
		{
			ProcessRunner.Stop(process);
			process.Dispose();

			throw;
		}
	}

	/// <summary>Formats the contents as the file at the path, and ends the run when the server could not.</summary>
	public string Format(string fullPath, string contents)
	{
		using HttpRequestMessage request = new(HttpMethod.Post, "format")
		{
			Content = new StringContent(
				JsonSerializer.Serialize(new { fileContents = contents, fileName = fullPath }),
				Encoding.UTF8,
				"application/json"
			),
		};

		try
		{
			using HttpResponseMessage response = _client.Send(request, _cancellationToken);
			using var reply = JsonDocument.Parse(response.Content.ReadAsStream(_cancellationToken));

			JsonElement root = reply.RootElement;
			string? status = root.TryGetProperty("status", out JsonElement statusElement) ? statusElement.GetString() : null;

			if (status == "Formatted" && root.TryGetProperty("formattedFile", out JsonElement formatted) && formatted.GetString() is { } text)
				return text;

			string? reason = root.TryGetProperty("errorMessage", out JsonElement message) ? message.GetString() : null;
			throw new FormatRunException($"CSharpier could not format '{fullPath}' ({status ?? "no status"}): {reason}".TrimEnd(' ', ':'));
		}
		catch (OperationCanceledException exception) when (!_cancellationToken.IsCancellationRequested)
		{
			// The client's own time limit raises a cancellation, which the tool would otherwise report as Ctrl+C.
			string waited = _client.Timeout.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture);
			throw new FormatRunException($"The CSharpier server did not answer for '{fullPath}' within {waited} seconds.", exception);
		}
		catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
		{
			throw new FormatRunException($"The CSharpier server could not format '{fullPath}': {exception.Message}", exception);
		}
	}

	public void Dispose()
	{
		_registration.Dispose();
		_client.Dispose();

		ProcessRunner.Stop(_process);
		_process.Dispose();
	}
}
