using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

using ListKind = (string Name, string Header, string Brackets, string Element, bool Joins);

namespace Nickelony.Stanza.Analyzers.Tests.Guards;

/// <summary>
/// Runs the pinned CSharpier on the examples that the formatter page and the rule pages describe.
/// </summary>
[TestClass]
[TestCategory("Repository")]
public sealed partial class FormatterClaimGuardTests
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

	private const string PaddedProject =
		"<Project>\n\n  <PropertyGroup>\n    <TargetFramework>net8.0</TargetFramework>\n\n  </PropertyGroup>\n</Project>\n";

	private const string TightProject =
		"<Project>\n  <PropertyGroup>\n    <TargetFramework>net8.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n";

	private const string TightSlashProject =
		"<Project>\n  <ItemGroup>\n    <Compile Include=\"a.cs\"/>\n  </ItemGroup>\n</Project>\n";

	private const string SpacedSlashProject =
		"<Project>\n  <ItemGroup>\n    <Compile Include=\"a.cs\" />\n  </ItemGroup>\n</Project>\n";

	private const string EndTagProject =
		"<Project>\n  <ItemGroup>\n    <Compile Include=\"a.cs\"></Compile>\n  </ItemGroup>\n</Project>\n";

	// The comments differ in their spacing and their words, and none of them names the condition.
	private const string EndifComments =
		"#if DEBUG\n\tprivate int _first;\n\tprivate int _second;\n#endif //DEBUG\n\n"
		+ "#if DEBUG\n\tprivate int _third;\n#endif    //     note   here\n\n"
		+ "#if DEBUG\n\tprivate int _fourth;\n#endif //not-a-name";

	// The name makes a for header longer than the print width, so CSharpier breaks it.
	private const string LongName =
		"TheNumberOfPositionsThatTheProbeWalksBackFromTheTokenBeforeItMeetsACharacterThatIsNotTheEscapeCharacter";

	private const string Summary =
		"/// <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary>";

	private static readonly string[] s_bodies =
	[
		"value with { A = 1 };",
		"from item in value select item;",
	];

	// The examples all go to CSharpier in one run, because each run starts a process that costs seconds.
	private static readonly Lazy<Dictionary<string, string>> s_formatted = new(FormatAll);

	// Each kind of list holds an element four times, named too long for one line, and twice with short names.
	// The brackets of a kind close its declaration, and an enum and a switch expression are never joined, so they have no joined form.
	private static readonly ListKind[] s_listKinds =
	[
		("an enum", "private enum Level", "{}", "{0}", false),
		("an array initializer", "private string[] Names = new[]", "{};", "\"{0}\"", true),
		("a collection initializer", "private List<string> Names = new()", "{};", "\"{0}\"", true),
		("an object initializer", "private Options Value = new()", "{};", "{0} = {1}", true),
		("an anonymous object", "private object Value = new", "{};", "{0} = {1}", true),
		("a with initializer", "private Options Next = Value with", "{};", "{0} = {1}", true),
		("a collection expression", "private string[] Names =", "[];", "\"{0}\"", true),
		("a switch expression", "private string Name = 1 switch", "{};", "{1} => \"{0}\"", false),
		("a list pattern", "private bool Matches = Value is", "[];", "\"{0}\"", true),
	];

	// CSharpier keeps the comma of a property pattern as written, so it has no row above.
	private static readonly ListKind s_propertyPattern = (
		"a property pattern",
		"private bool Matches = Value is",
		"{};",
		"{0}: {1}",
		true
	);

	public static IEnumerable<object[]> ListKinds { get; } = [.. s_listKinds.Select(ReadListKind)];

	public static IEnumerable<object[]> RuleExamples { get; } =
	[
		["the blank line between block accessors", "ZA0101", Members, Erases],
		["the blank line between collection elements", "ZA0141", Statements, Erases],
		["the blank line between arguments", "ZA0151", Statements, Erases],
		["the blank line between parameters", "ZA0161", Members, Erases],
		["the blank line between declarators", "ZA0171", Statements, Erases],
		["the blank line before a clause keyword", "ZA0201", Statements, Erases],
		["the brace padding", "ZA0203", Members, Erases],
		["the empty block", "ZA0204", Members, Erases],
		["the extra blank lines", "ZA0056", Members, Erases],
		["the comma after the last enum member added", "ZA0198", Members, Erases],
		["the comma after the last enum member removed", "ZA0199", Members, Erases],
		["the blank line between switch sections", "ZA0111", Statements, Keeps],
		["the blank line between switch expression arms", "ZA0121", Statements, Keeps],
		["the blank line between object initializer members", "ZA0131", Statements, Keeps],
		["the blank line around a region marker", "ZA0401", Members, Keeps],
		["the blank line around a conditional block", "ZA0402", Members, Keeps],
		["the blank line above a jump statement", "ZA0009", Statements, Keeps],
		["the blank line beside the base call", "ZA0010", Members, Keeps],
		["the blank line below the guard clauses", "ZA0011", Statements, Keeps],
	];

	// Each row holds an empty block body below its header, as ZA0204 leaves it, and the line CSharpier writes for it.
	public static IEnumerable<object[]> EmptyBodies { get; } =
	[
		[
			"a for loop",
			"\t\tfor (int i = 0; i < 1; i++)\n\t\t{ }",
			"\t\tfor (int i = 0; i < 1; i++) { }",
		],

		["a while loop", "\t\twhile (Flag)\n\t\t{ }", "\t\twhile (Flag) { }"],
		["a do loop", "\t\tdo\n\t\t{ }\n\t\twhile (Flag);", "\t\tdo { } while (Flag);"],

		[
			"a foreach loop",
			"\t\tforeach (int item in Items)\n\t\t{ }",
			"\t\tforeach (int item in Items) { }",
		],

		["a using statement", "\t\tusing (Resource)\n\t\t{ }", "\t\tusing (Resource) { }"],
		["a lock statement", "\t\tlock (Gate)\n\t\t{ }", "\t\tlock (Gate) { }"],

		[
			"a fixed statement",
			"\t\tfixed (byte* pointer = Buffer)\n\t\t{ }",
			"\t\tfixed (byte* pointer = Buffer) { }",
		],

		["an if statement", "\t\tif (Flag)\n\t\t{ }", "\t\tif (Flag) { }"],
		["an else clause", "\t\tif (Flag)\n\t\t\tRun();\n\t\telse\n\t\t{ }", "\t\telse { }"],

		[
			"a for loop whose header CSharpier breaks",
			$"\t\tfor (int i = 0; i < {LongName} && i < {LongName}; i++)\n\t\t{{ }}",
			"\t\t) { }",
		],
	];

	// Each row holds an empty loop body that both CSharpier and IDE2001 keep below its header.
	public static IEnumerable<object[]> KeptEmptyBodies { get; } =
	[
		["an empty statement", "\t\tfor (int i = 0; i < 1; i++)\n\t\t\t;"],

		[
			"a block that holds a comment",
			"\t\tfor (int i = 0; i < 1; i++)\n\t\t{\n\t\t\t// Nothing to do.\n\t\t}",
		],
	];

	// A tuple is a list of its own, so each row holds a spaced and a tight form of one.
	public static IEnumerable<object[]> TupleExamples { get; } =
	[
		[
			"the blank line between tuple elements",
			Statements,
			"\t\tvar pair = (\n\t\t\tBuild(\n\t\t\t\t1\n\t\t\t),\n\n\t\t\tBuild(\n\t\t\t\t2\n\t\t\t)\n\t\t);",
			"\t\tvar pair = (\n\t\t\tBuild(\n\t\t\t\t1\n\t\t\t),\n\t\t\tBuild(\n\t\t\t\t2\n\t\t\t)\n\t\t);",
		],

		[
			"the blank line between tuple type elements",
			Members,
			"\tprivate (\n\t\tIReadOnlyDictionary<string, int> lookup,\n\n\t\tint capacity) Pair;",
			"\tprivate (\n\t\tIReadOnlyDictionary<string, int> lookup,\n\t\tint capacity) Pair;",
		],

		[
			"the blank line above a comment in a tuple",
			Statements,
			"\t\tvar pair = (\n\t\t\t1,\n\n\t\t\t// The second element.\n\t\t\t2\n\t\t);",
			"\t\tvar pair = (\n\t\t\t1,\n\t\t\t// The second element.\n\t\t\t2\n\t\t);",
		],

		[
			"the blank line above a comment in a tuple type",
			Members,
			"\tprivate (\n\t\tint first,\n\n\t\t// The second element.\n\t\tint second) Pair;",
			"\tprivate (\n\t\tint first,\n\t\t// The second element.\n\t\tint second) Pair;",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(TupleExamples),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void CSharpierErasesTheBlankLinesOfATupleAsItDoesThoseOfAnArgumentList(
		string name,
		bool statements,
		string spaced,
		string tight
	)
	{
		Func<string, string> wrap = statements ? AsStatements : AsMembers;
		AssertCSharpierErasesTheDifference(wrap(spaced), wrap(tight), name);
	}

	// IDE2001 moves such a body back below its header, so the formatter page lists the empty bodies that both keep.
	[TestMethod]
	[DynamicData(
		nameof(EmptyBodies),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void CSharpierJoinsAnEmptyBlockBodyToTheLineOfItsHeader(
		string name,
		string below,
		string joined
	)
	{
		Assert.Contains(
			"\n" + joined + "\n",
			Format(AsStatements(below)),
			$"CSharpier kept the empty body of {name} below its header."
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(KeptEmptyBodies),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void CSharpierKeepsAnEmptyStatementOrACommentedBlockBelowItsHeader(
		string name,
		string below
	)
	{
		Assert.AreEqual(
			AsStatements(below),
			Format(AsStatements(below)),
			$"CSharpier moved {name} of a loop."
		);
	}

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
	[DynamicData(
		nameof(ListKinds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public void CSharpierWritesTheCommaOfAListItBreaksAndDropsItFromAListItJoins(
		string name,
		string broken,
		string last,
		string joined
	)
	{
		Assert.DoesNotContain(
			last + ",",
			broken,
			$"The broken {name} already ends in a comma, so the check proves nothing."
		);

		Assert.Contains(
			last + ",\n",
			Format(AsMembers(broken)),
			$"CSharpier did not write the trailing comma of a {name} it breaks."
		);

		if (joined.Length == 0)
			return;

		Assert.IsTrue(
			TrailingComma().IsMatch(joined),
			$"The joined {name} holds no trailing comma, so the check proves nothing."
		);

		Assert.IsFalse(
			TrailingComma().IsMatch(Format(AsMembers(joined))),
			$"CSharpier kept the trailing comma of a {name} it joins."
		);
	}

	[TestMethod]
	public void CSharpierKeepsTheCommaOfABrokenPropertyPatternAsWritten()
	{
		AssertCSharpierKeepsTheDifference(
			AsMembers(BrokenPropertyPattern(comma: string.Empty)),
			AsMembers(BrokenPropertyPattern(comma: ",")),
			"the trailing comma of a property pattern"
		);
	}

	[TestMethod]
	public void CSharpierLeavesTheCommentAfterAnEndifAsWritten()
	{
		string formatted = Format(AsMembers(EndifComments));

		foreach (
			string line in EndifComments
				.Split('\n')
				.Where(line => line.StartsWith("#endif", StringComparison.Ordinal))
		)
		{
			Assert.Contains(
				line,
				formatted,
				$"CSharpier rewrote '{line}', which the rule pages say it leaves as written."
			);
		}
	}

	[TestMethod]
	public void CSharpierRemovesTheBlankLinesBesideTheTagsOfAProjectFile() =>
		AssertCSharpierErasesTheDifference(PaddedProject, TightProject, "markup element padding");

	[TestMethod]
	public void CSharpierSpacesTheSlashOfASelfClosingTagInAProjectFile()
	{
		AssertCSharpierErasesTheDifference(
			TightSlashProject,
			SpacedSlashProject,
			"self-closing tag"
		);

		Assert.Contains(
			"<Compile Include=\"a.cs\" />",
			Format(TightSlashProject),
			"CSharpier did not write the space before the slash."
		);
	}

	[TestMethod]
	public void CSharpierKeepsAnEmptyElementOfAProjectFileInEitherForm() =>
		AssertCSharpierKeepsTheDifference(EndTagProject, SpacedSlashProject, "empty element");

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

	// ZA0302 and ZA0304 keep the block of such a body, which they would otherwise break after its arrow.
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

	private static object[] ReadListKind(ListKind kind)
	{
		string broken = WriteList(
			kind,

			elements:
			[
				.. Enumerable
					.Range(1, 4)
					.Select(index => Fill(kind.Element, new string('n', 40) + index, index)),
			],

			comma: string.Empty
		);

		string last = Fill(kind.Element, new string('n', 40) + 4, 4);

		string joined = kind.Joins
			? WriteList(
				kind,
				elements: [Fill(kind.Element, "a", 1), Fill(kind.Element, "b", 2)],
				comma: ","
			)
			: string.Empty;

		return [kind.Name, broken, last, joined];
	}

	private static string WriteList(ListKind kind, string[] elements, string comma)
	{
		string items = string.Join(",\n", elements.Select(element => "\t\t" + element));
		return $"\t{kind.Header}\n\t{kind.Brackets[..1]}\n{items}{comma}\n\t{kind.Brackets[1..]}";
	}

	private static string BrokenPropertyPattern(string comma)
	{
		string[] elements =
		[
			.. Enumerable
				.Range(1, 4)
				.Select(index =>
					Fill(s_propertyPattern.Element, new string('n', 40) + index, index)
				),
		];

		return WriteList(s_propertyPattern, elements, comma);
	}

	private static string Fill(string element, string text, int index)
	{
		return element
			.Replace("{0}", text, StringComparison.Ordinal)
			.Replace("{1}", $"{index}", StringComparison.Ordinal);
	}

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
			AsMembers(EndifComments),
			PaddedProject,
			TightProject,
			TightSlashProject,
			SpacedSlashProject,
			EndTagProject,
			AsMembers(BrokenPropertyPattern(comma: string.Empty)),
			AsMembers(BrokenPropertyPattern(comma: ",")),
		];

		foreach (object[] row in ListKinds)
		{
			sources.Add(AsMembers((string)row[1]));

			if (((string)row[3]).Length > 0)
				sources.Add(AsMembers((string)row[3]));
		}

		foreach (object[] row in RuleExamples)
		{
			(string before, string after) = RuleExample((string)row[1]);
			Func<string, string> wrap = (bool)row[2] ? AsStatements : AsMembers;

			sources.Add(wrap(before));
			sources.Add(wrap(after));
		}

		foreach (object[] row in TupleExamples)
		{
			Func<string, string> wrap = (bool)row[1] ? AsStatements : AsMembers;

			sources.Add(wrap((string)row[2]));
			sources.Add(wrap((string)row[3]));
		}

		foreach (object[] row in EmptyBodies.Concat(KeptEmptyBodies))
			sources.Add(AsStatements((string)row[1]));

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
		using TempDirectory directory = new(editorConfig: false);

		File.Copy(
			RepositoryPaths.Below(".csharpierrc.json"),
			Path.Combine(directory.Root, ".csharpierrc.json")
		);

		// A project file is markup, which CSharpier formats by its extension.
		for (int index = 0; index < sources.Count; index++)
			_ = directory.Write(FileName(index, sources[index]), sources[index]);

		(int exitCode, string error) = RunCSharpier(directory.Root);

		Assert.AreEqual(
			0,
			exitCode,
			$"'dotnet tool run csharpier' failed. Run 'dotnet tool restore' first. {error}"
		);

		return Enumerable
			.Range(0, sources.Count)
			.ToDictionary(
				index => sources[index],
				index => directory.Read(FileName(index, sources[index])).ReplaceLineEndings("\n"),
				StringComparer.Ordinal
			);
	}

	private static string FileName(int index, string source) =>
		index + (source.StartsWith('<') ? ".csproj" : ".cs");

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

	[GeneratedRegex(@",\s*[\]}]")]
	private static partial Regex TrailingComma();
}
