using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.Convergence;

public sealed partial class ConvergenceTests
{
	// The rules that keep single-line siblings of a list together, which ship on.
	private static readonly DiagnosticDescriptor[] s_singleLineTightRules =
	[
		.. SiblingRuleSets.All.Select(siblings => siblings.KeepSingleLine),
	];

	// A collapse writes the blank lines the multi-line rule would have written, so the fix order writes one text.
	public static IEnumerable<object[]> CollapseOrderRows { get; } =
	[
		[
			"a method between a field and a method",
			DiagnosticDescriptors.UseExpressionBodyForSingleLineMember,

			"""
				class C
				{
					int x;
					void M()
					{
						DoThing();
					}
					void N() => DoThing();
					void DoThing() { }
				}
				""",

			"""
				class C
				{
					int x;

					void M() => DoThing();

					void N() => DoThing();
					void DoThing() { }
				}
				""",
		],

		[
			"a multi-line getter between two fields",
			DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember,

			"""
				class C
				{
					int x;
					int Value
					{
						get
						{
							return 1;
						}
					}
					int y;
				}
				""",

			"""
				class C
				{
					int x;

					int Value => 1;

					int y;
				}
				""",
		],

		[
			"a local function above a call",
			DiagnosticDescriptors.UseExpressionBodyForSingleLineMember,

			"""
				class C
				{
					void M()
					{
						void Local()
						{
							DoThing();
						}
						Local();
					}

					void DoThing() { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						void Local() => DoThing();

						Local();
					}

					void DoThing() { }
				}
				""",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(CollapseOrderRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AMemberThatCollapsesSettlesTheSameWayWithTheMultiLineRule(
		string name,
		DiagnosticDescriptor collapse,
		string source,
		string fixedSource
	)
	{
		DiagnosticDescriptor[] enabled =
		[
			DiagnosticDescriptors.SeparateMultiLineConstructs,
			collapse,
		];

		Dictionary<string, ReportDiagnostic> rules = TestRuleSets.OnSuppressingOthersOf(
			enabled,
			[.. enabled, .. s_statementCorpus.ShippedOn]
		);

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			options: new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules },
			name
		);
	}

	public static IEnumerable<object[]> JoinedEmptyBlockRows { get; } =
	[
		[
			"a method",

			"""
				class C
				{
					int x;
					void M() {
					}
					int y;
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
			"an if statement",

			"""
				class C
				{
					void M(int x)
					{
						DoThing();
						if (x > 0) {
						}
						DoThing();
					}

					void DoThing() { }
				}
				""",

			"""
				class C
				{
					void M(int x)
					{
						DoThing();

						if (x > 0) { }

						DoThing();
					}

					void DoThing() { }
				}
				""",
		],

		[
			"a chain whose two branches both join",

			"""
				class C
				{
					void M(int x)
					{
						DoThing();
						if (x > 0) {
						} else {
						}
						DoThing();
					}

					void DoThing() { }
				}
				""",

			"""
				class C
				{
					void M(int x)
					{
						DoThing();

						if (x > 0) { } else { }

						DoThing();
					}

					void DoThing() { }
				}
				""",
		],

		[
			"a switch expression arm",

			"""
				class Foo { }

				class C
				{
					Foo M(int x)
					{
						return x switch
						{
							1 => new Foo(),
							2 => new Foo {
							},
							3 => new Foo(),
							_ => new Foo(),
						};
					}
				}
				""",

			"""
				class Foo { }

				class C
				{
					Foo M(int x)
					{
						return x switch
						{
							1 => new Foo(),

							2 => new Foo { },

							3 => new Foo(),
							_ => new Foo(),
						};
					}
				}
				""",
		],

		[
			"a switch section",

			"""
				class Foo { }

				class C
				{
					Foo M(int x)
					{
						switch (x)
						{
							case 1: return new Foo();
							case 2: return new Foo {
							};
							case 3: return new Foo();
							default: return new Foo();
						}
					}
				}
				""",

			"""
				class Foo { }

				class C
				{
					Foo M(int x)
					{
						switch (x)
						{
							case 1: return new Foo();

							case 2: return new Foo { };

							case 3: return new Foo();
							default: return new Foo();
						}
					}
				}
				""",
		],

		[
			"a bare block",

			"""
				class C
				{
					void M()
					{
						DoThing();
						{
						}
						DoThing();
					}

					void DoThing() { }
				}
				""",

			"""
				class C
				{
					void M()
					{
						DoThing();

						{ }

						DoThing();
					}

					void DoThing() { }
				}
				""",
		],

		[
			"a statement below a comment that trails over two lines",

			"""
				class C
				{
					void M(int x)
					{
						DoThing();
						if (x > 0) {
						} /* One
						two */
						DoThing();
					}

					void DoThing() { }
				}
				""",

			"""
				class C
				{
					void M(int x)
					{
						DoThing();

						if (x > 0) { } /* One
						two */

						DoThing();
					}

					void DoThing() { }
				}
				""",
		],

		[
			"two classes",

			"""
				class A {
				}
				class B {
				}
				class D { }
				""",

			"""
				class A { }

				class B { }

				class D { }
				""",
		],
	];

	public static IEnumerable<object[]> JoinedEmptyBlockInListRows { get; } =
	[
		[
			"an event's accessors",
			DiagnosticDescriptors.SeparateMultiLineAccessors,

			"""
				class C
				{
					event System.Action E
					{
						add {
						}
						remove {
						}
					}
				}
				""",

			"""
				class C
				{
					event System.Action E
					{
						add { }

						remove { }
					}
				}
				""",
		],

		[
			"an accessor beside an arrow",
			DiagnosticDescriptors.SeparateMultiLineAccessors,

			"""
				class C
				{
					int _x;

					int P
					{
						get => _x;
						set {
						}
					}
				}
				""",

			"""
				class C
				{
					int _x;

					int P
					{
						get => _x;

						set { }
					}
				}
				""",
		],

		[
			"a collection element",
			DiagnosticDescriptors.SeparateMultiLineCollectionElements,

			"""
				class Foo { }

				class C
				{
					Foo[] items = new Foo[]
					{
						new Foo(),
						new Foo {
						},
						new Foo(),
					};
				}
				""",

			"""
				class Foo { }

				class C
				{
					Foo[] items = new Foo[]
					{
						new Foo(),

						new Foo { },

						new Foo(),
					};
				}
				""",
		],

		[
			"an argument",
			DiagnosticDescriptors.SeparateMultiLineArguments,

			"""
				class C
				{
					void M(System.Action a, System.Action b, System.Action c)
					{
						M(
							() => { },
							() => {
							},
							() => { }
						);
					}
				}
				""",

			"""
				class C
				{
					void M(System.Action a, System.Action b, System.Action c)
					{
						M(
							() => { },

							() => { },

							() => { }
						);
					}
				}
				""",
		],

		[
			"an initializer member",
			DiagnosticDescriptors.SeparateMultiLineInitializerMembers,

			"""
				class Foo
				{
					public Foo A;

					public Foo B;

					public Foo D;
				}

				class C
				{
					Foo item = new Foo
					{
						A = new Foo(),
						B = new Foo {
						},
						D = new Foo(),
					};
				}
				""",

			"""
				class Foo
				{
					public Foo A;

					public Foo B;

					public Foo D;
				}

				class C
				{
					Foo item = new Foo
					{
						A = new Foo(),

						B = new Foo { },

						D = new Foo(),
					};
				}
				""",
		],

		[
			"a declarator",
			DiagnosticDescriptors.SeparateMultiLineDeclarators,

			"""
				class Foo { }

				class C
				{
					void M()
					{
						Foo a = new Foo(),
							b = new Foo {
							},
							c = new Foo();
					}
				}
				""",

			"""
				class Foo { }

				class C
				{
					void M()
					{
						Foo a = new Foo(),

							b = new Foo { },

							c = new Foo();
					}
				}
				""",
		],
	];

	// A join in a list unit leaves a single line, so it writes the gaps of that list's multi-line rule, which ships off for these lists.
	[TestMethod]
	[DynamicData(
		nameof(JoinedEmptyBlockInListRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AnEmptyBlockJoinedInAListSettlesTheSameWayInEveryFixOrder(
		string name,
		DiagnosticDescriptor separating,
		string source,
		string fixedSource
	)
	{
		// The single-line tight rules ship on and would take the gaps once the join leaves single lines, so they stay off.
		var options = new RunOptions(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = TestRuleSets.OnSuppressingOthersOf(
				[separating],
				s_singleLineTightRules
			),
		};

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			options,
			name
		);
	}

	// The comment that trails the closing brace goes below the body, and the blank line stands below the comment.
	[TestMethod]
	public async Task AnElseBelowAJumpSettlesBesideACommentThatTrailsOverTwoLines()
	{
		const string source = """
			class C
			{
				int M(int x)
				{
					DoThing();

					if (x > 0)
					{
						return 1;
					}
					else
					{
						DoThing();
					} /* One
					two */
					return 2;
				}

				void DoThing() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				int M(int x)
				{
					DoThing();

					if (x > 0)
					{
						return 1;
					}

					DoThing();
					/* One
					two */

					return 2;
				}

				void DoThing() { }
			}
			""";

		var options = new RunOptions(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = new Dictionary<string, ReportDiagnostic>
			{
				[DiagnosticDescriptors.RemoveElseAfterJump.Id] = ReportDiagnostic.Warn,
			},
		};

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(fixedSource, source, options);
	}

	// A block joined to its header's line leaves a single line, which the multi-line rule does not judge, so the join writes the gaps that rule asks.
	[TestMethod]
	[DynamicData(
		nameof(JoinedEmptyBlockRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AnEmptyBlockJoinedToItsHeaderSettlesTheSameWayInEveryFixOrder(
		string name,
		string source,
		string fixedSource
	)
	{
		var options = new RunOptions(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = TestRuleSets.OnSuppressingOthersOf([], s_singleLineTightRules),
		};

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(
			expected: fixedSource,
			source,
			options,
			name
		);
	}

	[TestMethod]
	public async Task AnEmptyBlockJoinedToItsHeaderWritesNoBlankLineThatTheMultiLineRuleDoesNotAskFor()
	{
		string pragma =
			$"#pragma warning disable {DiagnosticDescriptors.SeparateMultiLineConstructs.Id}";

		string source =
			pragma
			+ """

				class C
				{
					int x;
					void M() {
					}
					int y;
				}
				""";

		string fixedSource =
			pragma
			+ """

				class C
				{
					int x;
					void M() { }
					int y;
				}
				""";

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(expected: fixedSource, source);
	}

	// A multi-line accessor stands in its own gaps, so a collapse writes what the accessor rule would.
	[TestMethod]
	public async Task ACollapsedAccessorSettlesTheSameWayWithTheAccessorRule()
	{
		const string source = """
			class C
			{
				int _v;

				int Value
				{
					get
					{
						return _v;
					}
					set
					{
						_v = value;
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int _v;

				int Value
				{
					get => _v;

					set => _v = value;
				}
			}
			""";

		var options = new RunOptions(RoslynTestHarness.AllAnalyzers)
		{
			DiagnosticOptions = TestRuleSets.OnSuppressingOthersOf(
				[
					DiagnosticDescriptors.SeparateMultiLineAccessors,
					DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember,
				],

				s_singleLineTightRules
			),
		};

		await RoslynTestHarness.AssertSettlesInEveryFixOrderAsync(fixedSource, source, options);
	}

	// A pragma over the multi-line rule asks for no blank line, so the collapse writes none.
	[TestMethod]
	public async Task ACollapseWritesNoBlankLineThatTheMultiLineRuleDoesNotAskFor()
	{
		string pragma =
			$"#pragma warning disable {DiagnosticDescriptors.SeparateMultiLineConstructs.Id}";

		string source =
			pragma
			+ """

				class C
				{
					void P() { }
					void M()
					{
						DoThing();
					}
					void DoThing() { }
				}
				""";

		string fixedSource =
			pragma
			+ """

				class C
				{
					void P() { }
					void M() => DoThing();
					void DoThing() { }
				}
				""";

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = TestRuleSets.Only(
					DiagnosticDescriptors.SeparateMultiLineConstructs,
					DiagnosticDescriptors.UseExpressionBodyForSingleLineMember
				),
			}
		);
	}

	// A directive keeps its gap as written, for the collapse as for the multi-line rule.
	[TestMethod]
	public async Task ACollapseLeavesTheGapOfADirective()
	{
		const string source = """
			class C
			{
				void P() { }
			#nullable enable
				void M()
				{
					DoThing();
				}
				void DoThing() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				void P() { }
			#nullable enable
				void M() => DoThing();

				void DoThing() { }
			}
			""";

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = TestRuleSets.Only(
					DiagnosticDescriptors.SeparateMultiLineConstructs,
					DiagnosticDescriptors.UseExpressionBodyForSingleLineMember
				),
			}
		);
	}

	// A Fix All that ran the rule of the lower gaps first would leave the lone const tight above, were only the first of them held.
	[TestMethod]
	public async Task TwoGapsOfOneRuleWaitTogetherForTheGapAnotherRuleAsksForAboveThem()
	{
		const string source = """
			class C
			{
				private string _a;
				private string _b;
				private int _c;
				private const int K = 1;
				public string D;
				private string _e;
			}
			""";

		const string fixedSource = """
			class C
			{
				private string _a;
				private string _b;

				private int _c;

				private const int K = 1;

				public string D;
				private string _e;
			}
			""";

		IReadOnlyDictionary<string, ReportDiagnostic> rules = TestRuleSets.Only(
			DiagnosticDescriptors.SeparateMemberGroups,
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
		);

		AnalyzerRunResult reported = await RoslynTestHarness.ReportAllAsync(source, rules);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns.Id,
			reported.Diagnostics.Single().Id
		);

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: fixedSource,
			source,
			options: new RunOptions(RoslynTestHarness.AllAnalyzers) { DiagnosticOptions = rules }
		);
	}

	// The same holds where the gap that waits belongs to the rule ahead of the one below it.
	[TestMethod]
	public async Task AGapWaitsWithTheGapOfAnotherRuleThatWaitsAboveIt()
	{
		const string source = """
			class C
			{
				private static int _a;
				public int B;
				private string _c;
				private const int K = 1;
				public int P { get; set; }
				public int Q { get; set; }
			}
			""";

		const string fixedSource = """
			class C
			{
				private static int _a;
				public int B;

				private string _c;

				private const int K = 1;

				public int P { get; set; }
				public int Q { get; set; }
			}
			""";

		await RoslynTestHarness.AssertFixAllPerIdSettlesAsync(
			expected: fixedSource,
			source,

			options: new RunOptions(RoslynTestHarness.AllAnalyzers)
			{
				DiagnosticOptions = TestRuleSets.Only(
					DiagnosticDescriptors.SeparateDeclarationRuns,
					DiagnosticDescriptors.SeparateMemberGroups,
					DiagnosticDescriptors.SeparateSameTypeDeclarationRuns
				),
			}
		);
	}
}
