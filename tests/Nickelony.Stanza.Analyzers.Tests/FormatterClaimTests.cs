using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>
/// Runs the pinned CSharpier on the examples that the formatter page and the rule pages describe.
/// </summary>
[TestClass]
[TestCategory("Repository")]
public sealed class FormatterClaimTests
{
	private const string BeforeMarker = "// Before\n";
	private const string AfterMarker = "// After\n";

	// A row names what its example holds, and whether CSharpier erases the difference or keeps it.
	private const bool Statements = true;
	private const bool Members = false;
	private const bool Erases = true;
	private const bool Keeps = false;

	private const string PlainAccessors =
		"public int Value\n{\n\tget => _value;\n\tset => _value = value;\n}";

	private const string SpacedAccessors =
		"public int Value\n{\n\tget => _value;\n\n\tset => _value = value;\n}";

	private const string Summary =
		"/// <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary>";

	private static readonly string[] s_bodies =
	[
		"value with { A = 1 };",
		"from item in value select item;",
	];

	// The examples all go to CSharpier in one run, because each run starts a process that costs seconds.
	private static readonly Lazy<Dictionary<string, string>> s_formatted = new(FormatAll);

	public static IEnumerable<object[]> RuleExamples { get; } =
	[
		["the blank line between block accessors", "ZA0010", Members, Erases],
		["the blank line between collection elements", "ZA0022", Statements, Erases],
		["the blank line between arguments", "ZA0025", Statements, Erases],
		["the blank line between parameters", "ZA0028", Members, Erases],
		["the blank line between declarators", "ZA0031", Statements, Erases],
		["the blank line before a clause keyword", "ZA0034", Statements, Erases],
		["the brace padding", "ZA0036", Members, Erases],
		["the empty block", "ZA0037", Members, Erases],
		["the blank line between switch sections", "ZA0013", Statements, Keeps],
		["the blank line between switch expression arms", "ZA0016", Statements, Keeps],
		["the blank line around a region marker", "ZA0047", Members, Keeps],
	];

	[TestMethod]
	public void TheRunOrderNamesTheRulesTheBodyProviderFixes()
	{
		string page = File.ReadAllText(RepositoryPaths.Below("docs", "Working-with-formatters.md"));

		string[] lines =
		[
			.. page.ReplaceLineEndings("\n")
				.Split('\n')
				.Where(line =>
					line.StartsWith("dotnet format analyzers", StringComparison.Ordinal)
				),
		];

		string[] body =
		[
			.. RoslynTestHarness
				.AllCodeFixProviders.Single(provider =>
					provider.GetType().Name == "BodyCodeFixProvider"
				)
				.FixableDiagnosticIds.Order(StringComparer.Ordinal),
		];

		Assert.HasCount(3, lines, "The run order holds three analyzer commands.");

		foreach (string line in lines)
		{
			string[] named =
			[
				.. line.Split(' ')
					.Where(word => word.StartsWith("ZA", StringComparison.Ordinal))
					.Order(StringComparer.Ordinal),
			];

			Assert.AreSequenceEqual(
				body,
				named,
				$"The run order names other body-shape rules than the provider fixes: {line}"
			);
		}
	}

	[TestMethod]
	[DynamicData(
		nameof(RuleExamples),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void CSharpierErasesOrKeepsTheDifferenceOfARuleExampleAsThePageSays(
		string name,
		string id,
		bool statements,
		bool erases
	)
	{
		(string before, string after) = RuleExample(id);

		Func<string, string> wrap = statements ? AsStatements : AsMembers;
		string subject = $"{id} ({name})";

		if (erases)
			AssertCSharpierErasesTheDifference(wrap(before), wrap(after), subject);
		else
			AssertCSharpierKeepsTheDifference(wrap(before), wrap(after), subject);
	}

	[TestMethod]
	public void CSharpierErasesTheBlankLineBetweenExpressionBodiedAccessors()
	{
		AssertCSharpierErasesTheDifference(
			AsMembers(PlainAccessors),
			AsMembers(SpacedAccessors),
			"expression-bodied accessor"
		);
	}

	[TestMethod]
	public void CSharpierLeavesADocumentationCommentAsWritten()
	{
		string formatted = Format(AsMembers(Summary + "\ninternal static void Bind()\n{\n}"));

		Assert.Contains(
			Summary,
			formatted,
			"CSharpier rewrote a documentation comment, which the formatter page says it leaves alone."
		);
	}

	// ZA0041 and ZA0043 keep the block of such a body, which they would otherwise break after its arrow.
	[TestMethod]
	public void CSharpierSpreadsAWithOrQueryBodyOverLinesOnlyAfterABrokenArrow()
	{
		foreach (string body in s_bodies)
		{
			string broken = Format(AsMembers(BrokenArrowMember(body)));
			string block = Format(AsMembers(BlockMember(body)));

			Assert.DoesNotContain(body, broken, $"CSharpier kept '{body}' on one line.");
			Assert.Contains(body, block, $"CSharpier spread '{body}' in a block.");
		}
	}

	// The signature fills its line, so CSharpier breaks the member after the arrow.
	private static string BrokenArrowMember(string body) =>
		$"\tinternal R {new string('N', 110)}(R value) =>\n\t\t{body}";

	private static string BlockMember(string body) =>
		$"\tinternal R Run(R value)\n\t{{\n\t\treturn {body}\n\t}}";

	private static (string Before, string After) RuleExample(string id)
	{
		string page = Directory
			.GetFiles(RepositoryPaths.Below("docs", "rules"), $"{id}-*.md")
			.Single();

		string text = File.ReadAllText(page).ReplaceLineEndings("\n");

		int before = text.IndexOf(BeforeMarker, StringComparison.Ordinal);
		int after = text.IndexOf(AfterMarker, StringComparison.Ordinal);
		int close = text.IndexOf("\n```", after, StringComparison.Ordinal);

		Assert.IsTrue(
			before >= 0 && after > before && close > after,
			$"{id} has no Before and After example in its page."
		);

		return (
			text[(before + BeforeMarker.Length)..after],
			text[(after + AfterMarker.Length)..close]
		);
	}

	// A snippet of members is placed in a class, so that CSharpier parses it as a compilation unit.
	private static string AsMembers(string body) =>
		"internal sealed class Sample\n{\n" + body + "\n}\n";

	private static string AsStatements(string body) =>
		"internal sealed class Sample\n{\n\tprivate void Run()\n\t{\n" + body + "\n\t}\n}\n";

	private static void AssertCSharpierErasesTheDifference(
		string first,
		string second,
		string subject
	)
	{
		Assert.AreNotEqual(
			first,
			second,
			$"The {subject} examples do not differ, so the check proves nothing."
		);

		Assert.AreEqual(
			Format(first),
			Format(second),
			$"CSharpier keeps the difference in the {subject} example."
		);
	}

	private static void AssertCSharpierKeepsTheDifference(
		string first,
		string second,
		string subject
	)
	{
		Assert.AreNotEqual(
			first,
			second,
			$"The {subject} examples do not differ, so the check proves nothing."
		);

		Assert.AreNotEqual(
			Format(first),
			Format(second),
			$"CSharpier erases the difference in the {subject} example."
		);
	}

	private static string Format(string source)
	{
		Assert.IsTrue(
			s_formatted.Value.TryGetValue(source, out string? formatted),
			$"The example is not among those the run formats:\n{source}"
		);

		return formatted!;
	}

	// Every source a test formats, so one run of CSharpier serves them all.
	private static List<string> ReadSources()
	{
		List<string> sources =
		[
			AsMembers(PlainAccessors),
			AsMembers(SpacedAccessors),
			AsMembers(Summary + "\ninternal static void Bind()\n{\n}"),
		];

		foreach (object[] row in RuleExamples)
		{
			(string before, string after) = RuleExample((string)row[1]);

			Func<string, string> wrap = (bool)row[2] ? AsStatements : AsMembers;

			sources.Add(wrap(before));
			sources.Add(wrap(after));
		}

		foreach (string body in s_bodies)
		{
			sources.Add(AsMembers(BrokenArrowMember(body)));
			sources.Add(AsMembers(BlockMember(body)));
		}

		return [.. sources.Distinct(StringComparer.Ordinal)];
	}

	private static Dictionary<string, string> FormatAll()
	{
		List<string> sources = ReadSources();

		// The configuration is copied so that the files take the repository's settings.
		string directory = Path.Combine(
			Path.GetTempPath(),
			"stanza-formatter-claims-" + Guid.NewGuid().ToString("N")
		);

		try
		{
			Directory.CreateDirectory(directory);

			File.Copy(
				RepositoryPaths.Below(".csharpierrc.json"),
				Path.Combine(directory, ".csharpierrc.json")
			);

			for (int index = 0; index < sources.Count; index++)
				File.WriteAllText(Path.Combine(directory, $"{index}.cs"), sources[index]);

			(int exitCode, string error) = RunCSharpier(directory);

			Assert.AreEqual(
				0,
				exitCode,
				$"'dotnet tool run csharpier' failed. Run 'dotnet tool restore' first. {error}"
			);

			return Enumerable
				.Range(0, sources.Count)
				.ToDictionary(
					index => sources[index],

					index =>
						File.ReadAllText(Path.Combine(directory, $"{index}.cs"))
							.ReplaceLineEndings("\n"),

					StringComparer.Ordinal
				);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	private static (int ExitCode, string Error) RunCSharpier(string directory)
	{
		// The tool is pinned in .config/dotnet-tools.json, so the command runs from the repository root.
		ProcessStartInfo start = new(DotnetHost.Executable)
		{
			WorkingDirectory = RepositoryPaths.Root(),
			RedirectStandardError = true,
			RedirectStandardOutput = true,
			UseShellExecute = false,
		};

		start.ArgumentList.Add("tool");
		start.ArgumentList.Add("run");
		start.ArgumentList.Add("csharpier");
		start.ArgumentList.Add("format");
		start.ArgumentList.Add(directory);

		RunResult result = ChildProcess.Run(start, "dotnet tool run csharpier format");

		return (result.ExitCode, result.Error);
	}
}
