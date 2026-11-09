using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Convergence;

public sealed partial class ConvergenceTests
{
	[TestMethod]
	[DataRow(
		"members",
		"class C\n{\n\tpublic void A()\n\t{\n\t}\n\tpublic void B() { }\n\tprivate int _k;\n\tpublic void M1() { }\n\tpublic void M2()\n\t{\n\t}\n}\n"
	)]
	[DataRow(
		"statements",
		"class C\n{\n\tvoid M(bool x)\n\t{\n\t\tif (x)\n\t\t{\n\t\t}\n\t\tFoo();\n\t\tvar k = 1;\n\t\tBar(k);\n\t\tBar(\n\t\t\tk);\n\t}\n\tvoid Foo() { }\n\tvoid Bar(int v) { }\n}\n"
	)]
	public async Task AGroupBesideAMultiLineUnitSettlesTheSameWayInEitherFixOrder(
		string name,
		string source
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(source);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: result.FixedText,
			source,
			message: $"{name}:"
		);
	}

	// A blank line that another rule has yet to write, or a documentation comment a blank line sets apart from its unit,
	// must not change what a rule decides, so each row settles on one text in every fix order.
	public static IEnumerable<object[]> FixOrderRows { get; } =
	[
		[
			"a declaration run above a multi-line statement keeps its blank line before ZA0001 writes the one below it",
			TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements),

			"""
				class C
				{
					void M()
					{
						loose = 1;

						loose = 2;

						loose = 3;

						string text = "";

						int number = 3;
						Call(
							1,
							2);
					}

					int loose;

					void Call(int first, int second) { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						loose = 1;

						loose = 2;

						loose = 3;

						string text = "";

						int number = 3;

						Call(
							1,
							2);
					}

					int loose;

					void Call(int first, int second) { }
				}
				""",
		],

		[
			"a gathered run beside the declaration run above a multi-line statement is gathered whichever rule fixes first",
			TestRuleSets.Only(DiagnosticDescriptors.GatherRunOfLooseStatements),

			"""
				class C
				{
					void M()
					{
						loose = 1;

						loose = 2;

						loose = 3;

						loose = 4;

						int count = 2;

						int number = 3;
						Call(
							1,
							2);
					}

					int loose;

					void Call(int first, int second) { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						loose = 1;
						loose = 2;
						loose = 3;
						loose = 4;

						int count = 2;

						int number = 3;

						Call(
							1,
							2);
					}

					int loose;

					void Call(int first, int second) { }
				}
				""",
		],

		[
			"a run of calls beside a region marker keeps its blank lines before ZA0401 writes the ones around the marker",

			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseStatements,
				DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors
			),

			"""
				class C
				{
					void M()
					{
				#region Marked
						loose = 4;
				#endregion
						DoThing();

						DoThing();

						Build(7);

						loose = 1;

						loose = 2;
					}

					int loose;

					void DoThing() { }

					int Build(int value) => value;
				}
				""",

			"""
				class C
				{
					void M()
					{
				#region Marked

						loose = 4;

				#endregion

						DoThing();

						DoThing();

						Build(7);

						loose = 1;

						loose = 2;
					}

					int loose;

					void DoThing() { }

					int Build(int value) => value;
				}
				""",
		],

		[
			"a declaration run beside a conditional block keeps its gaps before ZA0402 writes the ones around the block",
			TestRuleSets.Only(DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors),

			"""
				#define TRACE
				class C
				{
					void M()
					{
						DoThing();
						DoThing();
						int first = 1;
						int second = 2;
				#if TRACE
						DoThing();
				#endif
						DoThing();
					}
				}
				""",

			"""
				#define TRACE
				class C
				{
					void M()
					{
						DoThing();
						DoThing();

						int first = 1;
						int second = 2;

				#if TRACE
						DoThing();
				#endif

						DoThing();
					}
				}
				""",
		],

		[
			"a documentation comment set apart from its field by a blank line still ends the declaration run",
			DocumentedCommentedSiblingRuleSet(),

			"""
				class C
				{
					int first;
					/// <summary>The second field.</summary>

					int second;
					void Run() { }
				}
				""",

			"""
				class C
				{
					int first;

					/// <summary>The second field.</summary>
					int second;
					void Run() { }
				}
				""",
		],

		[
			"a documentation comment set apart from its field by a blank line still ends the modifier run",

			DocumentedCommentedSiblingRuleSet(
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAssertionRuns,
				DiagnosticDescriptors.SeparateMemberGroups
			),

			"""
				class C
				{
					static int first;
					/// <summary>The second field.</summary>

					static int second;
					void Run() { }
				}
				""",

			"""
				class C
				{
					static int first;

					/// <summary>The second field.</summary>
					static int second;
					void Run() { }
				}
				""",
		],

		[
			"a commented sibling starts a new group, so the declaration it splits off stays with the call below it",
			TestRuleSets.Only(DiagnosticDescriptors.SeparateCommentedSibling),

			"""
				class C
				{
					void M()
					{
						int a = 1;
						// c
						int b = 2;
						Build(1);
					}

					int Build(int value) => value;
				}
				""",

			"""
				class C
				{
					void M()
					{
						int a = 1;

						// c
						int b = 2;
						Build(1);
					}

					int Build(int value) => value;
				}
				""",
		],

		[
			"a field run gathered above a method run is gathered whichever rule writes the blank line below it",

			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseMembers,
				DiagnosticDescriptors.SeparateMemberGroups
			),

			"""
				class C
				{
					int first;

					int second;

					int third;

					int fourth;
					void Run() { }
					void Other() { }
				}
				""",

			"""
				class C
				{
					int first;
					int second;
					int third;
					int fourth;

					void Run() { }
					void Other() { }
				}
				""",
		],

		[
			"a field run gathered above a run of another modifier set is gathered whichever rule writes the blank line below it",

			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseMembers,
				DiagnosticDescriptors.SeparateModifierGroups
			),

			"""
				class C
				{
					int first;

					int second;

					int third;

					int fourth;
					static int fifth;
					static int sixth;
				}
				""",

			"""
				class C
				{
					int first;
					int second;
					int third;
					int fourth;

					static int fifth;
					static int sixth;
				}
				""",
		],

		[
			"a field run gathered above a run of another type is gathered whichever rule writes the blank line below it",

			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseMembers,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			),

			"""
				class C
				{
					int first;

					int second;

					int third;

					int fourth;
					string fifth;
					string sixth;
				}
				""",

			"""
				class C
				{
					int first;
					int second;
					int third;
					int fourth;

					string fifth;
					string sixth;
				}
				""",
		],

		[
			"a field run gathered above a commented field is gathered whichever rule writes the blank line above the comment",

			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseMembers,
				DiagnosticDescriptors.SeparateCommentedSibling
			),

			"""
				class C
				{
					int first;

					int second;

					int third;

					int fourth;
					// The fifth field.
					int fifth;
				}
				""",

			"""
				class C
				{
					int first;
					int second;
					int third;
					int fourth;

					// The fifth field.
					int fifth;
				}
				""",
		],

		[
			"a conditional block among the members leaves their blank lines while ZA0402 writes the ones around it",

			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseMembers,
				DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
			),

			"""
				class C
				{
					int first;

					int second;

					int third;

					int fourth;
				#if DEBUG
					int fifth;
				#endif
				}
				""",

			"""
				class C
				{
					int first;

					int second;

					int third;

					int fourth;

				#if DEBUG
					int fifth;
				#endif
				}
				""",
		],

		[
			"the gap below the imports is written by ZA0015 before the members below it are gathered",

			TestRuleSets.Only(
				DiagnosticDescriptors.GatherRunOfLooseMembers,
				DiagnosticDescriptors.SeparateImportSection
			),

			"""
				using System;
				delegate void First();

				delegate void Second();

				delegate void Third();

				delegate void Fourth();
				""",

			"""
				using System;

				delegate void First();
				delegate void Second();
				delegate void Third();
				delegate void Fourth();
				""",
		],

		[
			"an assignment above a subscription run keeps its tight gap whichever of ZA0008 and ZA0007 fixes first",

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateEventSubscriptionRuns,
				DiagnosticDescriptors.SeparateAssignmentRuns
			),

			"""
				class C
				{
					event System.Action Changed;
					event System.Action Closed;

					int first;

					void M()
					{
						DoThing();
						first = 1;
						Changed += DoThing;
						Closed += DoThing;
						DoThing();
					}

					void DoThing() { }
				}
				""",

			"""
				class C
				{
					event System.Action Changed;
					event System.Action Closed;

					int first;

					void M()
					{
						DoThing();
						first = 1;

						Changed += DoThing;
						Closed += DoThing;

						DoThing();
					}

					void DoThing() { }
				}
				""",
		],

		[
			"a same-type run that the modifier rule splits is split there whichever of ZA0014 and ZA0003 fixes first",

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateModifierGroups,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			),

			"""
				class C
				{
					static readonly int s_static4 = 4;
					int _field5;
					string _text6 = "6";
				}
				""",

			"""
				class C
				{
					static readonly int s_static4 = 4;

					int _field5;
					string _text6 = "6";
				}
				""",
		],

		[
			"a same-type run that the modifier rule splits stays split by the blank line a later group asks for",

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateModifierGroups,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			),

			"""
				class C
				{
					static readonly int s_static4 = 4;
					int _field5;
					int _field6;
					string _text7 = "7";
					string _text8 = "8";
				}
				""",

			"""
				class C
				{
					static readonly int s_static4 = 4;

					int _field5;
					int _field6;

					string _text7 = "7";
					string _text8 = "8";
				}
				""",
		],

		[
			"a modifier run that a later group splits to one field keeps the blank line above it",

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateModifierGroups,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
			),

			"""
				class C
				{
					private static int _a;
					private int _b;
					private string _c;
					private string _d;
				}
				""",

			"""
				class C
				{
					private static int _a;

					private int _b;

					private string _c;
					private string _d;
				}
				""",
		],

		[
			"a field run that the modifier rule splits keeps the blank line above its first field",

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateMemberGroups,
				DiagnosticDescriptors.SeparateModifierGroups
			),

			"""
				class C
				{
					public void M0() { }
					private static int s_1;
					private int _f2;
					private int _f3;
				}
				""",

			"""
				class C
				{
					public void M0() { }

					private static int s_1;

					private int _f2;
					private int _f3;
				}
				""",
		],

		[
			"a declaration run that a type splits to one local keeps the blank line below that local",

			OnSuppressingShippedStatementRules([
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateSameTypeDeclarationRuns,
			]),

			"""
				class C
				{
					void M()
					{
						int b0 = 2;
						int b1 = 2;
						string c2 = "x";
						M();
					}
				}
				""",

			"""
				class C
				{
					void M()
					{
						int b0 = 2;
						int b1 = 2;

						string c2 = "x";
						M();
					}
				}
				""",
		],

		[
			"declarations beside await runs settle whichever of ZA0002 and ZA0006 fixes first",

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateDeclarationRuns,
				DiagnosticDescriptors.SeparateAwaitRuns
			),

			"""
				class C
				{
					async System.Threading.Tasks.Task M()
					{
						DoThing();
						var first = 1;
						var second = 2;
						await Load(first);
						_ = await Save();
						int third = 3;
						await Load(third);
						await Save();
					}
				}
				""",

			"""
				class C
				{
					async System.Threading.Tasks.Task M()
					{
						DoThing();

						var first = 1;
						var second = 2;

						await Load(first);
						_ = await Save();

						int third = 3;

						await Load(third);
						await Save();
					}
				}
				""",
		],

		[
			"calls beside await runs settle whichever of ZA0004 and ZA0006 fixes first",

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateAwaitRuns
			),

			"""
				class C
				{
					async System.Threading.Tasks.Task M()
					{
						DoThing();
						DoThing();
						await Load();
						await Save();
						Build(1);
						Build(2);
						var value = 3;
						await Load(value);
						await Load(value);
					}
				}
				""",

			"""
				class C
				{
					async System.Threading.Tasks.Task M()
					{
						DoThing();
						DoThing();

						await Load();
						await Save();

						Build(1);
						Build(2);

						var value = 3;

						await Load(value);
						await Load(value);
					}
				}
				""",
		],

		[
			"assertions beside await runs settle whichever of ZA0005 and ZA0006 fixes first",

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateAssertionRuns,
				DiagnosticDescriptors.SeparateAwaitRuns
			),

			"""
				class C
				{
					async System.Threading.Tasks.Task M()
					{
						var value = 1;
						Assert.That(value);
						Assert.That(value);
						await Load(value);
						await Save();
						var other = 2;
						Assert.That(other);
						Assert.That(other);
					}
				}
				""",

			"""
				class C
				{
					async System.Threading.Tasks.Task M()
					{
						var value = 1;

						Assert.That(value);
						Assert.That(value);

						await Load(value);
						await Save();

						var other = 2;

						Assert.That(other);
						Assert.That(other);
					}
				}
				""",
		],

		[
			"assignments beside await runs settle whichever of ZA0008 and ZA0006 fixes first",

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateAssignmentRuns,
				DiagnosticDescriptors.SeparateAwaitRuns
			),

			"""
				class C
				{
					int _count;

					async System.Threading.Tasks.Task M()
					{
						DoThing();
						_count = 1;
						_count++;
						await Load();
						await Save();
						var value = 2;
						_count = value;
						_count += value;
					}
				}
				""",

			"""
				class C
				{
					int _count;

					async System.Threading.Tasks.Task M()
					{
						DoThing();

						_count = 1;
						_count++;

						await Load();
						await Save();

						var value = 2;

						_count = value;
						_count += value;
					}
				}
				""",
		],

		[
			"subscriptions beside await runs settle whichever of ZA0007 and ZA0006 fixes first",

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateEventSubscriptionRuns,
				DiagnosticDescriptors.SeparateAwaitRuns
			),

			"""
				class C
				{
					async System.Threading.Tasks.Task M()
					{
						DoThing();
						Events.Changed += DoThing;
						Events.Closed += DoThing;
						await Load();
						await Save();
						var value = 1;
						Events.Changed -= DoThing;
						Events.Closed -= DoThing;
					}
				}
				""",

			"""
				class C
				{
					async System.Threading.Tasks.Task M()
					{
						DoThing();

						Events.Changed += DoThing;
						Events.Closed += DoThing;

						await Load();
						await Save();

						var value = 1;

						Events.Changed -= DoThing;
						Events.Closed -= DoThing;
					}
				}
				""",
		],

		[
			"a base call beside calls of its name settles whichever of ZA0004 and ZA0010 fixes first",

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateInvocationRuns,
				DiagnosticDescriptors.SeparateBaseCall
			),

			"""
				class C : Stage
				{
					public override void OnStart()
					{
						base.DoThing();
						DoThing();
						Build(1);
						Build(2);
						Value = 3;
						base.DoThing();
					}
				}
				""",

			"""
				class C : Stage
				{
					public override void OnStart()
					{
						base.DoThing();

						DoThing();

						Build(1);
						Build(2);

						Value = 3;

						base.DoThing();
					}
				}
				""",
		],

		[
			"base calls beside guards and jumps settle whichever of ZA0009, ZA0010 and ZA0011 fixes first",

			TestRuleSets.Only(
				DiagnosticDescriptors.SeparateJumpStatement,
				DiagnosticDescriptors.SeparateBaseCall,
				DiagnosticDescriptors.SeparateGuardClauses
			),

			"""
				class C : Stage
				{
					public override void OnStart()
					{
						if (Value == 0) return;
						if (Value == 1) return;
						base.OnStart();
						return;
					}

					public override void OnStop()
					{
						if (Value == 0) return;
						Build(1);
						Build(2);
						base.OnStop();
					}

					public override int Fetch()
					{
						base.OnStart();
						Value = 1;
						Value = 2;
						return base.Fetch();
					}

					public override void Clear()
					{
						if (Value == 0) return;
						if (Value == 1) return;
						base.Clear();
					}
				}
				""",

			"""
				class C : Stage
				{
					public override void OnStart()
					{
						if (Value == 0) return;
						if (Value == 1) return;

						base.OnStart();

						return;
					}

					public override void OnStop()
					{
						if (Value == 0) return;

						Build(1);
						Build(2);

						base.OnStop();
					}

					public override int Fetch()
					{
						base.OnStart();

						Value = 1;
						Value = 2;

						return base.Fetch();
					}

					public override void Clear()
					{
						if (Value == 0) return;
						if (Value == 1) return;

						base.Clear();
					}
				}
				""",
		],

		[
			"an empty loop that only its collapse makes single-line leaves the if around it unbraced",

			TestRuleSets.Only(
				DiagnosticDescriptors.CollapseEmptyBlocks,
				DiagnosticDescriptors.UseBracesConsistently
			),

			"""
				class C
				{
					void M(int x)
					{
						if (x > 0)
							while (x > 1) {
							}
					}
				}
				""",

			"""
				class C
				{
					void M(int x)
					{
						if (x > 0)
							while (x > 1) { }
					}
				}
				""",
		],

		[
			"an accessor that its expression body makes single-line keeps the blank line beside it tight, as ZA0102 would",

			TestRuleSets.OnSuppressingOthersOf(
				[
					DiagnosticDescriptors.KeepMultiLineAccessorsTogether,
					DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember,
				],

				[
					DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether,
					DiagnosticDescriptors.KeepSingleLineAccessorsTogether,
				]
			),

			"""
				class C
				{
					private int _value;

					public int Value
					{
						get
						{
							return _value;
						}

						set
						{
							_value = value;
						}
					}
				}
				""",

			"""
				class C
				{
					private int _value;

					public int Value
					{
						get => _value;
						set => _value = value;
					}
				}
				""",
		],

		[
			"a collapse beside siblings on the lines of a member moves them down as ZA0001 would",

			TestRuleSets.Only(
				DiagnosticDescriptors.CollapseEmptyBlocks,
				DiagnosticDescriptors.SeparateMultiLineConstructs
			),

			"""
				class C
				{
					int x; void M() {
					} int y;
				}
				""",

			"""
				class C
				{
					int x;

					void M() { }

					int y;
				}
				""",
		],

		[
			"an expression body beside siblings on the lines of a member moves them down as ZA0001 would",

			TestRuleSets.Only(
				DiagnosticDescriptors.UseExpressionBodyForSingleLineMember,
				DiagnosticDescriptors.SeparateMultiLineConstructs
			),

			"""
				class C
				{
					int x; void M() {
						Foo();
					} int y;

					void Foo() { }
				}
				""",

			"""
				class C
				{
					int x;

					void M() => Foo();

					int y;

					void Foo() { }
				}
				""",
		],

		[
			"a value expression body beside siblings on the lines of a property moves them down as ZA0001 would",

			TestRuleSets.Only(
				DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember,
				DiagnosticDescriptors.SeparateMultiLineConstructs
			),

			"""
				class C
				{
					int x; int P {
						get { return 1; }
					} int y;
				}
				""",

			"""
				class C
				{
					int x;

					int P => 1;

					int y;
				}
				""",
		],

		[
			"a collapse beside initializer members on its lines moves them down as the multi-line rule of the list would",

			TestRuleSets.Only(
				DiagnosticDescriptors.CollapseEmptyBlocks,
				DiagnosticDescriptors.SeparateMultiLineInitializerMembers
			),

			"""
				class Foo { public int A, D; public Foo B; }

				class C
				{
					void M()
					{
						var f = new Foo { A = 1, B = new Foo {
						}, D = 2 };
					}
				}
				""",

			"""
				class Foo { public int A, D; public Foo B; }

				class C
				{
					void M()
					{
						var f = new Foo { A = 1,
						B = new Foo { },
						D = 2 };
					}
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(FixOrderRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AGapSettlesTheSameWayWhicheverFixComesFirst(
		string name,
		IReadOnlyDictionary<string, ReportDiagnostic> rules,
		string source,
		string fixedSource
	)
	{
		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			options: new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules },
			name
		);
	}

	// The capital rule leaves a listed keyword at the start of a text as written, so the keyword rule still finds it.
	[TestMethod]
	[DataRow("null", "True if bound")]
	[DataRow("null, true", "<see langword=\"true\"/> if bound")]
	public async Task TheCapitalAndTheKeywordRuleSettleTheSameWayWhicheverFixComesFirst(
		string langwords,
		string opening
	)
	{
		const string source = """
			internal sealed class Sample
			{
				/// <summary>true if bound; returns null otherwise.</summary>
				/// <returns>null when the section is missing.</returns>
				internal int Bind() => 1;
			}
			""";

		string fixedSource = $$"""
			internal sealed class Sample
			{
				/// <summary>{{opening}}; returns <see langword="null"/> otherwise.</summary>
				/// <returns><see langword="null"/> when the section is missing.</returns>
				internal int Bind() => 1;
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.CapitalizeDocumentationText,
			DiagnosticDescriptors.MarkLanguageKeywords
		);

		Dictionary<string, string> options = new() { ["stanza_doc_langwords"] = langwords };

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = rules,
				AnalyzerConfigOptions = options,
			},

			name: langwords
		);
	}

	[TestMethod]
	public async Task AFormattedFileWithItsBlankLinesTakenOutIsRestoredByEachFixOrder()
	{
		string formatted = FormattedFile.ReplaceLineEndings("\n");

		string damaged = string.Join(
			"\n",
			formatted.Split('\n').Where(line => line.Trim().Length > 0)
		);

		Assert.AreNotEqual(formatted, damaged);

		AnalyzerRunResult result = await RoslynTestHarness.RunIterativelyAsync(
			damaged,
			diagnosticOptions: s_layoutRules
		);

		RoslynTestHarness.AssertFixedText(formatted, result);

		RunOptions options = new(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = s_layoutRules,
		};

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: formatted,
			damaged,
			options
		);
	}
}
