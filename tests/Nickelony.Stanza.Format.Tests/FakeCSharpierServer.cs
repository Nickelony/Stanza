namespace Nickelony.Stanza.Format.Tests;

/// <summary>A program that a test runs in place of <c>csharpier server</c>.</summary>
internal static class FakeCSharpierServer
{
	/// <summary>Joins line breaks, turns "wrong" into "right", and fails on "BROKEN".</summary>
	/// <remarks>
	/// It appends a line to started.txt each time it starts.
	/// </remarks>
	public const string Program = """
		using System.Net;
		using System.Net.Sockets;
		using System.Text;
		using System.Text.Json;
		using System.Text.RegularExpressions;

		string lf = ((char)10).ToString();
		string headEnd = new string((char)13, 1) + lf + new string((char)13, 1) + lf;

		File.AppendAllText("started.txt", "started" + lf);

		TcpListener listener = new(IPAddress.Loopback, 0);
		listener.Start();
		Console.WriteLine($"Started on {((IPEndPoint)listener.LocalEndpoint).Port}");

		while (true)
		{
			using TcpClient client = listener.AcceptTcpClient();
			using Stream stream = client.GetStream();
			StringBuilder head = new();

			while (!head.ToString().EndsWith(headEnd))
				head.Append((char)stream.ReadByte());

			byte[] body = new byte[int.Parse(Regex.Match(head.ToString(), "(?i)content-length: ([0-9]+)").Groups[1].Value)];
			stream.ReadExactly(body);

			string contents = JsonDocument.Parse(body).RootElement.GetProperty("fileContents").GetString()!;
			bool broken = contents.Contains("BROKEN");

			using MemoryStream reply = new();

			using (Utf8JsonWriter writer = new(reply))
			{
				writer.WriteStartObject();

				if (broken)
					writer.WriteNull("formattedFile");
				else
					writer.WriteString("formattedFile", contents.Replace(lf + lf, lf).Replace("wrong", "right"));

				writer.WriteString("status", broken ? "Failed" : "Formatted");

				if (broken)
					writer.WriteString("errorMessage", "Syntax error.");
				else
					writer.WriteNull("errorMessage");

				writer.WriteEndObject();
			}

			stream.Write(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK{headEnd[..2]}Content-Length: {reply.Length}{headEnd[..2]}Connection: close{headEnd}"));
			stream.Write(reply.ToArray());
		}
		""";
}
