using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class KeepNarrowingAssertionWithBindingTests
{
	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_on = TestRuleSets.Only(
		DiagnosticDescriptors.KeepNarrowingAssertionWithBinding
	);

	[TestMethod]
	public async Task AnAssertionThatNarrowsTheBoundNameJoinsTheBinding()
	{
		const string source = """
			using System.Diagnostics;
			using System.Diagnostics.CodeAnalysis;

			class C
			{
				int M(int id)
				{
					string? name = Find(id);

					Debug.Assert(name != null);
					return name.Length;
				}

				int N(int id)
				{
					object? item = Find(id);

					Debug.Assert(item is string && id > 0);
					return id;
				}

				int O(int id)
				{
					string? name = Find(id);

					NotNull(name!);
					return name.Length;
				}

				int P(int id)
				{
					string? name = Find(id);

					System.ArgumentNullException.ThrowIfNull(name);
					return name.Length;
				}

				int Q(int id)
				{
					string? name = Find(id);

					Fail(name is null);
					return name.Length;
				}

				string? Find(int id) => id > 0 ? id.ToString() : null;

				static void NotNull([NotNull] object? value) => Debug.Assert(value != null);

				static void Fail([DoesNotReturnIf(true)] bool condition) => Debug.Assert(!condition);
			}
			""";

		const string fixedSource = """
			using System.Diagnostics;
			using System.Diagnostics.CodeAnalysis;

			class C
			{
				int M(int id)
				{
					string? name = Find(id);
					Debug.Assert(name != null);
					return name.Length;
				}

				int N(int id)
				{
					object? item = Find(id);
					Debug.Assert(item is string && id > 0);
					return id;
				}

				int O(int id)
				{
					string? name = Find(id);
					NotNull(name!);
					return name.Length;
				}

				int P(int id)
				{
					string? name = Find(id);
					System.ArgumentNullException.ThrowIfNull(name);
					return name.Length;
				}

				int Q(int id)
				{
					string? name = Find(id);
					Fail(name is null);
					return name.Length;
				}

				string? Find(int id) => id > 0 ? id.ToString() : null;

				static void NotNull([NotNull] object? value) => Debug.Assert(value != null);

				static void Fail([DoesNotReturnIf(true)] bool condition) => Debug.Assert(!condition);
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepNarrowingAssertionWithBindingAnalyzer>(
				source,
				s_on
			);

		Assert.HasCount(5, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnAssertionThatChecksAValueOrAnotherNameKeepsTheBlankLine()
	{
		const string source = """
			using System.Diagnostics;

			class C
			{
				int M(int id)
				{
					int total = Count(id);

					Assert.AreEqual(3, total);
					return total;
				}

				int N(int id)
				{
					string? name = Find(id);

					Debug.Assert(id > 0);
					return id;
				}

				int O(int id)
				{
					string? name = Find(id);

					Debug.Assert(name == null);
					return id;
				}

				int Count(int id) => id;

				string? Find(int id) => id > 0 ? id.ToString() : null;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepNarrowingAssertionWithBindingAnalyzer>(
				source,
				s_on
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnAssertionThatLeadsARunOfAssertionsOrHasACommentKeepsTheBlankLine()
	{
		const string source = """
			using System.Diagnostics;

			class C
			{
				void M(int id)
				{
					string? name = Find(id);

					Debug.Assert(name != null);
					Assert.AreEqual(3, name.Length);
				}

				int N(int id)
				{
					string? name = Find(id);

					Debug.Assert(name != null);
					Debug.Assert(name.Length > 0);
					return name.Length;
				}

				int O(int id)
				{
					string? name = Find(id);

					// The lookup never misses here.
					Debug.Assert(name != null);
					return name.Length;
				}

				string? Find(int id) => id > 0 ? id.ToString() : null;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepNarrowingAssertionWithBindingAnalyzer>(
				source,
				s_on
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheRuleWinsTheGapOverTheDeclarationAndAssertionRulesAndSettles()
	{
		const string source = """
			using System.Diagnostics;

			class C
			{
				int M(int id)
				{
					int first = id + 1;
					string? name = Find(first);

					Debug.Assert(name is not null);
					return name.Length;
				}

				string? Find(int id) => id > 0 ? id.ToString() : null;
			}
			""";

		const string fixedSource = """
			using System.Diagnostics;

			class C
			{
				int M(int id)
				{
					int first = id + 1;

					string? name = Find(first);
					Debug.Assert(name is not null);
					return name.Length;
				}

				string? Find(int id) => id > 0 ? id.ToString() : null;
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.KeepNarrowingAssertionWithBinding,
			DiagnosticDescriptors.SeparateDeclarationRuns,
			DiagnosticDescriptors.SeparateAssertionRuns
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source, rules);

		RoslynTestHarness.AssertFixedText(fixedSource, result);

		AnalyzerRunResult settled = await RoslynTestHarness.ReportAllAsync(fixedSource, rules);

		Assert.IsEmpty(settled.Diagnostics);
	}

	[TestMethod]
	public async Task AMultilineAssertionDoesNotJoinThroughCheckedGeometry()
	{
		const string source = """
			using System.Diagnostics;
			class C
			{
				void M()
				{
					object? value = Load();

					Debug.Assert(
						value is not null);
					Use(value);
				}
				object? Load() => null;
				void Use(object? value) { }
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.KeepNarrowingAssertionWithBinding,
			DiagnosticDescriptors.SeparateCheckedStanza,
			DiagnosticDescriptors.SeparateMultiLineConstructs
		);

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepNarrowingAssertionWithBindingAnalyzer>(
				source,
				rules
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[CrossCutting]
	public async Task APragmaOverTheAssertionReleasesItsGapToTheMultiLineRule()
	{
		const string source = """
			class C
			{
				void M(string? input)
				{
			#pragma warning disable ZA0060
					string? text = string.Concat(
						input,
						"x");
					System.Diagnostics.Debug.Assert(text != null);
					System.GC.KeepAlive(text);
			#pragma warning restore ZA0060
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(string? input)
				{
			#pragma warning disable ZA0060
					string? text = string.Concat(
						input,
						"x");

					System.Diagnostics.Debug.Assert(text != null);
					System.GC.KeepAlive(text);
			#pragma warning restore ZA0060
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.KeepNarrowingAssertionWithBinding,
			DiagnosticDescriptors.SeparateMultiLineConstructs,
		]);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			fixedSource,
			source,
			new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}
}
