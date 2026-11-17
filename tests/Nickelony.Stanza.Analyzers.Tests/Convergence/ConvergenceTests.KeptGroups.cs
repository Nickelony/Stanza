using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Tests.Guards;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Convergence;

public sealed partial class ConvergenceTests
{
	// The gap rules of a level, with the named rules on beside them and every other rule suppressed.
	private static Dictionary<string, ReportDiagnostic> AtLevelWith(
		string level,
		params DiagnosticDescriptor[] rules
	)
	{
		HashSet<string> enabled =
		[
			.. PresetFiles
				.ReadTable()
				.ReadEnabled(level)
				.Where(id => string.CompareOrdinal(id, "ZA0001") >= 0 && string.CompareOrdinal(id, "ZA0051") <= 0),

			.. rules.Select(rule => rule.Id),
		];

		return TestRuleSets.OnSuppressingAllOthers([
			.. TestRuleSets.Declared.Select(rule => rule.Descriptor).Where(rule => enabled.Contains(rule.Id)),
		]);
	}

	// Each row is a body where a keeper and the rules beside it meet at a gap,
	// with the text every fix order writes, and each gap of the body claimed by one rule.
	public static IEnumerable<object[]> KeptGroupRows { get; } =
	[
		[
			"a multi-line boundary moved above a kept pair is no loose gap for the gather rule",
			"strong",
			"ZA0058",
			"stanza_max_loose_run_size=1",
			"Audit();\nstring o = Find(id);\nif (o == null)\n{\n\tthrow new InvalidOperationException();\n}\n\nreturn o;",
			"Audit();\n\nstring o = Find(id);\nif (o == null)\n{\n\tthrow new InvalidOperationException();\n}\n\nreturn o;",
		],

		[
			"a multi-line boundary moved above a kept pair ends the declaration run there",
			"strong",
			"ZA0058",
			"",
			"Audit();\nvar name = id.ToString();\nvar order = Find(id);\n\nif (order == null)\n{\n\treturn null;\n}\n\nreturn order + name;",
			"Audit();\nvar name = id.ToString();\n\nvar order = Find(id);\nif (order == null)\n{\n\treturn null;\n}\n\nreturn order + name;",
		],

		[
			"the declaration rule reads a dependent gap as joined before the keeper removes its blank line",
			"strong",
			"ZA0002,ZA0061",
			"",
			"var a = Find(id);\nstring b = a + text;\nstring c = Parse(text);\n\nstring d = c + text;\nreturn d;",
			"var a = Find(id);\nstring b = a + text;\nstring c = Parse(text);\nstring d = c + text;\n\nreturn d;",
		],

		[
			"the jump rule reads the stanza above a jump with the dependent gaps joined",
			"strong",
			"ZA0013,ZA0061",
			"",
			"var v = Find(id);\n\nvar w = Parse(v);\n\nvar x = Parse(w);\nif (w == null) return null;\nreturn w;",
			"var v = Find(id);\nvar w = Parse(v);\nvar x = Parse(w);\n\nif (w == null) return null;\nreturn w;",
		],

		[
			"the assignment rule reads a receiver group as joined before the keeper removes its blank line",
			"strong",
			"ZA0009,ZA0064",
			"stanza_receiver_roots=db",
			"_total = id;\ndb.Count = 1;\n\ndb.Count = 2;\nreturn text;",
			"_total = id;\ndb.Count = 1;\ndb.Count = 2;\n\nreturn text;",
		],

		[
			"the dependent bindings keeper writes no blank line inside a checked stanza",
			"strong",
			"ZA0012,ZA0061",
			"",
			"Audit();\nvar a = Parse(text);\n\nvar b = a.Trim();\nvar order = Find(id);\nif (order == null) return null;\nAudit();\nreturn order + b;",
			"Audit();\n\nvar a = Parse(text);\nvar b = a.Trim();\n\nvar order = Find(id);\nif (order == null) return null;\n\nAudit();\nreturn order + b;",
		],

		[
			"a stanza that ends where the next starts asks for their shared gap once",
			"strong",
			"ZA0012",
			"",
			"Audit();\nArgumentNullException.ThrowIfNull(text);\nvar s = Find(id);\nDebug.Assert(s != null);\nAudit();\nreturn s;",
			"Audit();\n\nArgumentNullException.ThrowIfNull(text);\n\nvar s = Find(id);\nDebug.Assert(s != null);\nAudit();\n\nreturn s;",
		],

		[
			"a body that opens with a throw helper asks for the gap below it once",
			"moderate",
			"ZA0012",
			"",
			"ArgumentNullException.ThrowIfNull(text);\nvar s = Find(id);\nDebug.Assert(s != null);\nAudit();\nreturn s;",
			"ArgumentNullException.ThrowIfNull(text);\n\nvar s = Find(id);\nDebug.Assert(s != null);\nAudit();\nreturn s;",
		],

		[
			"a jump below a guard chain the chain keeper joins asks for its blank line",
			"strong",
			"ZA0057",
			"stanza_guard_roots=Guard",
			"var v = Find(id);\nif (v == null)\n{\n\treturn null;\n}\nGuard.NotNull(v);\nreturn v;",
			"var v = Find(id);\n\nif (v == null)\n{\n\treturn null;\n}\nGuard.NotNull(v);\n\nreturn v;",
		],

		[
			"the checked stanza rule owns the gap above a kept group that the receiver rule asks for too",
			"minimal",
			"ZA0012,ZA0010,ZA0061",
			"stanza_receiver_roots=*",
			"var svc = new Svc();\nsvc.Write(id);\nsvc.Close();\nvar a = Find(id);\nstring b = a + text;\nif (b == null)\n{\n\treturn null;\n}\nreturn b;",
			"var svc = new Svc();\n\nsvc.Write(id);\nsvc.Close();\n\nvar a = Find(id);\nstring b = a + text;\nif (b == null)\n{\n\treturn null;\n}\n\nreturn b;",
		],

		[
			"a declaration a keeper joins to the unit above it is no lone one, so its group gets no blank line above it",
			"moderate",
			"ZA0061",
			"",
			"Audit();\nvar a = Find(id);\nstring b = a + text;\nvar c = Parse(text);\nvar d = Parse(text);\nreturn d;",
			"Audit();\nvar a = Find(id);\nstring b = a + text;\n\nvar c = Parse(text);\nvar d = Parse(text);\n\nreturn d;",
		],

		[
			"a lone declaration above multi-line seeds is set apart at the moderate level whichever blank line is written first",
			"moderate",
			"ZA0063",
			"",
			"string v6 = id + text;\nvar v7 = Find(id);\nint n = 0;\nint m = 1;\nforeach (char item in v7)\n{\n\tn += item;\n\tm += n;\n}\nreturn null;",
			"string v6 = id + text;\n\nvar v7 = Find(id);\n\nint n = 0;\nint m = 1;\nforeach (char item in v7)\n{\n\tn += item;\n\tm += n;\n}\n\nreturn null;",
		],

		[
			"a lone declaration above multi-line seeds is set apart at the strong level whichever blank line is written first",
			"strong",
			"ZA0063",
			"",
			"string v6 = id + text;\nvar v7 = Find(id);\nint n = 0;\nint m = 1;\nforeach (char item in v7)\n{\n\tn += item;\n\tm += n;\n}\nreturn null;",
			"string v6 = id + text;\n\nvar v7 = Find(id);\n\nint n = 0;\nint m = 1;\nforeach (char item in v7)\n{\n\tn += item;\n\tm += n;\n}\n\nreturn null;",
		],

		[
			"a lone declaration above a guarded dependent group is set apart whichever blank line is written first",
			"strong",
			"ZA0058,ZA0061",
			"",
			"Audit();\nstring v7 = id + text;\nvar a = Find(id);\nvar b = Parse(a);\nif (b == null)\n{\n\treturn null;\n}\nreturn v7;",
			"Audit();\n\nstring v7 = id + text;\n\nvar a = Find(id);\nvar b = Parse(a);\nif (b == null)\n{\n\treturn null;\n}\n\nreturn v7;",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(KeptGroupRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AKeptGroupAndTheRulesBesideItClaimEachGapOnceAndSettleInEveryFixOrder(
		string name,
		string level,
		string ids,
		string keys,
		string body,
		string fixedBody
	)
	{
		string source = SourceGenerators.WrapKeptGroupBody(body);
		string fixedSource = SourceGenerators.WrapKeptGroupBody(fixedBody);

		var options = new RunOptions(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = AtLevelWith(
				level,
				[.. ids.Split(',').Select(id => TestRuleSets.Declared.Single(rule => rule.Descriptor.Id == id).Descriptor)]
			),

			AnalyzerConfigOptions = keys.Length == 0
				? null
				: keys.Split(';').Select(pair => pair.Split('=')).ToDictionary(keySelector: pair => pair[0], elementSelector: pair => pair[1]),
		};

		RoslynTestHarness.AssertEachGapIsClaimedOnce(await RoslynTestHarness.ReportAsync(source, options));
		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(expected: fixedSource, source, options, name);
	}

	// The example of the ZA0012 page, which the rule writes alone at either level that holds the rules beside it.
	[TestMethod]
	[DataRow("moderate")]
	[DataRow("strong")]
	public async Task TheCheckedStanzaPageExampleIsWhatTheLevelWrites(string level)
	{
		const string source = """
			class Customer { }

			class Repository
			{
				public Customer Find(int id) => null;
			}

			class C
			{
				Repository repository = new();

				void M(int id)
				{
					Audit(id);
					var customer = repository.Find(id);
					if (customer is null) return;
					Show(customer);
				}

				void Audit(int id) { }
				void Show(Customer customer) { }
			}
			""";

		const string fixedSource = """
			class Customer { }

			class Repository
			{
				public Customer Find(int id) => null;
			}

			class C
			{
				Repository repository = new();

				void M(int id)
				{
					Audit(id);

					var customer = repository.Find(id);
					if (customer is null) return;

					Show(customer);
				}

				void Audit(int id) { }
				void Show(Customer customer) { }
			}
			""";

		var options = new RunOptions(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = AtLevelWith(level, DiagnosticDescriptors.SeparateCheckedStanza),
		};

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(expected: fixedSource, source, options, name: level);
	}

	// The rules that keep groups together or separate stanzas, which a generated case enables a random subset of.
	private static readonly DiagnosticDescriptor[] s_keptGroupRules =
	[
		DiagnosticDescriptors.SeparateCheckedStanza,
		DiagnosticDescriptors.SeparateReceiverGroups,
		DiagnosticDescriptors.KeepGuardClausesTogether,
		DiagnosticDescriptors.KeepGuardWithBinding,
		DiagnosticDescriptors.KeepAccumulatorWithLoop,
		DiagnosticDescriptors.KeepJumpWithBinding,
		DiagnosticDescriptors.KeepNarrowingAssertionWithBinding,
		DiagnosticDescriptors.KeepInitializationWithCalls,
		DiagnosticDescriptors.KeepReceiverGroupTogether,
		DiagnosticDescriptors.KeepDependentBindingsTogether,
	];

	// Reads the level, options, kept-group rules and body a seed makes.
	private static (string Label, string Source, RunOptions Options) ReadKeptGroupCase(int seed)
	{
		var random = new Random(seed);
		string level = random.Next(2) == 0 ? "moderate" : "strong";

		DiagnosticDescriptor[] named = [.. s_keptGroupRules.Where(_ => random.Next(2) == 0)];
		Dictionary<string, string> keys = new() { ["stanza_guard_roots"] = "Guard" };
		string root = new[] { string.Empty, "db", "*" }[random.Next(3)];

		if (root.Length > 0)
			keys["stanza_receiver_roots"] = root;

		int limit = random.Next(4);

		if (limit > 0)
			keys["stanza_max_loose_run_size"] = limit.ToString(CultureInfo.InvariantCulture);

		string source = SourceGenerators.WrapKeptGroupBody(SourceGenerators.CreateKeptGroupBody(random));
		Dictionary<string, ReportDiagnostic> rules = AtLevelWith(level, named);

		string label =
			$"seed {seed}, {level} with {string.Join(",", named.Select(rule => rule.Id))}, "
			+ $"{string.Join(",", keys.Select(pair => $"{pair.Key}={pair.Value}"))}:";

		var options = new RunOptions(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = rules,
			AnalyzerConfigOptions = keys,
			BothEndings = false,
		};

		return (label, source, options);
	}

	// A generated body under random levels, options and kept-group rules claims each gap once and settles on one text in every fix order.
	private static async Task<int> AssertKeptGroupsSettleAsync(int seed)
	{
		(string label, string source, RunOptions options) = ReadKeptGroupCase(seed);
		AnalyzerRunResult report = await RoslynTestHarness.ReportAsync(source, options);

		if (report.Diagnostics.IsEmpty)
			return 0;

		try
		{
			RoslynTestHarness.AssertEachGapIsClaimedOnce(report);
			AnalyzerRunResult topFirst = await RoslynTestHarness.RunIterativelyAsync(source, options);

			await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(expected: topFirst.FixedText, source, options);
		}
		catch (AssertFailedException exception)
		{
			throw new AssertFailedException($"{label} {exception.Message}\n{source}", exception);
		}

		return report.Diagnostics.Length;
	}

	[TestMethod]
	[DynamicData(
		nameof(Seeds),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task KeptGroupsSettleTheSameWayInEveryFixOrder(int seed)
	{
		int findings = 0;

		// Each seed tries several bodies, since a body the rules leave alone proves nothing.
		for (int trial = 0; trial < 8; trial++)
			findings += await AssertKeptGroupsSettleAsync((seed * 1000) + trial);

		Assert.IsGreaterThan(0, findings, "The generated bodies needed no fix, so they prove nothing.");
	}

	[TestMethod]
	[TestCategory("Soak")]
	[Timeout(SoakRun.NoRunnerTimeout)]
	public Task ALargeSeededSetOfKeptGroupsSettlesTheSameWay() =>
		RunSoakAsync([AssertKeptGroupsSettleAsync]);

	// A block of bindings, guards, assertions and receiver calls with some blank lines, repeated to the size a cost test compares.
	private static string BuildLargeKeptGroupBody(int groups)
	{
		var random = new Random(7);
		StringBuilder body = new();

		for (int index = 0; index < groups; index++)
		{
			string[] lines =
			[
				$"var a{index} = Find(id);",
				$"string b{index} = a{index} + text;",
				"db.Count = 1;",
				"db.Write(id);",
				"Audit();",
				$"if (b{index} == null) return null;",
				$"_total = b{index};",
				$"var s{index} = new Svc();",
				$"s{index}.Write(id);",
				$"s{index}.Close();",
			];

			foreach (string line in lines)
			{
				body.Append(line).Append('\n');

				if (random.Next(3) == 0)
					body.Append('\n');
			}
		}

		return body.Append("return text;").ToString();
	}

	public static IEnumerable<object[]> LargeBlockRuleSets { get; } =
	[
		["the strong level", Array.Empty<DiagnosticDescriptor>(), 400],
		["the strong level and the kept-group rules", s_keptGroupRules, 50],
	];

	// Selected receivers make each statement ask whether it shares a line with a neighbor, and the kept groups read the whole block,
	// so a read per statement or per gap that scanned the block would make one large block cost its square.
	[TestMethod]
	[DoNotParallelize]
	[DynamicData(
		nameof(LargeBlockRuleSets),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task OneLargeBlockWithSelectedReceiversCostsInStepWithItsSize(
		string name,
		DiagnosticDescriptor[] rules,
		int groups
	)
	{
		var options = new RunOptions(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = AtLevelWith("strong", rules),
			AnalyzerConfigOptions = new Dictionary<string, string> { ["stanza_receiver_roots"] = "*" },
			BothEndings = false,
		};

		await LinearCost.AssertAnalysisLinearAsync(
			buildSource: count => SourceGenerators.WrapKeptGroupBody(BuildLargeKeptGroupBody(count)),
			units: groups,
			options,
			message: name
		);
	}
}
