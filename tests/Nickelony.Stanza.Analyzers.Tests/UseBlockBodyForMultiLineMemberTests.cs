using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Bodies;
using Nickelony.Stanza.Tests.Shared;

using static Nickelony.Stanza.Analyzers.Tests.BodyFamilyFixtures;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class UseBlockBodyForMultiLineMemberTests
{
	[TestMethod]
	public async Task AMemberWhoseBodyCannotBeIndentedIsLeftAlone()
	{
		const string source = """
			class C
			{
				int M(
					int value) =>
				value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AMemberWhoseAttributeSharesItsLineGetsABlock()
	{
		const string source = """
			class C
			{
				[System.Obsolete] int Pick(bool first) =>
					first ? 1
					: 2;
			}
			""";

		const string fixedSource = """
			class C
			{
				[System.Obsolete] int Pick(bool first)
				{
					return first ? 1
					: 2;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMixedIndentationSignatureStillGivesTheBodyABlock()
	{
		const string source = """
			class C
			{
					int M(
			            int a,
						int b) => a;
			}
			""";

		const string fixedSource = """
			class C
			{
					int M(
			            int a,
						int b)
					{
						return a;
					}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	public static IEnumerable<object?[]> IndentWidths { get; } =
	[
		["no width keys", null],

		[
			"a tab width apart from the indent step",
			new Dictionary<string, string> { ["tab_width"] = "8", ["indent_size"] = "2" },
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(IndentWidths),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AMultiLineBodyBecomesABlock(
		string name,
		IReadOnlyDictionary<string, string>? options
	)
	{
		const string source = """
			class C
			{
				int Pick(bool first) =>
					first ? 1
					: 2;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Pick(bool first)
				{
					return first ? 1
					: 2;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember),
				analyzerConfigOptions: options
			);

		Assert.HasCount(1, result.Diagnostics, name);
		RoslynTestHarness.AssertFixedText(fixedSource, result, name);
	}

	[TestMethod]
	public async Task AnAlignedSignatureLineDoesNotSetTheBodyIndent()
	{
		const string source = """
			class C
			{
			    int V(int a,
			          int b) => a;
			}
			""";

		const string fixedSource = """
			class C
			{
			    int V(int a,
			          int b)
			    {
			        return a;
			    }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// An alignment that lands two steps in still says nothing of where the body belongs, which is one step in.
	[TestMethod]
	public async Task ASignatureLineAlignedOnAWholeStepDoesNotSetTheBodyIndent()
	{
		const string source = """
			class C
			{
			    int Abc(int a,
			            int b) => a;
			}
			""";

		const string fixedSource = """
			class C
			{
			    int Abc(int a,
			            int b)
			    {
			        return a;
			    }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ABodyStartingOnTheSignatureLineBecomesABlock()
	{
		const string source = """
			class C
			{
				int Pick(bool first) => first
					? 1
					: 2;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Pick(bool first)
				{
					return first
					? 1
					: 2;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMultiLineThrowBodyBecomesABlockWithoutAReturn()
	{
		const string source = """
			class C
			{
				int Throw() =>
					throw new InvalidOperationException(
						"This member has no value."
					);
			}
			""";

		const string fixedSource = """
			class C
			{
				int Throw()
				{
					throw new InvalidOperationException(
						"This member has no value."
					);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMultiLineSignatureBecomesABlock()
	{
		const string source = """
			class C
			{
				int Build(
					int first,
					int second
				) => first + second;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Build(
					int first,
					int second
				)
				{
					return first + second;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnArrowAloneOnItsLineKeepsTheArrow()
	{
		const string source = """
			class C
			{
				int Build(int value) =>
					value + 1;

				int Pick() => 2;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AVoidBodyHoldsNoReturnKeyword()
	{
		const string source = """
			class C
			{
				void Run() =>
					Call(
						1,
						2
					);
			}
			""";

		const string fixedSource = """
			class C
			{
				void Run()
				{
					Call(
						1,
						2
					);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AVoidOperatorHoldsNoReturnKeyword()
	{
		RoslynTestHarness.RequireRoslynWithCSharp14();

		const string source = """
			class C
			{
				int _count;

				public void operator +=(C other) =>
					_count += Step(
						other._count
					);

				public void operator ++() =>
					_count = Step(
						_count
					);

				static int Step(int value) => value + 1;
			}
			""";

		const string fixedSource = """
			class C
			{
				int _count;

				public void operator +=(C other)
				{
					_count += Step(
						other._count
					);
				}

				public void operator ++()
				{
					_count = Step(
						_count
					);
				}

				static int Step(int value) => value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheTaskTypeDecidesTheReturnKeyword()
	{
		const string source = """
			using System.Threading.Tasks;

			class C
			{
				async Task Run() =>
					await Task.Delay(
						1
					);

				async Task<int> Load() =>
					await Task.FromResult(
						1
					);
			}
			""";

		const string fixedSource = """
			using System.Threading.Tasks;

			class C
			{
				async Task Run()
				{
					await Task.Delay(
						1
					);
				}

				async Task<int> Load()
				{
					return await Task.FromResult(
						1
					);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARefReturningBodySpellsTheRefOut()
	{
		const string source = """
			class C
			{
				ref int Pick(int index) =>
					ref _values[
						index
					];

				readonly int[] _values = new int[1];
			}
			""";

		const string fixedSource = """
			class C
			{
				ref int Pick(int index)
				{
					return ref _values[
						index
					];
				}

				readonly int[] _values = new int[1];
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AConstructorAnOperatorAndALocalFunctionTakeABlockBody()
	{
		const string source = """
			class C
			{
				C(int value) =>
					_field = new Item
					{
						Count = value,
					};

				public static C operator +(C left, C right) =>
					new C(
						left._field.Count + right._field.Count
					);

				int Count()
				{
					return Local();

					static int Local() =>
						Build(
							1
						);
				}

				readonly Item _field = new();
			}
			""";

		const string fixedSource = """
			class C
			{
				C(int value)
				{
					_field = new Item
					{
						Count = value,
					};
				}

				public static C operator +(C left, C right)
				{
					return new C(
						left._field.Count + right._field.Count
					);
				}

				int Count()
				{
					return Local();

					static int Local()
					{
						return Build(
							1
						);
					}
				}

				readonly Item _field = new();
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentBesideTheArrowKeepsTheArrow()
	{
		const string source = """
			class C
			{
				int Pick(bool first) =>
					// the cheap case
					first ? 1
					: 2;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ALambdaKeepsItsArrow()
	{
		const string source = """
			class C
			{
				void Run()
				{
					Func<int, int> pick = value =>
						value
						+ 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task ACommentAfterTheBodyStaysInsideTheBlock()
	{
		const string source = """
			class C
			{
				int Pick(bool first) =>
					first ? 1
					: 2; // one or two
			}
			""";

		const string fixedSource = """
			class C
			{
				int Pick(bool first)
				{
					return first ? 1
					: 2; // one or two
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnAsyncTaskTypeWrittenThroughAnAliasStillReturnsItsValue()
	{
		const string source = """
			using System.Threading.Tasks;
			using Work = System.Threading.Tasks.Task<int>;

			class C
			{
				int Build(int value) => value;

				async Work Load(
					int value) => Build(value);
			}
			""";

		const string fixedSource = """
			using System.Threading.Tasks;
			using Work = System.Threading.Tasks.Task<int>;

			class C
			{
				int Build(int value) => value;

				async Work Load(
					int value)
				{
					return Build(value);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnAsyncTaskTypeAliasedOutsideTheFileStillReturnsItsValue()
	{
		const string source = """
			class C
			{
				int Build(int value) => value;

				async OutsideWork Load(
					int value) => Build(value);
			}
			""";

		const string fixedSource = """
			class C
			{
				int Build(int value) => value;

				async OutsideWork Load(
					int value)
				{
					return Build(value);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnAsyncTaskOfATypeParameterStillReturnsItsValue()
	{
		const string source = """
			using System.Threading.Tasks;

			class C
			{
				private static T Identity<T>(T value) => value;

				private async Task<T> Load<T>(
					T value) => Identity(value);
			}
			""";

		const string fixedSource = """
			using System.Threading.Tasks;

			class C
			{
				private static T Identity<T>(T value) => value;

				private async Task<T> Load<T>(
					T value)
				{
					return Identity(value);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AConditionalRefBodyKeepsItsRefReturn()
	{
		const string source = """
			class C
			{
				private int _a;
				private int _b;

				public ref int Pick(bool first) =>
					ref (first
						? ref _a
						: ref _b);
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _a;
				private int _b;

				public ref int Pick(bool first)
				{
					return ref (first
						? ref _a
						: ref _b);
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AByReferenceBodyKeepsTheRefItAlreadyCarries()
	{
		const string source = """
			class C
			{
				private int _a;

				public ref int Pick(
					int index) => ref _a;
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _a;

				public ref int Pick(
					int index)
				{
					return ref _a;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AVerbatimStringBodyStaysByteExact()
	{
		const string source = """
			class C
			{
				string M(
					int value) => @"first
					  second";
			}
			""";

		const string fixedSource = """
			class C
			{
				string M(
					int value)
				{
					return @"first
					  second";
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);

		Assert.Contains(
			"@\"first\n",
			result.FixedText.Replace("\r\n", "\n"),
			"The literal itself was rewritten instead of only being wrapped in a block."
		);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task StaticConstructorsDestructorsAndConversionsTakeABlock()
	{
		const string source = """
			class C
			{
				static C() =>
					SetUp(
						1);

				~C() =>
					SetUp(
						1);

				public static implicit operator int(
					C value) => value._field;

				int _field;

				static void SetUp(
					int value)
				{
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				static C()
				{
					SetUp(
						1);
				}

				~C()
				{
					SetUp(
						1);
				}

				public static implicit operator int(
					C value)
				{
					return value._field;
				}

				int _field;

				static void SetUp(
					int value)
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheTriggerDecidesWhatMakesAMemberMultiLine()
	{
		const string source = """
			class C
			{
				int Signature(
					int first,
					int second
				) => first + second;

				int Body(bool first) =>
					first ? 1
					: 2;

				int Both(
					int first,
					int second
				) =>
					first
					+ second;
			}
			""";

		const string bodyOnlyFixed = """
			class C
			{
				int Signature(
					int first,
					int second
				) => first + second;

				int Body(bool first)
				{
					return first ? 1
					: 2;
				}

				int Both(
					int first,
					int second
				)
				{
					return first
					+ second;
				}
			}
			""";

		// A wrapped signature, a wrapped expression, and the two of them together,
		// with their arrows at line indices 5, 7 and 14.
		AnalyzerRunResult byDefault =
			await RoslynTestHarness.ReportAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(3, byDefault.Diagnostics);

		AnalyzerRunResult bodyOnly =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember),
				analyzerConfigOptions: Trigger("body")
			);

		Assert.HasCount(2, bodyOnly.Diagnostics);
		Assert.AreSequenceEqual([7, 14], SortedLines(bodyOnly));

		RoslynTestHarness.AssertFixedText(bodyOnlyFixed, bodyOnly);

		AnalyzerRunResult signatureOnly = await RunStatementWithTriggerAsync(source, "signature");

		Assert.HasCount(2, signatureOnly.Diagnostics);
		Assert.AreSequenceEqual([5, 14], SortedLines(signatureOnly));

		AnalyzerRunResult bothOnly = await RunStatementWithTriggerAsync(source, "both");

		Assert.HasCount(1, bothOnly.Diagnostics);
		Assert.AreSequenceEqual([14], SortedLines(bothOnly));

		// A value that names no trigger leaves the default, so a typo cannot turn the rule off.
		foreach (string unknown in new[] { "everything", "", "bodyish" })
		{
			AnalyzerRunResult unknownValue = await RunStatementWithTriggerAsync(source, unknown);
			Assert.HasCount(3, unknownValue.Diagnostics, $"'{unknown}' must read as the default.");
		}
	}

	[TestMethod]
	public async Task TheTriggerNamesTheUnitsItJudged()
	{
		const string source = """
			class C
			{
				int Both(
					int first,
					int second
				) =>
					first
					+ second;
			}
			""";

		AnalyzerRunResult byDefault =
			await RoslynTestHarness.ReportAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, byDefault.Diagnostics);
		Assert.HasCount(1, byDefault.Diagnostics[0].AdditionalLocations);
		Assert.AreEqual(6, CulpritLine(byDefault, 0));

		AnalyzerRunResult signatureOnly = await RunStatementWithTriggerAsync(source, "signature");

		Assert.HasCount(1, signatureOnly.Diagnostics[0].AdditionalLocations);
		Assert.AreEqual(2, CulpritLine(signatureOnly, 0));

		AnalyzerRunResult bothOnly = await RunStatementWithTriggerAsync(source, "both");

		Assert.HasCount(2, bothOnly.Diagnostics[0].AdditionalLocations);
		Assert.AreEqual(2, CulpritLine(bothOnly, 0));
		Assert.AreEqual(6, CulpritLine(bothOnly, 1));
	}

	private static Task<AnalyzerRunResult> RunStatementWithTriggerAsync(
		string source,
		string trigger
	)
	{
		return RoslynTestHarness.ReportAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember),
			analyzerConfigOptions: Trigger(trigger)
		);
	}

	private static int[] SortedLines(AnalyzerRunResult result) =>
		[.. result.DiagnosticLines.OrderBy(line => line)];

	private static int CulpritLine(AnalyzerRunResult result, int index) =>
		result.Diagnostics[0].AdditionalLocations[index].GetLineSpan().StartLinePosition.Line;

	[TestMethod]
	public async Task FixAllGivesAdjacentAndNestedMembersTheirBlocks()
	{
		const string source = """
			class C
			{
				int Pick(bool first) =>
					first ? 1
					: 2;

				int Other(bool first) =>
					first ? 3
					: 4;

				class D
				{
					int Inner(bool first) =>
						first ? 5
						: 6;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int Pick(bool first)
				{
					return first ? 1
					: 2;
				}

				int Other(bool first)
				{
					return first ? 3
					: 4;
				}

				class D
				{
					int Inner(bool first)
					{
						return first ? 5
						: 6;
					}
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
		);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnUnsafeMemberIsJudgedLikeAnyOther()
	{
		const string source = """
			class C
			{
				unsafe int Pick(
					int* value) => *value;
			}
			""";

		const string fixedSource = """
			class C
			{
				unsafe int Pick(
					int* value)
				{
					return *value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMethodBodyHoldingADirectiveIsLeftAlone()
	{
		const string source = """
			class C
			{
				int Value() =>
					Build(
						1,
			#nullable enable
						2
					);

				int Build(int first, int second)
				{
					return first + second;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineMember)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
