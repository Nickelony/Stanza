using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Format.Cli;
using Nickelony.Stanza.Format.Planning;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Format.Tests;

[TestClass]
[TestCategory("Repository")]
public sealed class ToolDocumentationTests
{
	[TestMethod]
	public void TheUsageBlockOfTheCommandReferenceIsTheTextTheToolPrints()
	{
		string usage = FormatCommandLine.Usage.ReplaceLineEndings("\n");

		Assert.Contains(
			usage,
			ReadCommandReference(),

			"The usage block of the command reference does not match the text the tool prints. "
				+ "Copy the output of 'stanza-format --help' into it again."
		);
	}

	[TestMethod]
	public void TheCommandReferenceNamesEachStepARunCanTake()
	{
		FormatFacts facts = new(
			Solution: "App.slnx",
			HasMarkup: true,
			ChangedFiles: [],
			SkippedSources: [],
			CompiledFiles: [new CompiledFile("A.cs", "A.csproj", "net8.0|")]
		);

		FormatOptions all = new(All: true, Check: false, Path: ".", Layout: null);
		FormatOptions whitespace = all with { Layout = FormatLayout.Whitespace };
		FormatOptions check = all with { All = false, Check = true };

		ImmutableArray<FormatStep>[] plans =
		[
			Plan(all, facts),
			Plan(whitespace, facts),
			Plan(check, facts),
			Plan(whitespace with { All = false, Check = true }, facts),
		];

		string[] titles =
		[
			.. plans.SelectMany(static plan => plan).Select(static step => step.Title).Distinct(),
		];

		Assert.IsNotEmpty(
			titles,
			"The plans hold no step, so the check below would pass vacuously."
		);

		string reference = ReadCommandReference();

		foreach (string title in titles)
			Assert.Contains($"**{title}**", reference, $"The page lacks the step '{title}'.");
	}

	private static ImmutableArray<FormatStep> Plan(FormatOptions options, FormatFacts facts)
	{
		FormatLayout layout = FormatPlanner.ResolveLayout(options, hasCSharpier: true);
		return FormatPlanner.Plan(options, facts, layout);
	}

	private static string ReadCommandReference()
	{
		return File.ReadAllText(RepositoryPaths.Below("docs", "Format-command-reference.md"))
			.ReplaceLineEndings("\n");
	}
}
