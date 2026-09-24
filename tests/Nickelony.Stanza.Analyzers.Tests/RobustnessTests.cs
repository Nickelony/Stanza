using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class RobustnessTests
{
	public static IEnumerable<object[]> MalformedSources { get; } =
	[
		["a truncated parameter list", "class C { void M( { "],
		["an unterminated block", "class C { void M() { DoThing(); "],
		["an unterminated raw string", "class C { const string Value = \"\"\"\n<a>\n"],
		["an unterminated conditional region", "#if DEBUG\nclass C { }\n"],
		["a switch expression cut off mid arm", "class C { int Pick(int v) => v switch { 1 => "],
		["an expression body with no statement after it", "class C { int Value => \n"],
		["a control header with no body", "class C { void M(bool first) { if (first"],
		["a lone closing brace", "}"],
		["nothing at all", ""],

		[
			"a directive that renames the file",
			"#line 9999 \"SomewhereElse.cs\"\nclass C\n{\n\tvoid M()\n\t{\n\t\tDoThing();\n\t}\n}\n",
		],

		["a failed directive", "#error this file is broken on purpose\n"],

		[
			"a pragma that names no rule",
			"#pragma warning disable\nclass C\n{\n\tvoid M()\n\t{\n\t\tvar first = 1;\n\t\tvar second = 2;\n\t\tDoThing();\n\t}\n}\n#pragma warning restore\n",
		],

		[
			"a closing region marker that closes nothing",
			"class C\n{\n\tprivate int _first;\n\t#endregion\n\tprivate int _second;\n\t#region Fields\n\tprivate int _third;\n}\n",
		],

		[
			"a nesting past the parser's depth",
			"class C { void M() { DoThing()" + new string('(', 200),
		],

		[
			"a markup literal that was never closed",
			"class C\n{\n\tconst string Value = \"\"\"\n\t\t<root>\n\t\t\t<a />\n\t\t<b />\n",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(MalformedSources),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	[CrossCutting]
	public async Task EveryFixSurvivesSourceThatDoesNotCompile(string name, string source)
	{
		// A broken file may never settle, so the run asserts no crash and no fix without an edit, not a fixed point.
		// An analyzer crash or a provider that throws fails the row.
		AssertFailedException? failure = null;

		try
		{
			_ = await RoslynTestHarness.RunIterativelyAsync(
				source,

				new RunOptions(RoslynTestHarness.AllAnalyzers)
				{
					DiagnosticOptions = TestRuleSets.AllRulesEnabled,
					RequireCompiles = false,
					AssertFixedPoint = false,
				}
			);
		}
		catch (AssertFailedException exception)
		{
			failure = exception;
		}

		// The failure is rethrown outside the catch block, where MSTEST0058 expects an assertion.
		if (failure is not null)
			throw new AssertFailedException($"{name}: {failure.Message}", failure);
	}

	[TestMethod]
	public async Task RulesStillReportOnSourceThatDoesNotCompile()
	{
		// `DoWork` is defined nowhere, so the snippet does not compile.
		// The `#line` directive makes the diagnostics report the path `SomewhereElse.cs`.
		// The run keeps them because their location is in the snippet's syntax tree, whatever path that location reports.
		// The member is an ordinary one-statement block body, which `UseExpressionBodyForSingleLineMember` reports.
		const string source = """
			#line 9999 "SomewhereElse.cs"
			class C
			{
				void M()
				{
					DoWork();
				}
			}
			""";

		ImmutableArray<Diagnostic> diagnostics = await RoslynTestHarness.RunWithoutCompilingAsync(
			source,
			TestRuleSets.AllRulesEnabled
		);

		Assert.Contains(
			DiagnosticDescriptors.UseExpressionBodyForSingleLineMember.Id,
			diagnostics.Select(diagnostic => diagnostic.Id),
			"no rule looked at the source, so the survival rows above prove nothing."
		);
	}
}
