using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Nickelony.Stanza.Format.Planning;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Format.Running;

/// <summary>Reads the sources each project of a solution compiles, with one MSBuild evaluation per project.</summary>
internal static class CompileItems
{
	public static ImmutableArray<CompiledFile> Collect(string root, string solution, CancellationToken cancellationToken)
	{
		ProcessResult listing = ProcessRunner.Run(
			fileName: "dotnet",
			arguments: ["sln", ArgumentPath.ToProtected(solution), "list"],
			workingDirectory: root,
			echo: null,
			cancellationToken
		);

		if (listing.ExitCode != 0)
			throw new FormatRunException($"dotnet sln {solution} list failed with exit code {listing.ExitCode}: {listing.Error.Trim()}");

		string solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solution, root)) ?? root;

		// A project the solution names but the disk lacks is skipped, as `dotnet format` skips it.
		ImmutableArray<string> projects =
		[
			.. from project in ReadProjects(listing.Output)
			let fullPath = Path.GetFullPath(project, solutionDirectory)
			where File.Exists(fullPath)
			select fullPath,
		];

		if (projects.IsEmpty)
			return [];

		// The results are in project order, so the first project to compile a file is the first in the solution.
		ImmutableArray<ImmutableArray<CompiledFile>> files = ProcessRunner.RunEach(
			projects,
			project => ReadProjectFiles(root, project, cancellationToken),
			cancellationToken
		);

		return [.. files.SelectMany(static projectFiles => projectFiles)];
	}

	/// <summary>
	/// Reads only the C# and Visual Basic projects, whatever the case of their extension, as <c>dotnet format</c> does.
	/// </summary>
	public static ImmutableArray<string> ReadProjects(string listing)
	{
		// `dotnet sln list` writes a heading, a line of dashes and a project on each line after it; only the heading is translated.
		string[] lines = listing.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		int dashes = Array.FindIndex(lines, static line => line.All(static character => character == '-'));

		return dashes < 0
			? []
			: [.. lines.Skip(dashes + 1).Where(static project => Path.GetExtension(project).ToUpperInvariant() is ".CSPROJ" or ".VBPROJ")];
	}

	/// <summary>The view holds the framework, which sets its symbols, and the symbols the project defines.</summary>
	/// <remarks>
	/// A project that targets several frameworks has one view, because <c>dotnet format</c> merges those views itself.
	/// </remarks>
	public static ImmutableArray<CompiledFile> ReadItems(string output, string root, string project, string? view = null)
	{
		using JsonDocument document = ParseResult(output);

		JsonElement result = document.RootElement;

		string relativeProject = RepositoryPath.ToRelative(root, project);

		string shownView = view ?? ReadView(result);

		if (!result.TryGetProperty("Items", out JsonElement items) || !items.TryGetProperty("Compile", out JsonElement compile))
			return [];

		List<CompiledFile> files = [];

		foreach (JsonElement item in compile.EnumerateArray())
		{
			string path = RepositoryPath.ToRelative(root, ReadMetadata(item, "FullPath"));

			// A file outside the repository is not this repository's to format.
			if (!RelativePathScope.LeavesDirectory(path))
				files.Add(new CompiledFile(path, relativeProject, shownView));
		}

		return [.. files];
	}

	/// <summary>A project with only <c>TargetFrameworks</c> lists its sources once a framework is set.</summary>
	public static ImmutableArray<string> ReadFrameworks(string output)
	{
		using JsonDocument document = ParseResult(output);

		string framework = ReadProperty(document.RootElement, "TargetFramework");
		string frameworks = ReadProperty(document.RootElement, "TargetFrameworks");

		return framework.Length > 0 ? [] : [.. frameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
	}

	/// <summary>
	/// A failed evaluation writes its errors outside the object, so the error is the lines that do not belong to it.
	/// </summary>
	public static string ReadErrors(string output)
	{
		string[] lines =
		[
			.. from line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			where line[0] is not ('{' or '}' or '"' or ']' or '[')
			select line,
		];

		return lines.Length == 0 ? "MSBuild wrote no error line." : string.Join(Environment.NewLine, lines);
	}

	private static ImmutableArray<CompiledFile> ReadProjectFiles(string root, string project, CancellationToken cancellationToken)
	{
		string output = RunEvaluation(root, project, framework: null, cancellationToken);
		ImmutableArray<string> frameworks = ReadFrameworks(output);

		if (frameworks.IsEmpty)
			return ReadItems(output, root, project);

		// The outer evaluation lists no default source, so each framework lists its own, and a file two of them compile counts once.
		string view = ReadView(output);

		return
		[
			.. frameworks
				.SelectMany(framework => ReadItems(output: RunEvaluation(root, project, framework, cancellationToken), root, project, view))
				.DistinctBy(static file => file.Path, PathComparison.Comparer),
		];
	}

	private static string RunEvaluation(string root, string project, string? framework, CancellationToken cancellationToken)
	{
		// A response file could add a switch that changes the result, such as a target or a verbosity.
		string[] arguments =
		[
			"msbuild",
			project,
			"-getItem:Compile",
			"-getProperty:TargetFramework,TargetFrameworks,DefineConstants",
			"-nologo",
			"-noAutoResponse",
			.. framework is null ? (string[])[] : [$"-p:TargetFramework={framework}"],
		];

		ProcessResult result = ProcessRunner.Run(fileName: "dotnet", arguments, workingDirectory: root, echo: null, cancellationToken);

		if (result.ExitCode != 0)
		{
			throw new FormatRunException(
				$"MSBuild could not list the files {RepositoryPath.ToRelative(root, project)} compiles (exit code {result.ExitCode}): "
					+ ReadErrors(result.Output + "\n" + result.Error)
			);
		}

		return result.Output;
	}

	private static JsonDocument ParseResult(string output)
	{
		// A warning can come before the result, so the result starts at the first line that opens an object.
		int start = output.StartsWith('{') ? 0 : output.IndexOf("\n{", StringComparison.Ordinal);

		if (start < 0)
			throw new FormatRunException($"MSBuild wrote no result: {output.Trim()}");

		return JsonDocument.Parse(output[start..]);
	}

	private static string ReadView(JsonElement result) =>
		BuildView(ReadProperty(result, "TargetFramework") + ReadProperty(result, "TargetFrameworks"), ReadProperty(result, "DefineConstants"));

	private static string ReadView(string output)
	{
		using JsonDocument document = ParseResult(output);
		return ReadView(document.RootElement);
	}

	private static string ReadProperty(JsonElement result, string name) =>
		result.TryGetProperty("Properties", out JsonElement properties) ? ReadMetadata(properties, name) : string.Empty;

	private static string ReadMetadata(JsonElement item, string name) =>
		item.TryGetProperty(name, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;

	private static string BuildView(string framework, string constants)
	{
		IEnumerable<string> symbols = constants
			.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Distinct(StringComparer.Ordinal)
			.Order(StringComparer.Ordinal);

		return $"{framework}|{string.Join(';', symbols)}";
	}
}
