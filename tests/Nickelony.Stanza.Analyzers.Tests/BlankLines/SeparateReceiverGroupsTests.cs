using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateReceiverGroupsTests
{
	[TestMethod]
	public async Task PathComparisonSeparatesNestedReceiverGroups()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					item.A();
					item.B();
					item.child.A();
					item.child.B();
				}
				static dynamic Get() => null!;
			}
			""";

		const string fixedSource = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					item.A();
					item.B();

					item.child.A();
					item.child.B();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "*",
			["stanza_receiver_comparison"] = "path",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateReceiverGroups,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateReceiverGroupsAnalyzer>(
			source,
			rules,
			options
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task RootComparisonKeepsNestedPathsInOneGroup()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					item.A();
					item.B();
					item.child.A();
					item.child.B();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "*",
			["stanza_receiver_comparison"] = "root",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateReceiverGroups,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateReceiverGroupsAnalyzer>(
			source,
			rules,
			options
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task MemberWritesAndCallsShareTheirOwnerPath()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					item.name = 1;
					item.count++;
					item.Save();
					item.child.name = 2;
					item.child.count += 1;
					item.child.Save();
				}
				static dynamic Get() => null!;
			}
			""";

		const string fixedSource = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					item.name = 1;
					item.count++;
					item.Save();

					item.child.name = 2;
					item.child.count += 1;
					item.child.Save();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "*",
			["stanza_receiver_comparison"] = "path",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateReceiverGroups,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateReceiverGroupsAnalyzer>(
			source,
			rules,
			options
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SingletonReceiverGroupsDoNotBlankEveryPair()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					logger.Info();
					db.Add();
					cache.Clear();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "*",
			["stanza_receiver_comparison"] = "path",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateReceiverGroups,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateReceiverGroupsAnalyzer>(
			source,
			rules,
			options
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ARepeatedGroupOwnsItsEdgesBesideSingletons()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					logger.Info();
					db.Add();
					db.Save();
					cache.Clear();
				}
				static dynamic Get() => null!;
			}
			""";

		const string fixedSource = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					logger.Info();

					db.Add();
					db.Save();

					cache.Clear();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "*",
			["stanza_receiver_comparison"] = "path",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateReceiverGroups,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateReceiverGroupsAnalyzer>(
			source,
			rules,
			options
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EmptySelectorsLeaveReceiverGroupingInactive()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					item.A();
					item.B();
					db.A();
					db.B();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "none",
			["stanza_receiver_comparison"] = "root",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateReceiverGroups,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateReceiverGroupsAnalyzer>(
			source,
			rules,
			options
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task FactoriesIndexesAndConditionalReceiversDoNotQualify()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					Get().A();
					Get().B();
					item[0].A();
					item[0].B();
					item?.A();
					item?.B();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "*",
			["stanza_receiver_comparison"] = "path",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateReceiverGroups,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateReceiverGroupsAnalyzer>(
			source,
			rules,
			options
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ASelectedLaterSegmentDoesNotSelectEverySameRootCall()
	{
		const string source = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					item.other.A();
					item.child.A();
					item.child.B();
					item.other.B();
				}
				static dynamic Get() => null!;
			}
			""";

		const string fixedSource = """
			class C
			{
				async System.Threading.Tasks.Task M(dynamic item, dynamic db, dynamic cache, dynamic logger)
				{
					item.other.A();

					item.child.A();
					item.child.B();

					item.other.B();
				}
				static dynamic Get() => null!;
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "child",
			["stanza_receiver_comparison"] = "root",
		};

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateReceiverGroups,
		]);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateReceiverGroupsAnalyzer>(
			source,
			rules,
			options
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("this")]
	[DataRow("base")]
	public async Task TransparentWrappersAndEscapedKeywordRootsKeepTheirWrittenIdentity(
		string keyword
	)
	{
		const string source = """
			class C
			{
				dynamic db = null!;
				void M(dynamic @this)
				{
					Start();
					(this.db).First();
					db!.Second();
					Middle();
					@this.First();
					@this.Second();
					Finish();
				}
				void Start() { }
				void Middle() { }
				void Finish() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				dynamic db = null!;
				void M(dynamic @this)
				{
					Start();

					(this.db).First();
					db!.Second();

					Middle();

					@this.First();
					@this.Second();

					Finish();
				}
				void Start() { }
				void Middle() { }
				void Finish() { }
			}
			""";

		var options = new Dictionary<string, string>
		{
			["stanza_receiver_roots"] = "db, " + keyword,
			["stanza_receiver_comparison"] = "path",
		};

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateReceiverGroupsAnalyzer>(
			source.Replace("@this", "@" + keyword),
			TestRuleSets.OnSuppressingAllOthers([DiagnosticDescriptors.SeparateReceiverGroups]),
			options
		);

		RoslynTestHarness.AssertFixedText(fixedSource.Replace("@this", "@" + keyword), result);
	}

	[TestMethod]
	[CrossCutting]
	public async Task APragmaOverTheGroupsReleasesTheirGapToTheInvocationRule()
	{
		const string source = """
			class C
			{
				void M(dynamic db, dynamic cache)
				{
			#pragma warning disable ZA0010
					db.A();
					db.B();
					cache.C();
			#pragma warning restore ZA0010
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(dynamic db, dynamic cache)
				{
			#pragma warning disable ZA0010
					db.A();
					db.B();

					cache.C();
			#pragma warning restore ZA0010
				}
			}
			""";

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingAllOthers([
			DiagnosticDescriptors.SeparateReceiverGroups,
			DiagnosticDescriptors.SeparateInvocationRuns,
		]);

		var options = new Dictionary<string, string> { ["stanza_receiver_roots"] = "db,cache" };
		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules, options);

		Assert.HasCount(1, reported.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateInvocationRuns.Id, reported.Diagnostics[0].Id);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			fixedSource,
			source,

			new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			}
		);
	}
}
