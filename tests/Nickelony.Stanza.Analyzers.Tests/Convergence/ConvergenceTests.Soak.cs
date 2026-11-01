using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Convergence;

public sealed partial class ConvergenceTests
{
	// A soak test has no runner timeout, since SoakRun holds it to a limit that grows with the seed count.
	// The corpora that cost the most run in tests of their own.
	[TestMethod]
	[TestCategory("Soak")]
	[Timeout(SoakRun.NoRunnerTimeout)]
	public Task ALargeSeededSetSettlesTheSameWay() =>
		RunSoakAsync([AssertLayoutSettlesAsync, AssertDocumentationSettlesAsync]);

	[TestMethod]
	[TestCategory("Soak")]
	[Timeout(SoakRun.NoRunnerTimeout)]
	public Task ALargeSeededSetOfStatementsSettlesTheSameWay() =>
		RunSoakAsync([AssertLooseRunsSettleAsync]);

	[TestMethod]
	[TestCategory("Soak")]
	[Timeout(SoakRun.NoRunnerTimeout)]
	public Task ALargeSeededSetOfGuardedStatementsSettlesTheSameWay() =>
		RunSoakAsync([AssertGuardedRunsSettleAsync]);

	[TestMethod]
	[TestCategory("Soak")]
	[Timeout(SoakRun.NoRunnerTimeout)]
	public Task ALargeSeededSetOfMembersSettlesTheSameWay() =>
		RunSoakAsync([AssertLooseMemberRunsSettleAsync]);

	[TestMethod]
	[TestCategory("Soak")]
	[Timeout(SoakRun.NoRunnerTimeout)]
	public Task ALargeSeededSetOfFilesSettlesTheSameWay() =>
		RunSoakAsync([AssertLooseFileRunsSettleAsync]);

	[TestMethod]
	[TestCategory("Soak")]
	[Timeout(SoakRun.NoRunnerTimeout)]
	public Task ALargeSeededSetOfChainsSettlesTheSameWay() =>
		RunSoakAsync([AssertSimplifyingRunsSettleAsync]);

	private static async Task RunSoakAsync(Func<int, Task<int>>[] runs)
	{
		int count = SoakRun.ReadCountOrSkip(
			$"Set {SoakRun.Variable} to the number of seeds to run."
		);

		await SoakRun.RunAsync(count, () => RunSeedsAsync(runs, count));
	}

	private static async Task RunSeedsAsync(Func<int, Task<int>>[] runs, int count)
	{
		List<string> failures = [];
		bool outlasted = false;
		int first = SoakRun.ReadFirstSeed(SoakFirstSeed);

		// A spent budget stops the soak at its seed, and the failures so far are reported below.
		for (int seed = first; seed < first + count && !outlasted; seed++)
		{
			// Each seed gets the budget of a whole test, so the soak's grows with its seed count.
			HarnessBudget.Restart();

			foreach (Func<int, Task<int>> run in runs)
			{
				try
				{
					_ = await run(seed);
				}
				catch (AssertFailedException exception)
				{
					failures.Add($"seed {seed}: {exception.Message.Split('\n')[0]}");
				}
				catch (OperationCanceledException)
					when (HarnessBudget.Token.IsCancellationRequested)
				{
					failures.Add($"seed {seed}: the run outlasted its budget.");
					outlasted = true;

					break;
				}
				catch (Exception exception)
					when (exception is not (AssertFailedException or OperationCanceledException))
				{
					failures.Add(SoakRun.DescribeCrash(seed, exception));
				}
			}
		}

		Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
	}

	private static async Task<int> AssertLayoutSettlesAsync(int seed)
	{
		var random = new Random(seed);

		string source = SourceGenerators.CreateLayoutSource(random);

		AnalyzerRunResult result = await RunAsync(
			source,
			rules: s_layoutRules,
			options: null,
			label: $"seed {seed}:"
		);

		Assert.AreSequenceEqual(
			ReadTokens(source),
			ReadTokens(result.FixedText),
			$"seed {seed}: a fix changed a token."
		);

		Assert.AreSequenceEqual(
			ReadComments(source),
			ReadComments(result.FixedText),
			$"seed {seed}: a fix changed a comment."
		);

		return result.Diagnostics.Length;
	}

	// The limit takes the seed's last two bits, so the setting takes the next one and meets every limit in both values.
	private static string ReadAssignmentTargets(int seed) => seed / 4 % 2 == 0 ? "mixed" : "split";

	private static Task<int> AssertLooseRunsSettleAsync(int seed)
	{
		int limit = s_looseLimits[seed % s_looseLimits.Length];

		string source = SourceGenerators.CreateStatementSource(
			new Random(seed),
			looseRun: limit + 1
		);

		return AssertSourceSettlesAsync(
			source,
			limit,
			label: $"seed {seed}:",
			targets: ReadAssignmentTargets(seed)
		);
	}

	private static Task<int> AssertGuardedRunsSettleAsync(int seed)
	{
		int limit = s_looseLimits[seed % s_looseLimits.Length];
		string source = SourceGenerators.CreateGuardedSource(new Random(seed), looseRun: limit + 1);

		return AssertSourceSettlesAsync(
			source,
			limit,
			label: $"seed {seed}:",
			targets: ReadAssignmentTargets(seed)
		);
	}

	private static Task<int> AssertLooseMemberRunsSettleAsync(int seed)
	{
		int limit = s_looseMemberLimits[seed % s_looseMemberLimits.Length];
		string source = SourceGenerators.CreateMemberSource(new Random(seed), looseRun: limit + 1);

		return AssertSourceSettlesAsync(
			source,
			limit,
			label: $"seed {seed}:",
			corpus: s_memberCorpus,
			targets: ReadAssignmentTargets(seed)
		);
	}

	private static Task<int> AssertLooseFileRunsSettleAsync(int seed)
	{
		int limit = s_looseMemberLimits[seed % s_looseMemberLimits.Length];
		string source = SourceGenerators.CreateFileSource(new Random(seed), looseRun: limit + 1);

		return AssertSourceSettlesAsync(
			source,
			limit,
			label: $"seed {seed}:",
			corpus: s_fileCorpus,
			targets: ReadAssignmentTargets(seed)
		);
	}

	// A fix order must not change the text,
	// so the first rule set settles on one text from the top and from the bottom.
	private static async Task<int> AssertSourceSettlesAsync(
		string source,
		int limit,
		string label,
		LooseCorpus? corpus = null,
		string targets = "mixed"
	)
	{
		corpus ??= s_statementCorpus;

		Dictionary<string, string> options = new()
		{
			["stanza_max_loose_run_size"] = $"{limit}",
			["stanza_guard_roots"] = "Guard",
			["stanza_assignment_targets"] = targets,
		};

		int findings = 0;

		foreach (DiagnosticDescriptor[] rules in corpus.RuleSets)
		{
			Dictionary<string, ReportDiagnostic> enabled = TestRuleSets.OnSuppressingOthersOf(
				rules,
				corpus.ShippedOn
			);

			AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(
				source,
				enabled,
				options
			);

			if (reported.Diagnostics.Length > 0)
				RoslynTestHarness.AssertEachGapIsClaimedOnce(reported);

			AnalyzerRunResult result = await RunAsync(source, rules: enabled, options, label);

			// The first set holds the most rules and contests the most gaps,
			// and a second order for each set would outlast the soak's test timeout.
			if (ReferenceEquals(rules, corpus.RuleSets[0]))
			{
				AnalyzerRunResult bottomFirst = await RoslynTestHarness.RunIterativelyAsync(
					source,

					new RunOptions(RoslynTestHarness.AllAnalyzers)
					{
						DiagnosticOptions = enabled,
						AnalyzerConfigOptions = options,
						FixesBottomFirst = true,
					}
				);

				Assert.AreEqual(
					result.FixedText,
					bottomFirst.FixedText,
					$"{label} fixing the last gap first wrote another text than fixing the first gap first."
				);
			}

			Assert.AreSequenceEqual(
				ReadTokens(source),
				ReadTokens(result.FixedText),
				$"{label} a fix changed a token."
			);

			Assert.AreSequenceEqual(
				ReadComments(source),
				ReadComments(result.FixedText),
				$"{label} a fix changed a comment."
			);

			findings += result.Diagnostics.Length;
		}

		await AssertGatheringLeavesNoWorkAsync(source, options, corpus);

		return findings;
	}

	private static async Task<int> AssertDocumentationSettlesAsync(int seed)
	{
		var random = new Random(seed);

		List<string> comments = [];
		List<string> members = [];

		for (int index = 0; index < 3; index++)
		{
			IReadOnlyList<string> body = GeneratedDocBody.Create(random, ["first", "second"]);
			comments.AddRange(body);

			List<string> lines =
			[
				.. body.Select(line => line.Length == 0 ? "\t///" : "\t/// " + line),
			];

			lines.Add($"\tinternal int Member{index}(int first, string second)");
			lines.Add("\t{");
			lines.Add("\t\treturn first;");
			lines.Add("\t}");
			members.Add(string.Join("\n", lines));
		}

		string source =
			"internal sealed class Sample\n{\n" + string.Join("\n\n", members) + "\n}\n";

		int[] widths = [60, 80, 100, 120];

		Dictionary<string, string> options = new()
		{
			["stanza_max_line_length"] = $"{widths[seed % widths.Length]}",
		};

		AnalyzerRunResult result = await RunAsync(
			source,
			rules: s_documentationRules,
			options,
			label: $"seed {seed}:"
		);

		string[] fixedLines =
		[
			.. result
				.FixedText.Split('\n')
				.Select(line => line.Trim())
				.Where(line => line.StartsWith("///", StringComparison.Ordinal))
				.Select(line => line[3..]),
		];

		Assert.AreSequenceEqual(
			[.. GeneratedDocBody.ReadWords(comments).Order(StringComparer.Ordinal)],
			[.. GeneratedDocBody.ReadWords(fixedLines).Order(StringComparer.Ordinal)],
			$"seed {seed}: a fix changed the words of a comment."
		);

		Assert.IsLessThanOrEqualTo(
			GeneratedDocBody.CountStartTags(comments),
			GeneratedDocBody.CountCapitals(fixedLines),
			$"seed {seed}: a fix capitalized more than the start of each text."
		);

		return result.Diagnostics.Length;
	}

	private static async Task<AnalyzerRunResult> RunAsync(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic> rules,
		IReadOnlyDictionary<string, string>? options,
		string label
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,
			diagnosticOptions: rules,
			analyzerConfigOptions: options
		);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: result.FixedText,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			},

			message: label
		);

		return result;
	}

	internal static string[] ReadTokens(string source)
	{
		return
		[
			.. CSharpSyntaxTree
				.ParseText(source)
				.GetRoot()
				.DescendantTokens()
				.Select(token => token.Text),
		];
	}

	// A comment keeps its words and its lines; only the spaces inside a line may change, as the marker rules change them.
	internal static string[] ReadComments(string source)
	{
		return
		[
			.. CSharpSyntaxTree
				.ParseText(source)
				.GetRoot()
				.DescendantTrivia()
				.Where(trivia =>
					trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
					|| trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
				)
				.Select(trivia =>
					LineSpace().Replace(trivia.ToString().ReplaceLineEndings("\n"), string.Empty)
				),
		];
	}

	[GeneratedRegex("[ \t]+")]
	private static partial Regex LineSpace();
}
