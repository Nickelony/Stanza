using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Tests.Convergence;
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

			void Commented()
			{
				Helpers.DoThing();
				// The next step.
				Helpers.DoThing();

				// The last step.
				Helpers.DoThing();
			}

			void Loose(int x)
			{
				x = 1;

				x = 2;

				int count = 3;

				Helpers.DoThing();

				Helpers.DoThing();

				x = 4;

				x = 5;
			}

			int Jumps(int x)
			{
				x = 1;

				x = 2;

				x = 3;

				x = 4;

				if (x > 0)
				{
					Helpers.DoThing();
				}
				x = 5;
				x = 6;
				return x;
			}

			int CommentedJump(int x)
			{
				x = 1;
				// The next step.
				x = 2;
				return x;
			}

			int SectionJumps(int x)
			{
				switch (x)
				{
					case 1:
						x = 1;
						x = 2;
						return x;
					default:
						x = 3;
						x = 4;
						break;
				}

				return x;
			}

			void Resources(int x)
			{
				using var first = Helpers.Open();
				using var second = Helpers.Open();
				var third = x;
				var fourth = x;
				Assert.That(third);
				Assert.That(fourth);
				Helpers.DoThing();
			}

			void Alone(int x)
			{
				Helpers.DoThing();
				var lone = x;
				Assert.That(lone);
				Assert.That(x);
				Helpers.DoThing();
				Helpers.DoThing();
			}

			int Assigned(int x)
			{
				Helpers.DoThing();
				var lone = x;
				x = lone;
				x += 1;
				x++;
				Assert.That(x);
				Assert.That(lone);
				(x, lone) = Helpers.Read();
				x = 5;
				return x;
			}

			event Action Changed;
			event Action Closed;

			void Subscribed(int x)
			{
				Helpers.DoThing();
				var handler = Helpers.DoThing;
				Changed += handler;
				Closed -= handler;
				x += 1;
				x = 2;
				Changed += handler;
				Assert.That(x);
				Changed += handler;
				Closed += handler;

				Helpers.DoThing();

				Changed -= handler;

				Closed -= handler;
				return;
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

	// The corpus calls Guard, which only the key makes a throw helper.
	private static readonly Dictionary<string, string> s_options = new(StringComparer.Ordinal)
	{
		["stanza_guard_roots"] = "Guard",
	};

	// Methods that open with guards beside the statements the guard rule shares its gaps with.
	private const string GuardRepros = """
		internal sealed class GuardRepros : Stage
		{
			public override void OnStart()
			{
				if (Value == 0) return;
				base.OnStart();
				Helpers.DoThing();
				Helpers.DoThing();
				return;
			}

			public override void OnStop()
			{
				Helpers.DoThing();

				Value = 1;

				Value = 2;
				base.OnStop();
			}

			public override void Clear()
			{
				if (Value == 0) return;
				if (Value == 1) return;
				base.Clear();
			}

			public override int Fetch()
			{
				base.DoThing();
				DoThing();
				return base.Fetch();
			}

			void Guarded(int x, string text)
			{
				ArgumentNullException.ThrowIfNull(text);
				if (x == 0) return;
				if (x == 1) throw new ArgumentException("x");
				x = 1;

				x = 2;

				x = 3;
				return;
			}

			int GuardedJumps(int x)
			{
				if (x == 0) return 0;
				if (x == 1) return 1;
				return x;
			}

			void GuardedLoop(int x)
			{
				while (x > 0)
				{
					if (x == 1) continue;
					x--;
					x--;
				}
			}

			void GuardedByRoots(object text)
			{
				Guard.NotNull(text);
				Guard.Positive(1);
				Helpers.DoThing();
			}

			void GuardedAboveRuns(int x, string text)
			{
				ArgumentNullException.ThrowIfNull(text);
				var first = x;
				var second = x;
				Helpers.DoThing();
				Helpers.DoThing();
				Assert.That(first);
				Assert.That(second);
				x = 1;
				x = 2;
				return;
			}

			void GuardedMultiLine(int x)
			{
				if (x == 0)
				{
					return;
				}
				// Next.
				x = 1;
			}

			void GuardedPair(int x)
			{
				if (x == 0) return;
				x = 1;
			}
		}

		""";

	private static readonly string s_corpus =
		SourceGenerators.CreateLayoutSource(new Random(1)) + Repros;

	private static Dictionary<string, ReportDiagnostic> AllEnabled() =>
		new(TestRuleSets.AllRulesEnabled, StringComparer.Ordinal);

	// All the rules on, less the candidates whose bit is set in the mask.
	private static Dictionary<string, ReportDiagnostic> AllEnabledLeavingOut(
		DiagnosticDescriptor[] candidates,
		int off
	)
	{
		Dictionary<string, ReportDiagnostic> rules = AllEnabled();

		for (int bit = 0; bit < candidates.Length; bit++)
		{
			if ((off & (1 << bit)) != 0)
				rules[candidates[bit].Id] = ReportDiagnostic.Suppress;
		}

		return rules;
	}

	public static IEnumerable<object[]> DisabledRules { get; } =
	[
		.. TestRuleSets
			.Declared.Select(rule => rule.Descriptor.Id)
			.Order(StringComparer.Ordinal)
			.Select(id => new object[] { id }),
	];

	// Each of the run rules off or on, so a project that switches one kind of run off is a state of its own.
	public static IEnumerable<object[]> RunRuleStates { get; } =
	[.. Enumerable.Range(0, 1 << TestRuleSets.RunRules.Length).Select(off => new object[] { off })];

	// The guard rule, the jump rule, the loose run rules and the two-unit rule share the gaps around the guards.
	private static readonly DiagnosticDescriptor[] s_guardNeighbors =
	[
		DiagnosticDescriptors.SeparateGuardClauses,
		DiagnosticDescriptors.SeparateJumpStatement,
		DiagnosticDescriptors.GatherRunOfLooseStatements,
		DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
	];

	public static IEnumerable<object[]> GuardNeighborStates { get; } =
	[.. Enumerable.Range(0, 1 << s_guardNeighbors.Length).Select(off => new object[] { off })];

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
		nameof(RunRuleStates),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheCorpusSettlesWithAnySetOfRunRulesDisabled(int off)
	{
		Dictionary<string, ReportDiagnostic> rules = AllEnabledLeavingOut(
			TestRuleSets.RunRules,
			off
		);

		_ = await SettleAsync(new Configuration(Rules: rules));
	}

	[TestMethod]
	[DynamicData(
		nameof(GuardNeighborStates),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheCorpusSettlesWithAnySetOfTheGuardNeighborsDisabled(int off)
	{
		Dictionary<string, ReportDiagnostic> rules = AllEnabledLeavingOut(s_guardNeighbors, off);
		_ = await SettleAsync(new Configuration(Rules: rules, Source: GuardRepros));
	}

	[TestMethod]
	[DynamicData(
		nameof(RunRuleStates),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheGuardCorpusSettlesWithAnySetOfRunRulesDisabled(int off)
	{
		Dictionary<string, ReportDiagnostic> rules = AllEnabledLeavingOut(
			TestRuleSets.RunRules,
			off
		);

		_ = await SettleAsync(new Configuration(Rules: rules, Source: GuardRepros));
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

	// Each other rule sets its own key, so the bulk key reaches ZA0001 alone, and both Roslyn and the deferring rules read it off.
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

	// The rules that defer to ZA0001 cannot see the attribute, so the gaps they defer go unreported, but the corpus settles.
	[TestMethod]
	public async Task TheCorpusSettlesWhenAnAttributeSuppressesARuleOthersDeferTo()
	{
		string source = s_corpus.Replace(
			"internal sealed class Sample",
			"[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(\"Formatting\", \"ZA0001\")]\n\ninternal sealed class Sample",
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

	// The corpus has so many runs that, with the rules on, one fix a round needs more rounds than the default allows.
	private const int CorpusFixRounds = 100;

	// Fixes the corpus one diagnostic at a time and with a Fix All per ID in both orders, and returns the settled text.
	private static async Task<string> SettleAsync(Configuration configuration)
	{
		string source = configuration.Source ?? s_corpus;

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = configuration.Rules,
				AnalyzerConfigOptions = s_options,
				GlobalOptions = configuration.GlobalOptions,
				TreeOptions = configuration.TreeOptions,
				MaxFixRounds = CorpusFixRounds,
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
				AnalyzerConfigOptions = s_options,
				GlobalOptions = configuration.GlobalOptions,
				TreeOptions = configuration.TreeOptions,
				MaxFixRounds = CorpusFixRounds,
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
