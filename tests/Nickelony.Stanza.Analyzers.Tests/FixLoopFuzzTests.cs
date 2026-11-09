using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Tests.Convergence;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Fixes seeded sources under random rule sets until they settle.</summary>
[TestClass]
public sealed class FixLoopFuzzTests
{
	private const int FirstSeed = 5000;

	private const char LineSeparator = (char)0x2028;
	private const char NextLine = (char)0x0085;

	// The rules that rewrite a token are left out, so the tokens of a source can be compared with the fixed ones.
	private static readonly DiagnosticDescriptor[] s_pool =
	[
		.. TestRuleSets
			.Declared.Select(rule => rule.Descriptor)
			.Where(descriptor =>
				!TestRuleSets.TokenRules.Contains(descriptor.Id)
				&& !TestRuleSets.ReportOnly.Contains(descriptor.Id)
			),
	];

	private static readonly RulePair[] s_pairs =
	[
		.. StanzaRules
			.All.Select(rule => rule.Pair)
			.OfType<RulePair>()
			.DistinctBy(pair => pair.First.Id),
	];

	// A pair has two halves that undo each other, so a set holds one of them at most.
	private static readonly (
		DiagnosticDescriptor First,
		DiagnosticDescriptor Second
	)[] s_exclusive = [.. s_pairs.Select(pair => (pair.First, pair.Second))];

	private sealed record FuzzCase(
		string Source,
		DiagnosticDescriptor[] Rules,
		ImmutableArray<DiagnosticAnalyzer> Analyzers,
		ImmutableArray<CodeFixProvider> Providers,
		Dictionary<string, string> Options
	);

	[TestMethod]
	public void ASeedAlwaysDrawsTheSameCase()
	{
		for (int seed = FirstSeed; seed < FirstSeed + 10; seed++)
		{
			FuzzCase first = CreateCase(seed);
			FuzzCase second = CreateCase(seed);

			Assert.AreEqual(first.Source, second.Source);
			Assert.AreSequenceEqual(Ids(first.Rules), Ids(second.Rules));

			Assert.AreSequenceEqual(
				first.Analyzers.Select(analyzer => analyzer.GetType().Name),
				second.Analyzers.Select(analyzer => analyzer.GetType().Name)
			);
		}
	}

	[TestMethod]
	public void TheCasesBreakLinesWithBothUnicodeSeparators()
	{
		FuzzCase[] cases = [.. Enumerable.Range(FirstSeed, 30).Select(CreateCase)];

		Assert.IsTrue(cases.Any(drawn => drawn.Source.Contains(LineSeparator)));
		Assert.IsTrue(cases.Any(drawn => drawn.Source.Contains(NextLine)));

		Assert.IsTrue(
			cases.Any(drawn =>
				!drawn.Source.Contains(LineSeparator) && !drawn.Source.Contains(NextLine)
			)
		);
	}

	[TestMethod]
	public void TheFirstLineBreakOfACaseStaysALineFeed()
	{
		foreach (FuzzCase drawn in Enumerable.Range(FirstSeed, 30).Select(CreateCase))
		{
			int firstBreak = drawn.Source.IndexOfAny(['\n', LineSeparator, NextLine]);
			Assert.AreEqual('\n', drawn.Source[firstBreak]);
		}
	}

	[TestMethod]
	public void NoCaseEnablesBothHalvesOfAPairAndEachSetVariesInSizeAndOrder()
	{
		FuzzCase[] cases = [.. Enumerable.Range(FirstSeed, 100).Select(CreateCase)];

		foreach (FuzzCase drawn in cases)
		{
			Assert.IsNotEmpty(drawn.Rules);

			foreach ((DiagnosticDescriptor first, DiagnosticDescriptor second) in s_exclusive)
			{
				Assert.IsFalse(
					drawn.Rules.Contains(first) && drawn.Rules.Contains(second),
					$"A case enables both {first.Id} and {second.Id}."
				);
			}
		}

		Assert.IsGreaterThan(10, cases.Select(drawn => drawn.Rules.Length).Distinct().Count());

		Assert.IsGreaterThan(
			10,

			cases
				.Select(drawn =>
					string.Join(",", drawn.Analyzers.Take(5).Select(a => a.GetType().Name))
				)
				.Distinct()
				.Count()
		);
	}

	// A test outside the soak would slow the plain run, since each seed fixes its source more than once.
	[TestMethod]
	[TestCategory("Soak")]
	[Timeout(SoakRun.NoRunnerTimeout)]
	[CrossCutting]
	public async Task RandomSetsOfRulesSettleTheSameWayOneFixAtATimeAndAsFixAllPerId()
	{
		int count = SoakRun.ReadCountOrSkip(
			$"Set {SoakRun.Variable} to the number of seeds to run."
		);

		await SoakRun.RunAsync(count, () => RunSeedsAsync(count));
	}

	private static async Task RunSeedsAsync(int count)
	{
		List<string> failures = [];

		int fixedSeeds = 0;
		int first = SoakRun.ReadFirstSeed(FirstSeed);

		for (int seed = first; seed < first + count; seed++)
		{
			// Each seed gets the budget of a whole test, so the soak's time grows with its seed count.
			HarnessBudget.Restart();

			try
			{
				if (await AssertCaseSettlesAsync(seed) > 0)
					fixedSeeds++;
			}
			catch (AssertFailedException exception)
			{
				failures.Add($"seed {seed}: {exception.Message}");
			}
			catch (OperationCanceledException) when (HarnessBudget.Token.IsCancellationRequested)
			{
				failures.Add($"seed {seed}: the run outlasted its budget.");
				break;
			}
			catch (Exception exception)
				when (exception is not (AssertFailedException or OperationCanceledException))
			{
				failures.Add(SoakRun.DescribeCrash(seed, exception));
			}
		}

		Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
		Assert.IsGreaterThan(count / 2, fixedSeeds, "Fewer than half of the sources needed a fix.");
	}

	private static async Task<int> AssertCaseSettlesAsync(int seed)
	{
		FuzzCase drawn = CreateCase(seed);

		string label =
			$"seed {seed}, rules {string.Join(",", Ids(drawn.Rules))} in the order {string.Join(",", drawn.Analyzers.Take(3).Select(analyzer => analyzer.GetType().Name))}:";

		var options = new RunOptions(drawn.Analyzers)
		{
			Providers = drawn.Providers,
			DiagnosticOptions = TestRuleSets.OnSuppressingAllOthers(drawn.Rules),
			AnalyzerConfigOptions = drawn.Options,
		};

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source: drawn.Source,
			options
		);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: result.FixedText,
			source: drawn.Source,
			options,
			message: label
		);

		Assert.AreSequenceEqual(
			ConvergenceTests.ReadTokens(drawn.Source),
			ConvergenceTests.ReadTokens(result.FixedText),
			$"{label} a fix changed a token."
		);

		Assert.AreSequenceEqual(
			ConvergenceTests.ReadComments(drawn.Source),
			ConvergenceTests.ReadComments(result.FixedText),
			$"{label} a fix changed a comment."
		);

		// A fix reuses the breaks of its source, so none writes a separator that the source did not hold.
		foreach (char separator in new[] { LineSeparator, NextLine })
		{
			Assert.IsLessThanOrEqualTo(
				drawn.Source.Count(character => character == separator),
				result.FixedText.Count(character => character == separator),
				$"{label} a fix wrote a line break of U+{(int)separator:X4}."
			);
		}

		return result.Diagnostics.Length;
	}

	private static FuzzCase CreateCase(int seed)
	{
		var random = new Random(seed);

		int limit = 1 + random.Next(3);

		string source = (seed % 5) switch
		{
			0 => SourceGenerators.CreateLayoutSource(random),
			1 => SourceGenerators.CreateStatementSource(random, looseRun: limit + 1),
			2 => SourceGenerators.CreateGuardedSource(random, looseRun: limit + 1),
			3 => SourceGenerators.CreateMemberSource(random, looseRun: limit + 1),
			_ => SourceGenerators.CreateFileSource(random, looseRun: limit + 1),
		};

		return new FuzzCase(
			Source: WithUnicodeBreaks(source, seed),
			Rules: PickRules(random),
			Analyzers: [.. RoslynTestHarness.AllAnalyzers.OrderBy(_ => random.Next())],
			Providers: [.. RoslynTestHarness.AllCodeFixProviders.OrderBy(_ => random.Next())],

			Options: new Dictionary<string, string>
			{
				["stanza_max_loose_run_size"] = $"{limit}",
				["stanza_guard_roots"] = "Guard",
				["stanza_assignment_targets"] = random.Next(2) == 0 ? "mixed" : "split",
			}
		);
	}

	private static DiagnosticDescriptor[] PickRules(Random random)
	{
		HashSet<string> barred =
		[
			.. s_exclusive.Select(pair => random.Next(2) == 0 ? pair.First.Id : pair.Second.Id),
		];

		DiagnosticDescriptor[] rules =
		[
			.. s_pool.Where(descriptor => !barred.Contains(descriptor.Id) && random.Next(2) == 0),
		];

		// A set of no rule would prove nothing, so one the draw left empty takes the first rule that is free.
		return rules.Length > 0
			? rules
			: [s_pool.First(descriptor => !barred.Contains(descriptor.Id))];
	}

	// Two of three cases break some lines with U+2028 or U+0085, which a source of one ending in the text otherwise lacks.
	// The first break stays a line feed, as the ending of the document.
	private static string WithUnicodeBreaks(string source, int seed)
	{
		if (seed % 3 == 2)
			return source;

		// The breaks have a draw of their own, so a source with them and one without share their rules.
		var random = new Random(seed);
		char separator = seed % 2 == 0 ? LineSeparator : NextLine;

		StringBuilder text = new(source.Length);
		bool seenFirst = false;

		foreach (char character in source)
		{
			if (character == '\n' && seenFirst && random.Next(5) == 0)
				text.Append(separator);
			else
				text.Append(character);

			seenFirst |= character == '\n';
		}

		return text.ToString();
	}

	private static string[] Ids(DiagnosticDescriptor[] rules) =>
		[.. rules.Select(rule => rule.Id).Order(StringComparer.Ordinal)];
}
