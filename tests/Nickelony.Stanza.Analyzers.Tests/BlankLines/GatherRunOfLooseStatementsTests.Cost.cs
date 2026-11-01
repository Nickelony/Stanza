using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

public sealed partial class GatherRunOfLooseStatementsTests
{
	// A lone declaration holds back the first gap of the calls below it, and each retry after that extends the run.
	// A retry reads the siblings around its gap, so the retries add up to a read of the container and not to its square.
	[TestMethod]
	public void ARetryThatExtendsAGatheredRunReadsOnlyTheSiblingsBesideItsGap()
	{
		const int calls = 400;

		StringBuilder statements = new("\t\tDoThing(); var lone = 1;\n");

		for (int index = 0; index < calls; index++)
			statements.Append("\n\t\tDoThing();\n");

		ImmutableArray<SyntaxNode> siblings = ReadStatements(statements.ToString());
		var options = StanzaOptions.From(FixedConfigOptions.Empty);

		ImmutableHashSet<int> gathered = [];
		long read = 0;
		int widest = 0;

		// The gap below the lone declaration is held back, so the retries run from the next one.
		for (int boundary = 3; boundary < siblings.Length; boundary++)
		{
			gathered = gathered.Add(boundary);

			BlankLineGrouping.GatheredStretch stretch = BlankLineGrouping.ReadStretchAround(
				siblings,
				options,
				gapOwners: default,
				gathered,
				boundary,
				cancellationToken: CancellationToken.None
			);

			read += stretch.End - stretch.Start;
			widest = int.Max(widest, stretch.End - stretch.Start);
		}

		Assert.IsLessThanOrEqualTo(4, widest, "a retry read the run it extends.");
		Assert.IsLessThan((long)siblings.Length * 4, read, "the retries read too many siblings.");
	}

	// A using declaration and a plain one of the same type form one group, though no other kind of run joins them.
	// So a stretch crosses the gap between them, or a retry beside the group would judge half of it.
	[TestMethod]
	public void AStretchCrossesTheGapBetweenAUsingDeclarationAndAPlainOneOfTheSameType()
	{
		ImmutableArray<SyntaxNode> siblings = ReadStatements(
			"\t\tDoThing();\n\t\tusing IDisposable first = Open();\n\t\tIDisposable second = Open();\n\t\tDoThing();"
		);

		BlankLineGrouping.GatheredStretch stretch = BlankLineGrouping.ReadStretchAround(
			siblings,
			options: StanzaOptions.From(FixedConfigOptions.Empty),
			gapOwners: default,
			gathered: [3],
			boundary: 3,
			cancellationToken: CancellationToken.None
		);

		Assert.AreEqual(1, stretch.Start);
	}

	// The blocks mix lone declarations, calls and jumps in long runs of loose gaps, so the retries extend gathered runs.
	private static string CreateBlock(int seed, int limit, Random random)
	{
		return (seed % 3) switch
		{
			0 => SourceGenerators.CreateStatementSource(random, looseRun: limit + 4),
			1 => SourceGenerators.CreateGuardedSource(random, looseRun: limit + 4),
			_ => CreateRandomBlock(random),
		};
	}

	// A retry reads the run borders of the stretch around its gap, and they must be the ones the whole container has there.
	[TestMethod]
	public void TheStretchOfARetryHoldsTheRunBordersOfTheWholeContainer()
	{
		const int blocks = 600;

		var options = StanzaOptions.From(FixedConfigOptions.Empty);
		int compared = 0;

		for (int seed = 0; seed < blocks; seed++)
		{
			var random = new Random(seed);
			string source = CreateBlock(seed, limit: 1 + (seed % 2), random);

			// Half the blocks split their assignments by a kind their text picks, the none and several kinds among them,
			// since the stretch reads assignments as one kind and must still hold every split run.
			Func<SyntaxNode, BlankLineGrouping.AssignmentTarget>? readTarget =
				seed % 4 < 2
					? null
					: node => new BlankLineGrouping.AssignmentTarget(
						Kind: (BlankLineGrouping.AssignmentKind)(node.ToString().Length % 6)
					);

			foreach (
				BlockSyntax block in CSharpSyntaxTree
					.ParseText(source)
					.GetRoot()
					.DescendantNodes()
					.OfType<BlockSyntax>()
			)
			{
				ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(block);

				int[] loose =
				[
					.. Enumerable
						.Range(1, int.Max(0, siblings.Length - 1))
						.Where(index =>
							BlankLineGaps.HasBlankLineBetween(siblings[index - 1], siblings[index])
						),
				];

				// The gaps gathered so far are a random share of the loose ones, and the retry adds one more.
				ImmutableHashSet<int> gathered = [];

				foreach (int boundary in loose.OrderBy(_ => random.Next()))
				{
					gathered = gathered.Add(boundary);

					BlankLineGrouping.GatheredStretch stretch = BlankLineGrouping.ReadStretchAround(
						siblings,
						options,
						gapOwners: default,
						gathered,
						boundary,
						cancellationToken: CancellationToken.None
					);

					var container = new BlankLineGrouping.GatheredStretch(
						gathered,
						Start: 0,
						End: siblings.Length
					);

					ImmutableArray<BlankLineGrouping.RunBorder> inStretch =
						BlankLineGrouping.ReadRunBorders(
							siblings,
							options,
							gapOwners: default,
							gathered: stretch,
							readTarget,
							cancellationToken: CancellationToken.None
						);

					ImmutableArray<BlankLineGrouping.RunBorder> inContainer =
						BlankLineGrouping.ReadRunBorders(
							siblings,
							options,
							gapOwners: default,
							gathered: container,
							readTarget,
							cancellationToken: CancellationToken.None
						);

					// A stretch that continues above starts inside a run, which the container starts higher.
					int first = stretch.ContinuesAbove ? stretch.Start + 1 : stretch.Start;

					string[] expected =
					[
						.. inContainer
							.Where(border =>
								border.RunStart >= first && border.RunEnd <= stretch.End
							)
							.Select(Describe),
					];

					string[] actual =
					[
						.. inStretch
							.Where(border =>
								border.RunStart >= first && border.RunEnd <= stretch.End
							)
							.Select(Describe),
					];

					Assert.AreSequenceEqual(
						expected,
						actual,
						$"seed {seed}: the stretch {stretch.Start}..{stretch.End} of the gap {boundary} read other borders than the container.\n{source}"
					);

					compared += expected.Length;
				}
			}
		}

		Assert.IsGreaterThan(
			blocks,
			compared,
			"the blocks hold too few borders to prove anything."
		);
	}

	private static string Describe(BlankLineGrouping.RunBorder border) =>
		$"{border.Boundary} {border.RunStart}..{border.RunEnd} {border.Family}";

	// The veto rounds run on the same blocks, and none of them has needed a third round.
	// The count is shared by every analyzer run, so no other test runs beside this one.
	[TestMethod]
	[DoNotParallelize]
	public async Task NoContainerNeedsAThirdVetoRound()
	{
		const int blocks = 600;

		BlankLineLooseRuns.DiscardThirdRounds();

		var analyzer = new GatherRunOfLooseStatementsAnalyzer();
		int reported = 0;

		for (int seed = 0; seed < blocks; seed++)
		{
			// Each block gets the budget of a run, so a loaded machine slows the test and does not cancel it.
			HarnessBudget.Restart();

			var random = new Random(seed);

			int limit = 1 + (seed % 2);
			string source = CreateBlock(seed, limit, random);

			Dictionary<string, ReportDiagnostic> enabled = [];

			foreach (DiagnosticDescriptor rule in s_askingRules)
			{
				enabled[rule.Id] =
					random.Next(5) == 0 ? ReportDiagnostic.Suppress : ReportDiagnostic.Warn;
			}

			enabled[DiagnosticDescriptors.GatherRunOfLooseStatements.Id] = ReportDiagnostic.Warn;

			AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(
				source,

				new RunOptions([analyzer])
				{
					DiagnosticOptions = enabled,

					AnalyzerConfigOptions = new Dictionary<string, string>
					{
						[LimitKey] = $"{limit}",
						["stanza_guard_roots"] = "Guard",
					},
				}
			);

			reported += result.Diagnostics.Length;
		}

		Assert.IsGreaterThan(
			blocks,
			reported,
			"the blocks hold too few loose runs to prove anything."
		);

		Assert.AreEqual(
			0,
			BlankLineLooseRuns.ThirdRounds,
			"a container needed a third veto round."
		);
	}

	// A veto round shrinks the removable gaps or ends the loop, so a container of n gaps takes at most n + 1 rounds.
	// Long containers where nearly every gap sets a lone declaration or a guard apart hold back the most gaps, and still end in two.
	[TestMethod]
	[DoNotParallelize]
	public async Task ALongContainerThatHoldsBackManyGapsSettlesInTwoRounds()
	{
		const int statements = 240;

		BlankLineLooseRuns.DiscardThirdRounds();

		var analyzer = new GatherRunOfLooseStatementsAnalyzer();
		int reported = 0;

		for (int seed = 0; seed < 6; seed++)
		{
			HarnessBudget.Restart();

			var random = new Random(seed);

			string source =
				seed < 3
					? CreateRandomBlock(random, length: statements)
					: CreateLoneDeclarationBlock(statements, guards: seed == 5);

			Dictionary<string, ReportDiagnostic> enabled = [];

			foreach (DiagnosticDescriptor rule in s_askingRules)
				enabled[rule.Id] = ReportDiagnostic.Warn;

			enabled[DiagnosticDescriptors.GatherRunOfLooseStatements.Id] = ReportDiagnostic.Warn;

			AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(
				source,

				new RunOptions([analyzer])
				{
					DiagnosticOptions = enabled,

					AnalyzerConfigOptions = new Dictionary<string, string>
					{
						[LimitKey] = "1",
						["stanza_guard_roots"] = "Guard",
					},
				}
			);

			reported += result.Diagnostics.Length;
		}

		Assert.IsGreaterThan(0, reported, "the blocks hold no loose run to prove anything.");

		Assert.AreEqual(
			0,
			BlankLineLooseRuns.ThirdRounds,
			"a long container needed a third veto round."
		);
	}

	// Every gap holds a blank line, and a lone declaration stands above each pair of calls, so each pair gathers behind a held-back gap.
	private static string CreateLoneDeclarationBlock(int statements, bool guards)
	{
		StringBuilder lines = new();

		for (int index = 0; index < statements; index++)
		{
			lines.Append(index == 0 ? "\t\t" : "\n\n\t\t");

			lines.Append(
				(index % 3, guards && index % 7 == 6) switch
				{
					(_, true) => "if (f == 0) return;",
					(0, _) => $"int i{index} = 1;",
					_ => "Foo();",
				}
			);
		}

		return "class C\n{\n\tint f;\n\tvoid Foo(int x = 0) { }\n\n\tvoid M()\n\t{\n"
			+ lines
			+ "\n\t}\n}\n";
	}

	private static readonly DiagnosticDescriptor[] s_askingRules =
	[
		DiagnosticDescriptors.SeparateDeclarationRuns,
		DiagnosticDescriptors.SeparateInvocationRuns,
		DiagnosticDescriptors.SeparateAssertionRuns,
		DiagnosticDescriptors.SeparateAwaitRuns,
		DiagnosticDescriptors.SeparateEventSubscriptionRuns,
		DiagnosticDescriptors.SeparateAssignmentRuns,
		DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
		DiagnosticDescriptors.SeparateMultiLineConstructs,
		DiagnosticDescriptors.SeparateCommentedSibling,
		DiagnosticDescriptors.SeparateJumpStatement,
		DiagnosticDescriptors.SeparateBaseCall,
		DiagnosticDescriptors.SeparateGuardClauses,
		DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
	];

	// Two thirds of the gaps hold a blank line, so loose runs are long and a lone declaration often stands above one.
	private static string CreateRandomBlock(Random random, int length = 0)
	{
		StringBuilder statements = new();
		length = length > 0 ? length : 5 + random.Next(10);

		for (int index = 0; index < length; index++)
		{
			statements.Append(
				index == 0 ? "\t\t"
				: random.Next(3) == 0 ? "\n\t\t"
				: "\n\n\t\t"
			);

			statements.Append(
				random.Next(16) switch
				{
					0 => "Bar();",
					1 => $"int i{index} = 1;",
					2 => $"string s{index} = \"\";",
					3 => $"var v{index} = 1;",
					4 => $"const int k{index} = 1;",
					5 => $"f = {index};",
					6 => "Foo(\n\t\t\t1);",
					7 => "Guard.NotNull(f);",
					8 => "if (f == 0) return;",
					9 => "Assert.That(f);",
					10 => "Changed += Handler;",
					11 => $"// c{index}\n\t\tFoo();",
					12 => $"using IDisposable u{index} = Open();",
					13 => $"IDisposable d{index} = Open();",
					_ => "Foo();",
				}
			);
		}

		return "using System;\nclass C\n{\n\tint f;\n\tevent Action Changed;\n\tvoid Handler() { }\n\tvoid Foo(int x = 0) { }\n\tvoid Bar() { }\n"
			+ "\tIDisposable Open() => null!;\n"
			+ "\tstatic class Guard { public static void NotNull(object o) { } }\n"
			+ "\tstatic class Assert { public static void That(int o) { } }\n\n"
			+ "\tvoid M()\n\t{\n"
			+ statements
			+ "\n\t}\n}\n";
	}

	private static ImmutableArray<SyntaxNode> ReadStatements(string statements)
	{
		string source =
			"using System;\nclass C\n{\n\tIDisposable Open() => null!;\n\n\tvoid DoThing() { }\n\n\tvoid M()\n\t{\n"
			+ statements
			+ "\n\t}\n}\n";

		SyntaxNode block = CSharpSyntaxTree
			.ParseText(source)
			.GetRoot()
			.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.Single(method => method.Identifier.ValueText == "M")
			.Body!;

		return StanzaSyntax.ReadSiblings(block);
	}
}
