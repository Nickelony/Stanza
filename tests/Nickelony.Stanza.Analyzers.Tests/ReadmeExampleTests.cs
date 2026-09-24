using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
[TestCategory("Repository")]
public sealed class ReadmeExampleTests
{
	private const string ReadmePath = "src/Nickelony.Stanza.Analyzers/README.md";
	private const string Fence = "```csharp\n";
	private const string BeforeMarker = "// Before\n";
	private const string AfterMarker = "// After\n";

	[TestMethod]
	public async Task TheWhatItDoesExampleFixesToItsAfterBlock()
	{
		string readme = File.ReadAllText(Path.Combine(RepositoryPaths.Root(), ReadmePath));
		(string before, string after) = ReadExample(readme);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(Wrap(before));

		Assert.AreEqual(
			Wrap(after),
			result.FixedText,
			"The fixed example does not match the After block of the readme."
		);
	}

	[TestMethod]
	public void TheRepositoryReadmeShowsTheSameExample()
	{
		string root = RepositoryPaths.Root();

		Assert.AreEqual(
			ReadExample(File.ReadAllText(Path.Combine(root, ReadmePath))),
			ReadExample(File.ReadAllText(Path.Combine(root, "README.md")), "## Analyzers"),
			"The repository readme shows an example that the analyzer readme does not."
		);
	}

	private static (string Before, string After) ReadExample(
		string readme,
		string heading = "## What it does"
	)
	{
		int section = readme.IndexOf(heading, StringComparison.Ordinal);

		int open = readme.IndexOf(Fence, section, StringComparison.Ordinal) + Fence.Length;
		int close = readme.IndexOf("\n```", open, StringComparison.Ordinal);

		string block = readme[open..close];

		int after = block.IndexOf(AfterMarker, StringComparison.Ordinal);

		return (block[BeforeMarker.Length..after], block[(after + AfterMarker.Length)..]);
	}

	// The example names members the readme leaves out, so they are declared around it and the snippet compiles.
	private static string Wrap(string snippet)
	{
		string body = string.Join(
			"\n",
			snippet.Split('\n').Select(line => line.Length == 0 ? line : "\t\t" + line)
		);

		return string.Join(
			"\n",
			"class C",
			"{",
			"\tprivate static readonly Logger logger = new();",
			"",
			"\tprivate static Options BuildOptions() => new();",
			"",
			"\tprivate static Options M()",
			"\t{",
			body.TrimEnd('\n', '\t'),
			"\t}",
			"",
			"\tprivate sealed class Options",
			"\t{",
			"\t\tpublic int Count { get; }",
			"\t}",
			"",
			"\tprivate sealed class Logger",
			"\t{",
			"\t\tpublic void LogInformation(string message, params object[] args)",
			"\t\t{",
			"\t\t\t_ = message;",
			"\t\t\t_ = args;",
			"\t\t}",
			"\t}",
			"}",
			""
		);
	}
}
