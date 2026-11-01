using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests.Harness;

internal static partial class RoslynTestHarness
{
	/// <summary>Asserts that every state of the toggled rules settles on one text.</summary>
	// The states between all off and all on run one line ending, which keeps a large matrix affordable.
	// The inspect callback runs first in each state, for the checks that only some matrices make.
	internal static async Task AssertEveryStateSettlesAsync(
		string source,
		DiagnosticDescriptor[] toggled,
		DiagnosticDescriptor[]? alwaysOn = null,
		ImmutableArray<DiagnosticAnalyzer> analyzers = default,
		IReadOnlyDictionary<string, string>? analyzerConfigOptions = null,
		Func<MatrixState, Task>? inspect = null
	)
	{
		int states = 1 << toggled.Length;

		for (int state = 0; state < states; state++)
		{
			HarnessBudget.Restart();

			DiagnosticDescriptor[] rulesOn =
			[
				.. alwaysOn ?? [],
				.. toggled.Where((_, position) => (state & (1 << position)) != 0),
			];

			Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingOthersOf(
				rulesOn,
				toggled
			);

			string label = string.Join(",", rulesOn.Select(static rule => rule.Id));

			RunOptions run = new(analyzers.IsDefault ? AllAnalyzers : analyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = analyzerConfigOptions,
				BothEndings = state == 0 || state == states - 1,
			};

			if (inspect is not null)
			{
				await inspect(
						new MatrixState(RulesOn: rulesOn, Rules: rules, Label: label, Run: run)
					)
					.ConfigureAwait(false);
			}

			AnalyzerRunResult topFirst = await RunIterativelyAsync(source, run)
				.ConfigureAwait(false);

			AnalyzerRunResult bottomFirst = await RunIterativelyAsync(
					source,

					run with
					{
						FixesBottomFirst = true,
					}
				)
				.ConfigureAwait(false);

			Assert.AreEqual(topFirst.FixedText, bottomFirst.FixedText, label);

			await AssertFixAllPerIdSettlesAsync(
					expected: topFirst.FixedText,
					source,
					options: run,
					message: label
				)
				.ConfigureAwait(false);
		}
	}

	/// <summary>Asserts that each fix order and a Fix All per ID write the text.</summary>
	// The name labels a failure, and a run with no name labels it by the fix order alone.
	internal static async Task AssertSettlesInEveryFixOrderAsync(
		string expected,
		string source,
		RunOptions? options = null,
		string? name = null
	)
	{
		options ??= new RunOptions(AllAnalyzers);

		foreach (bool bottomFirst in new[] { false, true })
		{
			AnalyzerRunResult result = await RunIterativelyAsync(
					source,

					options with
					{
						FixesBottomFirst = bottomFirst,
					}
				)
				.ConfigureAwait(false);

			AssertFixedText(
				expected,
				result,

				name is null
					? $"Bottom first {bottomFirst}:"
					: $"{name}, bottom first {bottomFirst}:"
			);
		}

		await AssertFixAllPerIdSettlesAsync(
				expected,
				source,
				options,
				message: name is null ? null : $"{name}:"
			)
			.ConfigureAwait(false);
	}

	/// <summary>Returns the text that every fix order and a Fix All per ID agree on.</summary>
	// It asserts that they agree, with every analyzer that ships on running beside the rules.
	// The fixes meet what they meet in a project, so a mismatch is the defect.
	internal static async Task<string> AssertFixesAgreeAsync(
		string source,
		IReadOnlyDictionary<string, ReportDiagnostic> rules
	)
	{
		var options = new RunOptions(AllAnalyzers) { DiagnosticOptions = rules };

		AnalyzerRunResult topFirst = await RunIterativelyAsync(source, options)
			.ConfigureAwait(false);

		AnalyzerRunResult bottomFirst = await RunIterativelyAsync(
				source,

				options with
				{
					FixesBottomFirst = true,
				}
			)
			.ConfigureAwait(false);

		Assert.AreEqual(
			topFirst.FixedText,
			bottomFirst.FixedText,
			"Fixing the last diagnostic first wrote another text than fixing the first."
		);

		await AssertFixAllPerIdSettlesAsync(topFirst.FixedText, source, options)
			.ConfigureAwait(false);

		return topFirst.FixedText;
	}
}
