using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Fixes one corpus under many rule sets until nothing changes.</summary>
[TestClass]
public sealed class ConfigurationMatrixTests
{
	private const int RandomMapCount = 20;

	// Shapes where the rules can undo each other or break code under one rule set and not another.
	private const string Repros = """
		internal sealed class Repros
		{
			void Standalone()
			{
				Helpers.DoThing();
				Helpers.DoThing();
				{
					Helpers.DoThing();
				}
			}

			void Trailing(int x)
			{
				Helpers.DoThing(); /* note
				more */ if (x > 0)
				{
					Helpers.DoThing();
				}
			}

			void Flat(IDisposable a, IDisposable b)
			{
				using (a)
				using (b)
				Helpers.Call(
					1, 2);
			}

			void Directive(bool a, bool b)
			{
				if (a)
				{
					if (b) Helpers.DoThing();
				}
		#if DEBUG
				else
					Helpers.DoThing();
		#endif
			}

			/// <summary>
			///
			/// Binds the options.
			///
			/// </summary>
			void Documented()
			{
				Helpers.DoThing();
			}
		}

		""";

	private static readonly string s_corpus =
		ConvergenceTests.CreateLayoutSource(new Random(1)) + Repros;

	private static Dictionary<string, ReportDiagnostic> AllEnabled() =>
		new(TestRuleSets.AllRulesEnabled, StringComparer.Ordinal);

	public static IEnumerable<object[]> DisabledRules { get; } =
	[
		.. TestRuleSets
			.Declared.Select(rule => rule.Descriptor.Id)
			.Order(StringComparer.Ordinal)
			.Select(id => new object[] { id }),
	];

	public static IEnumerable<object[]> Seeds { get; } =
	[.. Enumerable.Range(1, RandomMapCount).Select(seed => new object[] { seed })];

	// The rules that share a gap, pair or deferral, which a disabled rule can change the settled text through.
	private static readonly HashSet<string> s_sharingRules = FindSharingRules();

	private static HashSet<string> FindSharingRules()
	{
		HashSet<string> ids = [];

		foreach (StanzaRule rule in StanzaRules.All)
		{
			if (rule.Pair is null && rule.DefersTo.IsDefaultOrEmpty)
				continue;

			_ = ids.Add(rule.Descriptor.Id);

			foreach (DiagnosticDescriptor target in rule.DefersTo.IsDefault ? [] : rule.DefersTo)
				_ = ids.Add(target.Id);

			if (rule.Pair is { } pair)
			{
				_ = ids.Add(pair.First.Id);
				_ = ids.Add(pair.Second.Id);
			}
		}

		return ids;
	}

	// A rule that never reports on the corpus and takes no part in a pair or a deferral leaves the settled text as it was
	// when it is disabled, so its row settles nothing and checks that the other rules report as they did.
	// The rows are the declared rules, and the corpus decides at run time which of them report, so discovery runs no analysis.
	[TestMethod]
	[DynamicData(
		nameof(DisabledRules),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheCorpusSettlesWithOneRuleDisabled(string id)
	{
		Dictionary<string, ReportDiagnostic> rules = new(AllEnabled())
		{
			[id] = ReportDiagnostic.Suppress,
		};

		if (!s_sharingRules.Contains(id))
		{
			AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(
				s_corpus,
				AllEnabled()
			);

			if (!reported.Diagnostics.Any(diagnostic => diagnostic.Id == id))
			{
				AnalyzerRunResult without = await RoslynTestHarness.ReportAllAsync(s_corpus, rules);

				Assert.AreEqual(
					DescribeReports(reported),
					DescribeReports(without),
					$"{id} reports nothing here and shares no gap, yet disabling it changed what the other rules report."
				);

				return;
			}
		}

		_ = await SettleAsync(new Configuration(Rules: rules));
	}

	private static string DescribeReports(AnalyzerRunResult result)
	{
		return string.Join(
			"\n",

			result
				.Diagnostics.Select(diagnostic =>
					$"{diagnostic.Id} {diagnostic.Location.GetLineSpan().Span}"
				)
				.Order(StringComparer.Ordinal)
		);
	}

	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheCorpusSettlesUnderARandomSeverityMap(int seed)
	{
		var random = new Random(seed);

		var rules = TestRuleSets.Declared.ToDictionary(
			rule => rule.Descriptor.Id,
			_ => random.Next(2) == 0 ? ReportDiagnostic.Suppress : ReportDiagnostic.Warn,
			StringComparer.Ordinal
		);

		_ = await SettleAsync(new Configuration(Rules: rules));
	}

	[TestMethod]
	public async Task TheCorpusSettlesWhenAGlobalConfigEnablesEveryRule() =>
		_ = await SettleAsync(new Configuration(GlobalOptions: AllEnabled()));

	// Each other rule sets its own key, so the bulk key reaches ZA0005 alone, and both Roslyn and the deferring rules read it off.
	[TestMethod]
	public async Task ABulkSeverityKeyTurnsARuleOffAsItsOwnKeyDoes()
	{
		DiagnosticDescriptor multiLine = DiagnosticDescriptors.SeparateMultiLineConstructs;

		Dictionary<string, ReportDiagnostic> others = AllEnabled();
		others.Remove(multiLine.Id);

		string bulkKey = $"dotnet_analyzer_diagnostic.category-{multiLine.Category}.severity";

		Dictionary<string, ReportDiagnostic> bulk = new(others)
		{
			[bulkKey] = ReportDiagnostic.Suppress,
		};

		Dictionary<string, ReportDiagnostic> own = new(others)
		{
			[multiLine.Id] = ReportDiagnostic.Suppress,
		};

		string fromOwn = await SettleAsync(new Configuration(TreeOptions: own));
		string fromBulk = await SettleAsync(new Configuration(TreeOptions: bulk));

		AssertSameText(fromOwn, fromBulk, nameof(ABulkSeverityKeyTurnsARuleOffAsItsOwnKeyDoes));
	}

	// <NoWarn> disables a rule for the compilation, and Roslyn lets that beat a severity line in .editorconfig.
	[TestMethod]
	public async Task ANoWarnEntryBeatsAnEditorConfigSeverity()
	{
		string id = DiagnosticDescriptors.SeparateMultiLineConstructs.Id;

		Dictionary<string, ReportDiagnostic> rules = new(AllEnabled())
		{
			[id] = ReportDiagnostic.Suppress,
		};

		string noWarn = await SettleAsync(new Configuration(Rules: rules));

		string noWarnAndSeverity = await SettleAsync(
			new Configuration(
				Rules: rules,

				TreeOptions: new Dictionary<string, ReportDiagnostic>
				{
					[id] = ReportDiagnostic.Warn,
				}
			)
		);

		AssertSameText(noWarn, noWarnAndSeverity, nameof(ANoWarnEntryBeatsAnEditorConfigSeverity));
	}

	// The rules that defer to ZA0005 cannot see the attribute, so the gaps they defer go unreported, but the corpus settles.
	[TestMethod]
	public async Task TheCorpusSettlesWhenAnAttributeSuppressesARuleOthersDeferTo()
	{
		string source = s_corpus.Replace(
			"internal sealed class Sample",
			"[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(\"Formatting\", \"ZA0005\")]\n\ninternal sealed class Sample",
			StringComparison.Ordinal
		);

		Assert.AreNotEqual(
			s_corpus,
			source,
			"The corpus holds no class to put the attribute above."
		);

		_ = await SettleAsync(new Configuration(Rules: AllEnabled(), Source: source));
	}

	[TestMethod]
	public async Task ASeverityOfDefaultFixesAsNoSeverityLineDoes()
	{
		var defaults = TestRuleSets.Declared.ToDictionary(
			rule => rule.Descriptor.Id,
			_ => ReportDiagnostic.Default,
			StringComparer.Ordinal
		);

		string unset = await SettleAsync(new Configuration());
		string set = await SettleAsync(new Configuration(Rules: defaults));

		AssertSameText(unset, set, nameof(ASeverityOfDefaultFixesAsNoSeverityLineDoes));
	}

	// Fixes the corpus one diagnostic at a time and with a Fix All per ID in both orders, and returns the settled text.
	private static async Task<string> SettleAsync(Configuration configuration)
	{
		string source = configuration.Source ?? s_corpus;

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = configuration.Rules,
				GlobalOptions = configuration.GlobalOptions,
				TreeOptions = configuration.TreeOptions,
			}
		);

		Assert.AreSequenceEqual(
			ConvergenceTests.ReadComments(source),
			ConvergenceTests.ReadComments(result.FixedText),
			"a fix changed the text of a comment."
		);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: result.FixedText,
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = configuration.Rules,
				GlobalOptions = configuration.GlobalOptions,
				TreeOptions = configuration.TreeOptions,
			}
		);

		return result.FixedText;
	}

	private static void AssertSameText(string expected, string actual, string row)
	{
		Assert.AreEqual(
			expected,
			actual,
			$"{row}: the two configurations settle on different text."
		);
	}

	private sealed record Configuration(
		IReadOnlyDictionary<string, ReportDiagnostic>? Rules = null,
		IReadOnlyDictionary<string, ReportDiagnostic>? GlobalOptions = null,
		IReadOnlyDictionary<string, ReportDiagnostic>? TreeOptions = null,
		string? Source = null
	);
}
