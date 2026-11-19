using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class KeepDependentBindingsTogetherTests
{
	[TestMethod]
	public async Task AnAwaitedBindingPipelineKeepsItsDifferentTypesTogether()
	{
		const string source = """
			using System.IO;
			using System.Threading.Tasks;
			class C
			{
				async Task M(FileHandle fileHandle)
				{
					File file = await fileHandle.GetFileAsync();

					byte[] bytes = await file.ArrayBufferAsync();

					var stream = new MemoryStream(bytes);
				}
			}
			class FileHandle
			{
				public Task<File> GetFileAsync() => Task.FromResult(new File());
			}
			class File
			{
				public Task<byte[]> ArrayBufferAsync() => Task.FromResult(System.Array.Empty<byte>());
			}
			""";

		const string fixedSource = """
			using System.IO;
			using System.Threading.Tasks;
			class C
			{
				async Task M(FileHandle fileHandle)
				{
					File file = await fileHandle.GetFileAsync();
					byte[] bytes = await file.ArrayBufferAsync();
					var stream = new MemoryStream(bytes);
				}
			}
			class FileHandle
			{
				public Task<File> GetFileAsync() => Task.FromResult(new File());
			}
			class File
			{
				public Task<byte[]> ArrayBufferAsync() => Task.FromResult(System.Array.Empty<byte>());
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepDependentBindingsTogether,
		]);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepDependentBindingsTogetherAnalyzer>(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("var a = Get();\n\nvar b = Next(a);", "var a = Get();\nvar b = Next(a);")]
	[DataRow("const int a = 1;\n\nconst int b = a + 1;", "const int a = 1;\nconst int b = a + 1;")]
	[DataRow("var a = Get();\n\nvar b = a.Length;", "var a = Get();\nvar b = a.Length;")]
	[DataRow("var a = Get();\n\nvar b = $\"{a}\";", "var a = Get();\nvar b = $\"{a}\";")]
	[DataRow(
		"var a = Get();\n\nvar b = Next(a?.Value);",
		"var a = Get();\nvar b = Next(a?.Value);"
	)]
	[DataRow("var a = Get();\n\nvar b = obj.a;", "var a = Get();\n\nvar b = obj.a;")]
	[DataRow("var a = Get();\n\nvar b = \"a\";", "var a = Get();\n\nvar b = \"a\";")]
	[DataRow("var a = Get();\n\nvar b = nameof(a);", "var a = Get();\n\nvar b = nameof(a);")]
	[DataRow(
		"var a = Get();\n\nvar b = new System.Func<dynamic>(() => a);",
		"var a = Get();\n\nvar b = new System.Func<dynamic>(() => a);"
	)]
	[DataRow(
		"var a = Get();\n\nvar b = new System.Func<dynamic>(() => { return a; });",
		"var a = Get();\n\nvar b = new System.Func<dynamic>(() => { return a; });"
	)]
	[DataRow(
		"var a = new int[] { 1 };\n\nvar b = from item in a select item;",
		"var a = new int[] { 1 };\n\nvar b = from item in a select item;"
	)]
	[DataRow("var a = Get();\n\nvar b = Write(out a);", "var a = Get();\n\nvar b = Write(out a);")]
	[DataRow("var a = Get();\n\nvar b = (a = Get());", "var a = Get();\n\nvar b = (a = Get());")]
	[DataRow("var a = Get();\n\nvar b = (a += 1);", "var a = Get();\n\nvar b = (a += 1);")]
	[DataRow(
		"var a = Get();\n\nvar b = ((a = Get()), Next(a));",
		"var a = Get();\n\nvar b = ((a = Get()), Next(a));"
	)]
	[DataRow("var a = Get();\n\nvar b = Next(a++);", "var a = Get();\n\nvar b = Next(a++);")]
	[DataRow(
		"var a = Get();\n\nvar b = Get();\n\nvar c = Next(a);",
		"var a = Get();\n\nvar b = Get();\n\nvar c = Next(a);"
	)]
	[DataRow(
		"var a = Get();\n\n// Section.\nvar b = Next(a);",
		"var a = Get();\n\n// Section.\nvar b = Next(a);"
	)]
	[DataRow(
		"var a = Get(); // Section.\n\nvar b = Next(a);",
		"var a = Get(); // Section.\n\nvar b = Next(a);"
	)]
	[DataRow(
		"var a = Get();\n#pragma warning disable CS0219\nvar b = Next(a);",
		"var a = Get();\n#pragma warning disable CS0219\nvar b = Next(a);"
	)]
	[DataRow("var a = Get(\n\t1);\n\nvar b = Next(a);", "var a = Get(\n\t1);\n\nvar b = Next(a);")]
	[DataRow("var a = Get();\n\nvar b = Next(\n\ta);", "var a = Get();\n\nvar b = Next(\n\ta);")]
	[DataRow(
		"var a = Get(); var b = Next(a);\n\nvar c = Next(b);",
		"var a = Get(); var b = Next(a);\n\nvar c = Next(b);"
	)]
	[DataRow(
		"var (a, b) = (Get(), Get());\n\nvar c = Next(b);",
		"var (a, b) = (Get(), Get());\n\nvar c = Next(b);"
	)]
	[DataRow(
		"var a = Get();\n\nvar (b, c) = (a, Get());",
		"var a = Get();\n\nvar (b, c) = (a, Get());"
	)]
	[DataRow(
		"dynamic a = Get(), unused = Get();\n\nvar b = Next(a);",
		"dynamic a = Get(), unused = Get();\n\nvar b = Next(a);"
	)]
	[DataRow(
		"using var a = new System.IO.MemoryStream();\n\nvar b = a.Length;",
		"using var a = new System.IO.MemoryStream();\n\nvar b = a.Length;"
	)]
	[DataRow(
		"var a = Get();\n\nusing var b = new System.IO.MemoryStream(a);",
		"var a = Get();\n\nusing var b = new System.IO.MemoryStream(a);"
	)]
	[DataRow(
		"var a = Get();\nvar b = Next(a);\n\nvar c = Next(b);\nvar d = Next(c);",
		"var a = Get();\nvar b = Next(a);\n\nvar c = Next(b);\nvar d = Next(c);"
	)]
	[DataRow(
		"var a = Get();\n\nvar b = Next(a);\n\nvar c = Next(b);\nvar d = Next(c);",
		"var a = Get();\nvar b = Next(a);\nvar c = Next(b);\nvar d = Next(c);"
	)]
	[DataRow("var a = Get();\n\nvar b = Read(in a);", "var a = Get();\nvar b = Read(in a);")]
	[DataRow("var a = Get();\n\nvar b = Touch(ref a);", "var a = Get();\n\nvar b = Touch(ref a);")]
	public async Task OnlyImmediateSingleLineRuntimeDependenciesJoin(string before, string after)
	{
		string source = BuildSource(before);
		string fixedSource = BuildSource(after);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepDependentBindingsTogetherAnalyzer>(
				source,

				TestRuleSets.OnSuppressingAllOthers([
					DiagnosticDescriptors.KeepDependentBindingsTogether,
				])
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	[DataRow(false, false)]
	[DataRow(false, true)]
	[DataRow(true, false)]
	[DataRow(true, true)]
	public async Task DeclarationFamiliesCannotSplitTheDependentPipeline(bool types, bool keepsRuns)
	{
		string source = BuildSource(
			"Use(input);\nint a = 1;\n\ndynamic b = Next(a);\n\nvar c = Next(b);"
		);

		string fixedSource = BuildSource(
			"Use(input);\n\nint a = 1;\ndynamic b = Next(a);\nvar c = Next(b);"
		);

		DiagnosticDescriptor[] enabled =
		[
			DiagnosticDescriptors.KeepDependentBindingsTogether,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			.. types ? new[] { DiagnosticDescriptors.SeparateSameTypeDeclarationRuns } : [],
			.. keepsRuns ? new[] { DiagnosticDescriptors.KeepRunOfStatementsTogether } : [],
		];

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.OnSuppressingAllOthers(enabled)
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task DestructuringShapeSeparationRemainsIndependent()
	{
		string source = BuildSource(
			"var (a, b) = (1, 2);\nvar (c, d) = (3, 4);\nvar total = d + 1;\nvar limit = 10;"
		);

		string fixedSource = BuildSource(
			"var (a, b) = (1, 2);\nvar (c, d) = (3, 4);\n\nvar total = d + 1;\nvar limit = 10;\n"
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.OnSuppressingAllOthers([
				DiagnosticDescriptors.KeepDependentBindingsTogether,
				DiagnosticDescriptors.SeparateDeclarationRuns,
			]),

			new Dictionary<string, string> { ["stanza_declaration_shapes"] = "split" }
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task DependentSectionsStayProtectedFromWrittenTypeKeeping()
	{
		string source = BuildSource(
			"int a = 1;\nvar b = (int)Next(a);\n\nvar c = (int)Next(b);\nint d = (int)Next(c);"
		);

		string fixedSource = source;

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.OnSuppressingAllOthers([
				DiagnosticDescriptors.KeepDependentBindingsTogether,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
				DiagnosticDescriptors.KeepRunOfStatementsTogether,
			])
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	[DataRow(false)]
	[DataRow(true)]
	public async Task JumpSeparationReadsDependencyJoinsBeforeAnyFix(bool keepsJump)
	{
		string source = BuildSource(
			"var a = Get();\n\nvar b = Next(a);\n\nvar c = Next(b);\nreturn c;",
			exits: true
		);

		string fixedSource = BuildSource(
			"var a = Get();\nvar b = Next(a);\nvar c = Next(b);\n\nreturn c;",
			exits: true
		);

		Dictionary<string, ReportDiagnostic> enabled = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepDependentBindingsTogether,
			DiagnosticDescriptors.SeparateJumpStatement,
			.. keepsJump ? new[] { DiagnosticDescriptors.KeepJumpWithBinding } : [],
		]);

		AnalyzerRunResult reported =
			await RoslynTestHarness.ReportAsync<SeparateJumpStatementAnalyzer>(source, enabled);

		Assert.HasCount(1, reported.Diagnostics);
		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, enabled);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task InitializationKeepingCannotTreatADependencyInnerGapAsAStanzaOpening()
	{
		string source = BuildSource("var a = Get();\n\nvar b = Next(a);\n\nb.Save();");
		string fixedSource = BuildSource("var a = Get();\nvar b = Next(a);\n\nb.Save();");

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.OnSuppressingAllOthers([
				DiagnosticDescriptors.KeepDependentBindingsTogether,
				DiagnosticDescriptors.KeepInitializationWithCalls,
			])
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task CheckedBindingGroupsRetainTheirSingleRemovalOwner()
	{
		string source = BuildSource(
			"object a = Get();\n\nobject b = Next(a);\nif (a is null || b is null) return null;"
		);

		string fixedSource = BuildSource(
			"object a = Get();\nobject b = Next(a);\nif (a is null || b is null) return null;"
		);

		Dictionary<string, ReportDiagnostic> enabled = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepDependentBindingsTogether,
			DiagnosticDescriptors.KeepGuardWithBinding,
		]);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, enabled);

		Assert.HasCount(1, reported.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.KeepGuardWithBinding.Id, reported.Diagnostics[0].Id);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, enabled);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task RelatedGroupsDoNotInventSectionsAtAnAlreadyTightUpperGap()
	{
		string source = BuildSource(
			"var x = 1;\n\nvar y = 2;\n\nvar z = 3;\nvar a = Get();\nvar b = Next(a);\nvar c = Next(b);\nSystem.Diagnostics.Debug.Assert(c is not null);\nUse(c);"
		);

		string fixedSource = BuildSource(
			"var x = 1;\nvar y = 2;\nvar z = 3;\n\nvar a = Get();\nvar b = Next(a);\nvar c = Next(b);\nSystem.Diagnostics.Debug.Assert(c is not null);\nUse(c);"
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,

			TestRuleSets.OnSuppressingAllOthers([
				DiagnosticDescriptors.KeepDependentBindingsTogether,
				DiagnosticDescriptors.KeepNarrowingAssertionWithBinding,
				DiagnosticDescriptors.SeparateCheckedStanza,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
				DiagnosticDescriptors.KeepRunOfStatementsTogether,
			])
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	[DataRow(false)]
	[DataRow(true)]
	public async Task TheCheckedSeparatorProjectsAboveTheWholeDependentChain(bool keepsGuard)
	{
		const string source = """
			class C
			{
				void M()
				{
					Log();

					var path = GetPath();

					var text = Read(path);
					if (text is null) throw new System.InvalidOperationException();
					Log();
				}

				static void Log() { }
				static string GetPath() => "a";
				static string Read(string path) => path;
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					Log();

					var path = GetPath();
					var text = Read(path);
					if (text is null) throw new System.InvalidOperationException();

					Log();
				}

				static void Log() { }
				static string GetPath() => "a";
				static string Read(string path) => path;
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepDependentBindingsTogether,
			DiagnosticDescriptors.SeparateCheckedStanza,
			.. keepsGuard ? new[] { DiagnosticDescriptors.KeepGuardWithBinding } : [],
		]);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules);

		RoslynTestHarness.AssertEachGapIsClaimedOnce(reported);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			fixedSource,
			source,
			new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}

	[TestMethod]
	public async Task APragmaBarrierDoesNotBecomePartOfAWholeChainFix()
	{
		string source = BuildSource(
			"var a = Get();\n\nvar b = Next(a);\n#pragma warning disable ZA0061\n\nvar c = Next(b);\n\nvar d = Next(c);"
		);

		string fixedSource = BuildSource(
			"var a = Get();\nvar b = Next(a);\n#pragma warning disable ZA0061\n\nvar c = Next(b);\n\nvar d = Next(c);"
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepDependentBindingsTogetherAnalyzer>(
				source,

				TestRuleSets.OnSuppressingAllOthers([
					DiagnosticDescriptors.KeepDependentBindingsTogether,
				])
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TopLevelBindingsCanFormADependentChain()
	{
		const string source = """
			int first = 1;

			string second = first.ToString();

			var third = second.Length;
			""";

		const string fixedSource = """
			int first = 1;
			string second = first.ToString();
			var third = second.Length;
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepDependentBindingsTogetherAnalyzer>(
				source,

				TestRuleSets.OnSuppressingAllOthers([
					DiagnosticDescriptors.KeepDependentBindingsTogether,
				])
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task OneFixCarriesAllEligibleDependencyGapsBeforeSectionsCanChange()
	{
		string source = BuildSource(
			"var a = Get();\n\nvar b = Next(a);\n\nvar c = Next(b);\nvar d = Next(c);"
		);

		string fixedSource = BuildSource(
			"var a = Get();\nvar b = Next(a);\nvar c = Next(b);\nvar d = Next(c);"
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			new RunOptions([new KeepDependentBindingsTogetherAnalyzer()])
			{
				DiagnosticOptions = TestRuleSets.OnSuppressingAllOthers([
					DiagnosticDescriptors.KeepDependentBindingsTogether,
				]),

				MaxFixRounds = 1,
			}
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	public static IEnumerable<object[]> DeclarationAndGatherSubsets { get; } =
		TestRuleSets.SubsetRows(
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			DiagnosticDescriptors.GatherRunOfLooseStatements,
			DiagnosticDescriptors.KeepRunOfStatementsTogether
		);

	[TestMethod]
	[CrossCutting]
	[DynamicData(
		nameof(DeclarationAndGatherSubsets),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task NativeDeclarationAndGatherCombinationsHaveOneOwnerAndOneFixedText(
		string name,
		DiagnosticDescriptor[] enabled
	)
	{
		string source = BuildSource(
			"var x = 1;\n\nvar y = 2;\n\nvar z = 3;\nvar a = Get();\n\nvar b = Next(a);\n\nvar c = Next(b);\nSystem.Diagnostics.Debug.Assert(c is not null);\nUse(c);"
		);

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepDependentBindingsTogether,
			DiagnosticDescriptors.KeepNarrowingAssertionWithBinding,
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateJumpStatement,
			DiagnosticDescriptors.KeepJumpWithBinding,
			DiagnosticDescriptors.KeepInitializationWithCalls,
			.. enabled,
		]);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules);

		if (!reported.Diagnostics.IsEmpty)
			RoslynTestHarness.AssertEachGapIsClaimedOnce(reported);

		AnalyzerRunResult first = await RoslynTestHarness.RunIterativelyAsync(source, rules);
		string fixedSource = first.FixedText;

		AnalyzerRunResult bottom = await RoslynTestHarness.RunIterativelyAsync(
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				FixesBottomFirst = true,
			}
		);

		RoslynTestHarness.AssertFixedText(fixedSource, bottom, name);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			fixedSource,
			source,
			new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}

	[TestMethod]
	[CrossCutting]
	public async Task ClaimingEveryGapOfADependentChainCostsInStepWithItsLength()
	{
		(int small, int large) = await LinearCost.AssertQueryLinearAsync(
			rule: StanzaRules.KeepDependentBindingsTogether,

			buildSource: units =>
				BuildSource(
					"var a0 = Get();\n"
						+ string.Join(
							"\n",
							Enumerable.Range(1, units).Select(index => $"var a{index} = Next(a{index - 1});")
						)
				),

			units: 50,
			rules: TestRuleSets.OnSuppressingAllOthers([DiagnosticDescriptors.KeepDependentBindingsTogether]),
			query: container => LinearCost.CountClaims(container, StanzaRules.KeepDependentBindingsTogether)
		);

		// Each binding of the chain reads the one above it, so the rule claims every gap of the chain.
		Assert.AreEqual(50, small);
		Assert.AreEqual(200, large);
	}

	// Each group joins a chain whose read asks for one boundary beyond those it writes, so the group costs one try.
	// The read of a container tries eight boundaries at most, so ten groups try eight and leave the rest to the same-type rule,
	// and every fix order settles on one text either way.
	[TestMethod]
	[DoNotParallelize]
	[DataRow(4, 4)]
	[DataRow(10, 8)]
	public async Task TheReadOfAChainTriesEightCrossingsAtMost(int groups, int expected)
	{
		static string Group(int index, bool settled)
		{
			string gap = settled ? string.Empty : "\n";

			return $"\t\tint first{index} = 1;\n{(settled ? "\n" : string.Empty)}\t\tint second{index} = 2;\n"
				+ $"\t\tstring text{index} = second{index}.ToString();\n{gap}"
				+ $"\t\tbyte[] bytes{index} = System.Text.Encoding.UTF8.GetBytes(text{index});\n{gap}"
				+ $"\t\tvar stream{index} = new System.IO.MemoryStream(bytes{index});\n";
		}

		static string Method(int count, bool settled)
		{
			return "class C\n{\n\tvoid M()\n\t{\n"
				+ string.Join("\n", Enumerable.Range(0, count).Select(index => Group(index, settled)))
				+ "\t}\n}\n";
		}

		string source = Method(groups, settled: false);
		string fixedSource = Method(groups, settled: true);

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepDependentBindingsTogether,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		]);

		BlankLineKeptRuns.DiscardCrossings();

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAsync(
			source,
			// One line ending reads the container once.
			new RunOptions([new KeepDependentBindingsTogetherAnalyzer()]) { DiagnosticOptions = rules, BothEndings = false }
		);

		Assert.HasCount(groups * 2, reported.Diagnostics);
		Assert.AreEqual(expected, BlankLineKeptRuns.Crossings);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			options: new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}

	// A search of the block for each declarator allocates nothing, so the bytes of a walk cannot see it: its time can.
	// The time is no limit, which a slow machine passes badly, but the growth from one size to four times it:
	// a linear read grows four times and a search of the locals for each one grows sixteen times.
	[TestMethod]
	[DoNotParallelize]
	public void ReadingTheLocalOfEveryDeclaratorOfALongBlockIsLinear()
	{
		const int locals = 10_000;
		const double allowedGrowth = 10;

		// A read that takes less than this stands for this, so a few milliseconds of noise are not a growth.
		const double floorMilliseconds = 5;

		double small = MeasureReadingLocals(locals);
		double large = MeasureReadingLocals(locals * 4);

		Assert.IsLessThan(
			allowedGrowth,
			large / Math.Max(small, floorMilliseconds),
			$"Reading {locals:N0} locals took {small:N1} ms and {locals * 4:N0} took {large:N1} ms."
		);
	}

	private static double MeasureReadingLocals(int locals)
	{
		string source = "class C\n{\n\tvoid M()\n\t{\n" + string.Concat(Enumerable.Range(0, locals).Select(index => $"\t\tint v{index} = {index};\n")) + "\t}\n}\n";

		SyntaxTree tree = CSharpSyntaxTree.ParseText(source);

		var compilation = CSharpCompilation.Create(
			assemblyName: "Locals",
			syntaxTrees: [tree],
			references: RoslynTestHarness.MetadataReferences
		);

		VariableDeclaratorSyntax[] declarators = [.. tree.GetRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>()];
		double fastest = double.MaxValue;

		for (int run = 0; run < 3; run++)
		{
			SemanticModel semanticModel = compilation.GetSemanticModel(tree);

			// The first read binds the block once, which is not what the pin measures.
			Assert.IsNotNull(BlankLineKeptPairs.FindDeclaredLocal(semanticModel, declarators[0], default));

			var clock = Stopwatch.StartNew();

			foreach (VariableDeclaratorSyntax declarator in declarators)
				Assert.IsNotNull(BlankLineKeptPairs.FindDeclaredLocal(semanticModel, declarator, default));

			fastest = Math.Min(fastest, clock.Elapsed.TotalMilliseconds);
		}

		return fastest;
	}

	private static string BuildSource(string body, bool exits = false)
	{
		const string template = """
			using System.Linq;
			class C
			{
				dynamic M(dynamic input, dynamic obj)
				{
					BODY
					EXIT
				}
				static dynamic Get(params dynamic[] args) => 1;
				static dynamic Next(dynamic value) => value;
			static dynamic Read(in dynamic value) => value;
			static dynamic Touch(ref dynamic value) { value = 1; return value; }
				static void Use(dynamic value) { }
				static bool Write(out dynamic value) { value = 1; return true; }
			}
			""";

		return template
			.ReplaceLineEndings("\n")
			.Replace(
				"\t\tBODY",

				string.Join(
					"\n",
					body.Split('\n').Select(line => line.Length == 0 ? line : "\t\t" + line)
				)
			)
			.Replace("\t\tEXIT\n", exits ? string.Empty : "\t\treturn 0;\n");
	}

	[TestMethod]
	[DataRow("input.Touch(out a)", false)]
	[DataRow("input.Touch(ref a)", false)]
	[DataRow("input.Touch(a, out a)", false)]
	[DataRow("input.Touch(a, ref a)", false)]
	[DataRow("input.Touch(a)", true)]
	[DataRow("Read(in a)", true)]
	[DataRow("input.Touch(a.Value)", true)]
	public async Task DynamicWritableArgumentsCannotConsumeThePreviousBinding(
		string value,
		bool joins
	)
	{
		string source = BuildSource("var a = Get();\n\nvar b = " + value + ";");

		string fixedSource = BuildSource(
			"var a = Get();\n" + (joins ? string.Empty : "\n") + "var b = " + value + ";"
		);

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepDependentBindingsTogether,
		]);

		AnalyzerRunResult batch = await RoslynTestHarness.RunAllAsync(source, rules);
		RoslynTestHarness.AssertFixedText(fixedSource, batch);
		AnalyzerRunResult iterative = await RoslynTestHarness.RunIterativelyAsync(source, rules);
		RoslynTestHarness.AssertFixedText(fixedSource, iterative);
	}

	[TestMethod]
	[DataRow("sink.Touch(out a!)", false)]
	[DataRow("sink.Touch(ref a!)", false)]
	[DataRow("sink.Touch(a, out a!)", false)]
	[DataRow("sink.Touch(out (a!))", false)]
	[DataRow("sink.Touch(ref (a!))", false)]
	[DataRow("sink.Touch(a!)", true)]
	[DataRow("sink.Touch(a!.Length)", true)]
	[DataRow("Read(in a!)", true)]
	public async Task NullableSuppressionCannotHideAWritableDynamicArgument(
		string value,
		bool joins
	)
	{
		string source =
			"#nullable enable\nclass C { void M(dynamic sink) {\nstring? a = null;\n\nvar b = "
			+ value
			+ ";\n} static int Read(in string? value) => value?.Length ?? 0; }";

		string fixedSource = joins ? source.Replace("null;\n\n", "null;\n") : source;

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepDependentBindingsTogether,
		]);

		AnalyzerRunResult batch = await RoslynTestHarness.RunAllAsync(source, rules);
		RoslynTestHarness.AssertFixedText(fixedSource, batch);
		AnalyzerRunResult iterative = await RoslynTestHarness.RunIterativelyAsync(source, rules);
		RoslynTestHarness.AssertFixedText(fixedSource, iterative);
	}

	[TestMethod]
	[DataRow("sink.Touch(out a!.Field)")]
	[DataRow("sink.Touch(ref a!.Field)")]
	public async Task WritableMembersStillReadTheirSuppressedBindingOwner(string value)
	{
		string source =
			"class Box { public int Field; } class C { void M(dynamic sink) {\nvar a = new Box();\n\nvar b = "
			+ value
			+ ";\n} }";

		string fixedSource = source.Replace("Box();\n\n", "Box();\n");

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepDependentBindingsTogether,
		]);

		AnalyzerRunResult batch = await RoslynTestHarness.RunAllAsync(source, rules);
		RoslynTestHarness.AssertFixedText(fixedSource, batch);
		AnalyzerRunResult iterative = await RoslynTestHarness.RunIterativelyAsync(source, rules);
		RoslynTestHarness.AssertFixedText(fixedSource, iterative);
	}
}
