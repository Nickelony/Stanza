using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests.BlankLines;

[TestClass]
public sealed class SeparateAssignmentRunsTests
{
	private static readonly IReadOnlyDictionary<string, ReportDiagnostic> s_on = TestRuleSets.Only(
		DiagnosticDescriptors.SeparateAssignmentRuns
	);

	[TestMethod]
	public async Task RepeatedAssignmentsGetSeparated()
	{
		string source = Method(
			"""
			DoThing();
			a = 1;
			b = 2;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			a = 1;
			b = 2;

			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateAssignmentRunsAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EveryKindOfAssignmentJoinsOneRun()
	{
		string source = Method(
			"""
			DoThing();
			a = 1;
			this.b = 2;
			c += 3;
			d++;
			--e;
			(f, g) = Read();
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			a = 1;
			this.b = 2;
			c += 3;
			d++;
			--e;
			(f, g) = Read();

			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateAssignmentRunsAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneAssignmentStaysTight()
	{
		string source = Method(
			"""
			DoThing();
			a = 1;
			DoThing();
			d++;
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateAssignmentRunsAnalyzer>(
			source,
			s_on
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnAwaitAndACallOnTheRightSideStillReadAsAssignments()
	{
		string source = Method(
			"""
			DoThing();
			a = await Load();
			b = Build(a);
			DoThing();
			""",

			isAsync: true
		);

		string fixedSource = Method(
			"""
			DoThing();

			a = await Load();
			b = Build(a);

			DoThing();
			""",

			isAsync: true
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateAssignmentRunsAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ADiscardedCallBelongsToTheInvocationRuleAndADiscardedValueToThisOne()
	{
		string call = Method(
			"""
			DoThing();
			_ = Read();
			a = 1;
			DoThing();
			"""
		);

		string value = Method(
			"""
			DoThing();
			_ = a;
			_ = b;
			DoThing();
			"""
		);

		string fixedValue = Method(
			"""
			DoThing();

			_ = a;
			_ = b;

			DoThing();
			"""
		);

		AnalyzerRunResult callResult =
			await RoslynTestHarness.RunAsync<SeparateAssignmentRunsAnalyzer>(call, s_on);

		AnalyzerRunResult valueResult =
			await RoslynTestHarness.RunAsync<SeparateAssignmentRunsAnalyzer>(value, s_on);

		Assert.IsEmpty(callResult.Diagnostics);
		Assert.HasCount(2, valueResult.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedValue, valueResult);
	}

	[TestMethod]
	public async Task ASubscriptionReadsAsACompoundAssignment()
	{
		string source = Method(
			"""
			DoThing();
			Changed += Handler;
			Changed -= Handler;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			Changed += Handler;
			Changed -= Handler;

			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateAssignmentRunsAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AttachedCommentMovesWithTheRun()
	{
		string source = Method(
			"""
			DoThing();
			// Reset the pair.
			a = 1;
			b = 2;
			DoThing();
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			// Reset the pair.
			a = 1;
			b = 2;

			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateAssignmentRunsAnalyzer>(
			source,
			s_on
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheRuleShipsOffAndReportsNothingUntilASeverityEnablesIt()
	{
		string source = Method(
			"""
			DoThing();
			a = 1;
			b = 2;
			DoThing();
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportRunRulesAsync(source);

		AnalyzerRunResult enabled = await RoslynTestHarness.ReportRunRulesAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.SeparateAssignmentRuns)
		);

		Assert.IsEmpty(result.Diagnostics);
		Assert.HasCount(2, enabled.Diagnostics);
	}

	[TestMethod]
	public async Task ALoneDeclarationAboveAnAssignmentRunTakesItsBlankLineFromThisRule()
	{
		string source = Method(
			"""
			DoThing();
			var value = 1;
			a = value;
			b = value;
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();

			var value = 1;

			a = value;
			b = value;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ALoneDeclarationStaysTightAgainstAnAssignmentRunWhereThisRuleIsOff()
	{
		string source = Method(
			"""
			DoThing();
			var value = 1;
			a = value;
			b = value;
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportRunRulesAsync(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheDeclarationRuleOwnsTheGapBetweenADeclarationRunAndAnAssignmentRun()
	{
		string source = Method(
			"""
			var first = 1;
			var second = 2;
			a = first;
			b = second;
			"""
		);

		string fixedSource = Method(
			"""
			var first = 1;
			var second = 2;

			a = first;
			b = second;
			"""
		);

		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateDeclarationRuns.Id, both.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, both);

		// With the declaration rule off, the same gap goes to this rule and settles on the same text.
		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(
			source,

			TestRuleSets.WithOneOff(
				on: [DiagnosticDescriptors.SeparateAssignmentRuns],
				off: DiagnosticDescriptors.SeparateDeclarationRuns
			)
		);

		Assert.HasCount(1, handedOver.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateAssignmentRuns.Id,
			handedOver.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	[TestMethod]
	public async Task TheInvocationRuleOwnsTheGapBetweenACallRunAndAnAssignmentRun()
	{
		string source = Method(
			"""
			DoThing();
			DoThing();
			a = 1;
			b = 2;
			"""
		);

		string fixedSource = Method(
			"""
			DoThing();
			DoThing();

			a = 1;
			b = 2;
			"""
		);

		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateInvocationRuns.Id, both.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, both);

		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(
			source,

			TestRuleSets.WithOneOff(
				on: [DiagnosticDescriptors.SeparateAssignmentRuns],
				off: DiagnosticDescriptors.SeparateInvocationRuns
			)
		);

		Assert.HasCount(1, handedOver.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateAssignmentRuns.Id,
			handedOver.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	[TestMethod]
	public async Task TheAssertionRuleOwnsTheGapBetweenAnAssertionRunAndAnAssignmentRun()
	{
		string source = Method(
			"""
			Assert.That(1);
			Assert.That(2);
			a = 1;
			b = 2;
			"""
		);

		string fixedSource = Method(
			"""
			Assert.That(1);
			Assert.That(2);

			a = 1;
			b = 2;
			"""
		);

		AnalyzerRunResult both = await RoslynTestHarness.RunRunRulesAsync(source, s_on);

		Assert.HasCount(1, both.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateAssertionRuns.Id, both.Diagnostics[0].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, both);

		AnalyzerRunResult handedOver = await RoslynTestHarness.RunRunRulesAsync(
			source,

			TestRuleSets.WithOneOff(
				on: [DiagnosticDescriptors.SeparateAssignmentRuns],
				off: DiagnosticDescriptors.SeparateAssertionRuns
			)
		);

		Assert.HasCount(1, handedOver.Diagnostics);

		Assert.AreEqual(
			DiagnosticDescriptors.SeparateAssignmentRuns.Id,
			handedOver.Diagnostics[0].Id
		);

		RoslynTestHarness.AssertFixedText(fixedSource, handedOver);
	}

	[TestMethod]
	public async Task APragmaOverTheDeclarationRuleHandsItsGapToThisRule()
	{
		string source = Method(
			$$"""
			#pragma warning disable {{DiagnosticDescriptors.SeparateDeclarationRuns.Id}}
			var first = 1;
			var second = 2;
			a = first;
			b = second;
			#pragma warning restore {{DiagnosticDescriptors.SeparateDeclarationRuns.Id}}
			"""
		);

		AnalyzerRunResult result = await RoslynTestHarness.ReportRunRulesAsync(source, s_on);

		Assert.HasCount(1, result.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.SeparateAssignmentRuns.Id, result.Diagnostics[0].Id);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define A\n")]
	public async Task AnInactiveConditionalBlockLeavesTheRunToTheViewThatHoldsIt(string view)
	{
		string source =
			view
			+ Method(
				"""
				DoThing();
				a = 1;
				#if A
				b = 2;
				#endif
				DoThing();
				"""
			);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateAssignmentRunsAnalyzer>(
				source,

				TestRuleSets.WithOneOff(
					on: [DiagnosticDescriptors.SeparateAssignmentRuns],
					off: DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
				)
			);

		// The run holds both assignments only in the view that defines A, and a directive keeps the gap below it as written.
		Assert.HasCount(view.Length == 0 ? 0 : 1, result.Diagnostics, $"View [{view.Trim()}].");
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define A\n")]
	public async Task AConditionalBlockEndsTheRunInEveryViewWhereItsRuleIsOn(string view)
	{
		string source =
			view
			+ Method(
				"""
				DoThing();
				a = 1;
				#if A
				b = 2;
				#endif
				DoThing();
				"""
			);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateAssignmentRunsAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.SeparateAssignmentRuns,
					DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors
				)
			);

		Assert.IsEmpty(result.Diagnostics, $"View [{view.Trim()}].");
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define A\n")]
	public async Task ARegionMarkerEndsTheRunInEveryViewWhereItsRuleIsOn(string view)
	{
		string source =
			view
			+ Method(
				"""
				DoThing();
				a = 1;
				#region Pair
				b = 2;
				#endregion
				DoThing();
				"""
			);

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateAssignmentRunsAnalyzer>(
				source,

				TestRuleSets.Only(
					DiagnosticDescriptors.SeparateAssignmentRuns,
					DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors
				)
			);

		Assert.IsEmpty(result.Diagnostics, $"View [{view.Trim()}].");
	}

	[TestMethod]
	[DataRow(null)]
	[DataRow("mixed")]
	[DataRow("other")]
	public async Task EveryValueButSplitKeepsFieldsAndPropertiesInOneRun(string? value)
	{
		string source = Normalize(
			"""
			class C
			{
				int _count;

				int Count { get; set; }

				void M()
				{
					DoThing();
					_count = 1;
					Count = 2;
					DoThing();
				}
			}
			"""
		);

		string fixedSource = Normalize(
			"""
			class C
			{
				int _count;

				int Count { get; set; }

				void M()
				{
					DoThing();

					_count = 1;
					Count = 2;

					DoThing();
				}
			}
			"""
		);

		Dictionary<string, string>? options = value is null ? null : new() { [TargetsKey] = value };

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<SeparateAssignmentRunsAnalyzer>(
			source,
			s_on,
			options
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// Each row is a class in which a line of a single `~` stands where the split writes a blank line; a row without one stays as written.
	public static IEnumerable<object[]> TargetRows { get; } =
	[
		[
			"a field is a field whatever its name",

			"""
				class C
				{
					int _a, s_b, t_c, m_d, plain;

					int Count { get; set; }

					void M()
					{
						_a = 1;
						s_b = 2;
						t_c = 3;
						m_d = 4;
						plain = 5;
						~
						Count = 6;
						Count = 7;
					}
				}
				""",

			true,
		],

		[
			"static and thread-static fields are fields",

			"""
				class C
				{
					static int s_count;

					[ThreadStatic]
					static int t_count;

					int Count { get; set; }

					void M()
					{
						s_count = 1;
						C.s_count = 2;
						t_count = 3;
						~
						Count = 4;
						Count = 5;
					}
				}
				""",

			true,
		],

		[
			"a field through this, base or another instance is a field",

			"""
				class B
				{
					protected int _shared;
				}

				class C : B
				{
					int _own;

					int Count { get; set; }

					void M(C other)
					{
						this._own = 1;
						base._shared = 2;
						other._own = 3;
						~
						Count = 4;
						this.Count = 5;
					}
				}
				""",

			true,
		],

		[
			"a field of a struct local or parameter is a field",

			"""
				struct P
				{
					public int X;

					public int Y { get; set; }
				}

				class C
				{
					void M(P point)
					{
						var other = new P();

						point.X = 1;
						other.X = 2;
						~
						point.Y = 3;
						other.Y = 4;
					}
				}
				""",

			true,
		],

		[
			"a readonly field assigned in a constructor is a field",

			"""
				class C
				{
					readonly int _first;
					readonly int _second;

					C(int value)
					{
						_first = value;
						_second = value;
						~
						value = 0;
						value++;
					}
				}
				""",

			true,
		],

		[
			"a field-like event written with = is a field",

			"""
				class C
				{
					event Action Changed;

					int _count;

					int Count { get; set; }

					void M()
					{
						Changed = null;
						_count = 0;
						~
						Count = 1;
						Count = 2;
					}
				}
				""",

			true,
		],

		[
			"a subscription has no kind, so it joins the runs on both sides",

			"""
				class C
				{
					event Action Changed;

					int _count;

					int Count { get; set; }

					void M()
					{
						_count = 0;
						Changed += DoThing;
						Count = 1;
						Changed -= DoThing;
						_count = 2;
					}
				}
				""",

			true,
		],

		[
			"static, init-only and explicitly implemented properties are properties",

			"""
				interface IHas
				{
					int Value { get; set; }
				}

				class C : IHas
				{
					int _count;

					static int Shared { get; set; }

					int Init { get; init; }

					int IHas.Value { get; set; }

					C()
					{
						Shared = 1;
						Init = 2;
						((IHas)this).Value = 3;
						~
						_count = 4;
						_count = 5;
					}
				}
				""",

			true,
		],

		[
			"a ref-returning property is a property",

			"""
				class C
				{
					int _store;
					int _count;

					ref int Slot => ref _store;

					int Count { get; set; }

					void M()
					{
						Slot = 1;
						Count = 2;
						~
						_count = 3;
						_count = 4;
					}
				}
				""",

			true,
		],

		[
			"a member access is the kind of the member it names, not of where it starts",

			"""
				class C
				{
					Item _item = new();
					int _count;

					void M()
					{
						_item.Name = "a";
						_item.Count = 1;
						~
						_count = 2;
						_item = new();
					}
				}
				""",

			true,
		],

		[
			"ref locals, scoped locals and parameters are locals",

			"""
				class C
				{
					int _count;
					int[] _items = new int[2];

					void M(int value, ref int slot)
					{
						ref int first = ref _items[0];
						scoped ref int second = ref _items[1];

						first = 1;
						second = 2;
						first = ref _items[1];
						value = 3;
						slot = 4;
						~
						_count = 5;
						_count = 6;
					}
				}
				""",

			true,
		],

		[
			"a primary constructor parameter is a parameter",

			"""
				class C(int seed, int captured)
				{
					int _count;

					int Read() => seed + captured;

					void M()
					{
						seed = 1;
						captured = 2;
						~
						_count = 3;
						_count = 4;
					}
				}
				""",

			true,
		],

		[
			"a variable captured by a lambda or a local function is a local",

			"""
				class C
				{
					int _count;

					void M()
					{
						int total = 0;

						Action add = () =>
						{
							total = 1;
							total++;
							~
							_count = 2;
							_count = 3;
						};

						void Reset()
						{
							total = 0;
							total--;
							~
							_count = 0;
							_count++;
						}

						add();
						Reset();
					}
				}
				""",

			true,
		],

		[
			"an indexer, an array, a span and an index from the end are elements",

			"""
				class C
				{
					int[] _items = new int[4];
					List<int> _list = [0];
					int _count;

					int this[int index]
					{
						get => 0;
						set { }
					}

					void M(Span<int> span)
					{
						_items[0] = 1;
						_items[^1] = 2;
						_list[0] = 3;
						this[0] = 4;
						span[0] = 5;
						~
						_count = 6;
						_count = 7;
					}
				}
				""",

			true,
		],

		[
			"a ref-returning indexer is an element",

			"""
				class C
				{
					int[] _items = new int[4];
					int _count;

					ref int this[int index] => ref _items[index];

					void M()
					{
						this[0] = 1;
						this[1]++;
						~
						_count = 2;
						_count = 3;
					}
				}
				""",

			true,
		],

		[
			"a pointer dereference is an element and a member through a pointer is a field",

			"""
				struct P
				{
					public int X;
				}

				unsafe class C
				{
					int _count;

					void M(int* pointer, P* point)
					{
						*pointer = 1;
						pointer[1] = 2;
						~
						point->X = 3;
						_count = 4;
					}
				}
				""",

			true,
		],

		[
			"a null-coalescing assignment, a compound assignment and a step follow their target",

			"""
				class C
				{
					string _name;
					int _count;

					int Count { get; set; }

					void M()
					{
						_name ??= "a";
						_count += 1;
						_count++;
						--_count;
						~
						Count -= 1;
						Count++;
					}
				}
				""",

			true,
		],

		[
			"a deconstruction of one kind joins that kind",

			"""
				class C
				{
					int _a, _b;

					int Count { get; set; }

					void M()
					{
						(_a, _b) = (1, 2);
						_a = 3;
						~
						Count = 4;
						Count = 5;
					}
				}
				""",

			true,
		],

		[
			"a deconstruction of several kinds stands alone and ends the runs on both sides",

			"""
				class C
				{
					int _a;

					void M(int b)
					{
						_a = 1;
						_a = 2;
						~
						(_a, b) = (3, 4);
						~
						b = 5;
						b = 6;
					}
				}
				""",

			true,
		],

		[
			"a discard has no kind, so it joins the run around it",

			"""
				class C
				{
					int _a, _b;

					int Count { get; set; }

					void M()
					{
						_a = 1;
						_ = _a;
						(_b, _) = Read();
						~
						Count = 2;
						Count = 3;
					}
				}
				""",

			true,
		],

		[
			"a discard of a value between two kinds joins both runs, so nothing is split",

			"""
				class C
				{
					int _a, _b;

					int Count { get; set; }

					void M(int x)
					{
						_a = 1;
						_b = 2;
						_ = x;
						Count = 3;
						Count = 4;
					}
				}
				""",

			true,
		],

		[
			"a member of a dynamic value has no kind, so it joins the run around it",

			"""
				class C
				{
					int _count;

					void M(dynamic value)
					{
						_count = 1;
						value.Name = "a";
						_count = 2;
					}
				}
				""",

			false,
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(TargetRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheSplitSetsEachKindOfTargetApart(string name, string template, bool compiles)
	{
		AnalyzerRunResult result = await RunSplitAsync(WithoutMarks(template), compiles);
		RoslynTestHarness.AssertFixedText(WithMarksBlank(template), result, $"{name}:");
	}

	// The types the rows of one root write through: an anchor, and a chain of an outer and an inner.
	private const string RootTypes = """
		class Anchor
		{
			public int Movement { get; set; }

			public bool Survives { get; set; }
		}

		class Inner
		{
			public int C { get; set; }
		}

		class Outer
		{
			public Inner B { get; } = new();

			public Inner Get() => B;
		}
		""";

	// Each row is a class as `TargetRows` writes one, after the types of `RootTypes`.
	public static IEnumerable<object[]> SharedRootRows { get; } =
	[
		[
			"a field and the members written through it are one run, and the next root splits off",

			"""
				class C
				{
					Anchor _start, _end;

					void M()
					{
						_start = new Anchor();
						_start.Movement = 1;
						_start.Survives = true;
						~
						_end = new Anchor();
						_end.Movement = 2;
						_end.Survives = true;
					}
				}
				""",

			true,
		],

		[
			"a chain takes the root it starts from",

			"""
				class C
				{
					Outer _a;

					void M(int total)
					{
						_a = new Outer();
						_a.B.C = 1;
						~
						total = 2;
					}
				}
				""",

			true,
		],

		[
			"a static field is the root of what it writes through, not the type that holds it",

			"""
				class Statics
				{
					public static Anchor Other;

					public static Anchor Shared;
				}

				class C
				{
					void M()
					{
						Statics.Other.Movement = 0;
						~
						Statics.Shared = new Anchor();
						Statics.Shared.Movement = 1;
					}
				}
				""",

			true,
		],

		[
			"a pointer and the value it points to share the pointer as their root",

			"""
				unsafe class C
				{
					int _count;

					void M(int* pointer)
					{
						pointer = null;
						*pointer = 1;
						~
						_count = 2;
					}
				}
				""",

			true,
		],

		[
			"a chain through a call has no root, so it splits from the field it starts at",

			"""
				class C
				{
					Outer _a, _b;

					void M()
					{
						_b = new Outer();
						_a = new Outer();
						~
						_a.Get().C = 1;
					}
				}
				""",

			true,
		],

		[
			"this. qualifies the field that is the root",

			"""
				class C
				{
					Outer _a;

					void M(int total)
					{
						this._a = new Outer();
						_a.B.C = 1;
						this._a.B.C = 2;
						~
						total = 3;
					}
				}
				""",

			true,
		],

		[
			"an element takes the root of what it indexes",

			"""
				class C
				{
					int[] _items;

					int Count { get; set; }

					void M()
					{
						_items = new int[2];
						_items[0] = 1;
						_items[1] = 2;
						~
						Count = 3;
					}
				}
				""",

			true,
		],

		[
			"a local is its own root",

			"""
				class C
				{
					int _count;

					void M()
					{
						Anchor anchor;

						anchor = new Anchor();
						anchor.Movement = 1;
						~
						_count = 2;
					}
				}
				""",

			true,
		],

		[
			"two roots of one kind join by kind, and of two kinds split",

			"""
				class C
				{
					Anchor _start, _end;

					int _count;

					void M()
					{
						_start.Movement = 1;
						_end.Movement = 2;
						~
						_count = 3;
					}
				}
				""",

			true,
		],

		[
			"a root that does not bind joins nothing by it, so an element of it splits from a field",

			"""
				class C
				{
					int _count;

					void M()
					{
						_count = 1;
						~
						missing[0] = 2;
						missing[1] = 3;
					}
				}
				""",

			false,
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(SharedRootRows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task TheSplitKeepsNeighborsThatWriteThroughOneRootTogether(
		string name,
		string template,
		bool compiles
	)
	{
		string types = Normalize(RootTypes) + "\n";

		AnalyzerRunResult result = await RunSplitAsync(types + WithoutMarks(template), compiles);
		RoslynTestHarness.AssertFixedText(types + WithMarksBlank(template), result, $"{name}:");
	}

	// The anchors as their owner wrote them, and with a blank line inside the first run, which ZA0054 owns.
	[TestMethod]
	[DataRow("")]
	[DataRow("\n")]
	public async Task TheAnchorsOfOneRootSettleAsTheyStand(string inside)
	{
		string source =
			Normalize(RootTypes)
			+ "\n"
			+ Normalize(
				$$"""
				class C
				{
					Anchor _start, _end;

					void M()
					{
						_start = new Anchor();{{inside}}
						_start.Movement = 1;
						_start.Survives = true;

						_end = new Anchor();
						_end.Movement = 2;
						_end.Survives = true;
					}
				}
				"""
			);

		AnalyzerRunResult result = await RunSplitAsync(source, compiles: true);

		Assert.IsEmpty(result.Diagnostics);
		Assert.AreEqual(source, result.FixedText);
	}

	// A target that does not bind takes no kind, so the run keeps the shape it has once the target binds as its neighbors do.
	[TestMethod]
	public async Task ATargetThatDoesNotBindSplitsNothingAndKeepsItsTextOnceItBinds()
	{
		const string template = """
			class C
			{
				int _a, _b;
			DECLARATION
				int Count { get; set; }

				void M()
				{
					_a = 1;
					missing = 2;
					_b = 3;
					~
					Count = 4;
					Count = 5;
				}
			}
			""";

		string missing = Normalize(template)
			.Replace("DECLARATION\n", string.Empty, StringComparison.Ordinal);

		string declared = Normalize(template)
			.Replace("DECLARATION", "\tint missing;", StringComparison.Ordinal);

		AnalyzerRunResult unbound = await RunSplitAsync(WithoutMarks(missing), compiles: false);
		AnalyzerRunResult bound = await RunSplitAsync(WithoutMarks(declared), compiles: true);

		RoslynTestHarness.AssertFixedText(WithMarksBlank(missing), unbound);
		RoslynTestHarness.AssertFixedText(WithMarksBlank(declared), bound);

		// The fix writes the same blank line whether the target binds, so only the declaration tells the two texts apart.
		Assert.AreEqual(
			unbound.FixedText,
			bound.FixedText.Replace("\tint missing;\n", string.Empty, StringComparison.Ordinal)
		);
	}

	// A Roslyn with C# 14 binds the `field` of a property to its backing field, and one without it reads a name that does not bind.
	[TestMethod]
	public async Task TheFieldOfAPropertyIsAFieldWhereTheLanguageHasIt()
	{
		bool hasCSharp14 = Enum.IsDefined(typeof(LanguageVersion), 1400);

		const string template = """
			class C
			{
				int Count { get; set; }

				int Value
				{
					get;
					set
					{
						field = value;
						~
						Count = 1;
						Count = 2;
					}
				}
			}
			""";

		AnalyzerRunResult result = await RunSplitAsync(
			WithoutMarks(template),
			compiles: hasCSharp14
		);

		RoslynTestHarness.AssertFixedText(
			hasCSharp14 ? WithMarksBlank(template) : WithoutMarks(template),
			result
		);
	}

	// A Roslyn with C# 14 reads `item?.Name = "a";` as an assignment of the property, and one without it as one it cannot bind.
	[TestMethod]
	public async Task ANullConditionalAssignmentIsTheKindOfItsMemberWhereTheLanguageHasIt()
	{
		bool hasCSharp14 = Enum.IsDefined(typeof(LanguageVersion), 1400);

		const string template = """
			class C
			{
				int _count;

				void M(Item item)
				{
					item?.Name = "a";
					item?.Count = 1;
					~
					_count = 2;
					_count = 3;
				}
			}
			""";

		AnalyzerRunResult result = await RunSplitAsync(
			WithoutMarks(template),
			compiles: hasCSharp14
		);

		RoslynTestHarness.AssertFixedText(
			hasCSharp14 ? WithMarksBlank(template) : WithoutMarks(template),
			result
		);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define VIEW\n")]
	public async Task AnAssignmentInsideAConditionalBlockSplitsTheSameWayInEitherView(string view)
	{
		const string template = """
			class C
			{
				int _a;

				int Count { get; set; }

				void M()
				{
			#if VIEW
					_a = 0;
			#endif
					_a = 1;
					_a = 2;
					~
					Count = 3;
					Count = 4;
				}
			}
			""";

		AnalyzerRunResult result = await RunSplitAsync(
			view + WithoutMarks(template),
			compiles: true
		);

		RoslynTestHarness.AssertFixedText(
			view + WithMarksBlank(template),
			result,
			$"View [{view.Trim()}]."
		);
	}

	[TestMethod]
	public void AStatementIsBoundOnceHoweverOftenItIsRead()
	{
		SyntaxTree tree = CSharpSyntaxTree.ParseText(
			"""
			class C
			{
				int _count;

				int Count { get; set; }

				void M()
				{
					_count = 1;
					Count = 2;
					M();
				}
			}
			"""
		);

		SemanticModel model = CSharpCompilation
			.Create(
				"Targets",
				[tree],
				[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]
			)
			.GetSemanticModel(tree);

		StatementReads<BlankLineGrouping.AssignmentTarget> reads =
			BlankLineAssignmentTargets.GetReads(model);

		ExpressionStatementSyntax[] statements =
		[
			.. tree.GetRoot().DescendantNodes().OfType<ExpressionStatementSyntax>(),
		];

		BlankLineGrouping.AssignmentKind[] expected =
		[
			BlankLineGrouping.AssignmentKind.Field,
			BlankLineGrouping.AssignmentKind.Property,
			BlankLineGrouping.AssignmentKind.None,
		];

		for (int read = 0; read < 3; read++)
		{
			BlankLineGrouping.AssignmentKind[] kinds =
			[
				.. statements.Select(statement =>
					reads.Read(statement, CancellationToken.None).Kind
				),
			];

			Assert.AreSequenceEqual(expected, kinds);
		}

		Assert.AreSame(
			reads,
			BlankLineAssignmentTargets.GetReads(model),
			"One model has one cache."
		);

		Assert.AreEqual(
			2,
			reads.Bound,
			"Each of the two assignments is bound once, and the call is never bound."
		);
	}

	private const string TargetsKey = "stanza_assignment_targets";

	private static readonly IReadOnlyDictionary<string, string> s_split = new Dictionary<
		string,
		string
	>
	{
		[TargetsKey] = "split",
	};

	private static Task<AnalyzerRunResult> RunSplitAsync(string source, bool compiles)
	{
		return RoslynTestHarness.RunIterativelyAsync(
			source,

			new RunOptions([new SeparateAssignmentRunsAnalyzer()])
			{
				DiagnosticOptions = s_on,
				AnalyzerConfigOptions = s_split,
				RequireCompiles = compiles,
			}
		);
	}

	private static string Normalize(string text) =>
		text.ReplaceLineEndings("\n").TrimEnd('\n') + "\n";

	private static string WithoutMarks(string template) =>
		string.Join("\n", Normalize(template).Split('\n').Where(static line => line.Trim() != "~"));

	private static string WithMarksBlank(string template)
	{
		return string.Join(
			"\n",

			Normalize(template)
				.Split('\n')
				.Select(static line => line.Trim() == "~" ? string.Empty : line)
		);
	}

	// Wraps statements in a method of a class that declares the fields, the event and the handler they use.
	private static string Method(string body, bool isAsync = false)
	{
		return SourceFixtures.WrapBody(
			body,
			signature: isAsync ? "async System.Threading.Tasks.Task M()" : "void M()",
			members: "\tint a, b, c, d, e, f, g;\n\n\tevent Action Changed;\n\n\tvoid Handler()\n\t{\n\t}\n\n"
		);
	}
}
