using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Bodies;

using static Nickelony.Stanza.Analyzers.Tests.BodyFamilyFixtures;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class UseBlockBodyForMultiLineValueMemberTests
{
	[TestMethod]
	public async Task AValueMemberWhoseAttributeSharesItsLineGetsABlock()
	{
		const string source = """
			class C
			{
				[System.Obsolete] int Pick =>
					System.Math.Max(
						1, 2);
			}
			""";

		const string fixedSource = """
			class C
			{
				[System.Obsolete] int Pick
				{
					get
					{
						return System.Math.Max(
						1, 2);
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AGetterOnlyPropertyBecomesABlock()
	{
		const string source = """
			class C
			{
				int Value =>
					1
					+ 2;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Value
				{
					get
					{
						return 1
					+ 2;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AThrowingIndexerBecomesABlockWithoutAReturn()
	{
		const string source = """
			class C
			{
				int this[
					int index
				] => throw new InvalidOperationException();
			}
			""";

		const string fixedSource = """
			class C
			{
				int this[
					int index
				]
				{
					get
					{
						throw new InvalidOperationException();
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnIndexerBecomesABlock()
	{
		const string source = """
			class C
			{
				int this[int index] =>
					Build(
						index
					);
			}
			""";

		const string fixedSource = """
			class C
			{
				int this[int index]
				{
					get
					{
						return Build(
						index
					);
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnAccessorBecomesABlock()
	{
		const string source = """
			class C
			{
				int Value
				{
					get =>
						Build(
							1
						);
				}

				int _field;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Value
				{
					get
					{
						return Build(
							1
						);
					}
				}

				int _field;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnAccessorThatSharesItsLineWithTheMemberKeepsItsExpressionBody()
	{
		const string source = """
			class C
			{
				int Value { get => Build(
					1
				); }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	[DataRow("set")]
	[DataRow("init")]
	public async Task ASetterHoldsNoReturnKeyword(string setter)
	{
		string source = $$"""
			class C
			{
				int Value
				{
					{{setter}} =>
						_field = Build(
							value
						);
				}

				int _field;
			}
			""";

		string fixedSource = $$"""
			class C
			{
				int Value
				{
					{{setter}}
					{
						_field = Build(
							value
						);
					}
				}

				int _field;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ASingleLineValueMemberKeepsItsArrow()
	{
		const string source = """
			class C
			{
				int Value =>
					Build(1);

				int this[int index] =>
					Build(index);

				int Other
				{
					get => 1;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task TheTriggerCoversTheValueHalfAsWell()
	{
		const string source = """
			class C
			{
				string this[
					int index] => "value";

				string Name =>
					_first
					+ _last;

				string _first = "a";

				string _last = "b";
			}
			""";

		const string signatureOnlyFixed = """
			class C
			{
				string this[
					int index]
				{
					get
					{
						return "value";
					}
				}

				string Name =>
					_first
					+ _last;

				string _first = "a";

				string _last = "b";
			}
			""";

		// A wrapped indexer signature and a wrapped property expression,
		// their arrows at line indices 3 and 5.
		AnalyzerRunResult byDefault =
			await RoslynTestHarness.ReportAsync<UseBlockBodyForMultiLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember)
			);

		Assert.HasCount(2, byDefault.Diagnostics);

		AnalyzerRunResult bodyOnly = await RunValueWithTriggerAsync(source, "body");

		Assert.HasCount(1, bodyOnly.Diagnostics);
		Assert.AreEqual(5, bodyOnly.DiagnosticLines[0]);

		AnalyzerRunResult signatureOnly =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember),
				analyzerConfigOptions: Trigger("signature")
			);

		Assert.HasCount(1, signatureOnly.Diagnostics);
		Assert.AreEqual(3, signatureOnly.DiagnosticLines[0]);

		RoslynTestHarness.AssertFixedText(signatureOnlyFixed, signatureOnly);

		AnalyzerRunResult bothOnly = await RunValueWithTriggerAsync(source, "both");

		Assert.IsEmpty(bothOnly.Diagnostics);
	}

	private static Task<AnalyzerRunResult> RunValueWithTriggerAsync(string source, string trigger)
	{
		return RoslynTestHarness.ReportAsync<UseBlockBodyForMultiLineValueMemberAnalyzer>(
			source,
			TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember),
			analyzerConfigOptions: Trigger(trigger)
		);
	}

	// The directive stands inside the expression, past the arrow, so only the directive guard keeps the body as written.
	[TestMethod]
	public async Task ADirectiveInsideTheExpressionLeavesTheBodyAlone()
	{
		const string source = """
			class C
			{
				int Value =>
					Build(
			#if STANZA_UNDEFINED
						1
			#else
						2
			#endif
					);

				int Build(int value)
				{
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember)
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnExpressionBodyHoldingADirectiveIsLeftAlone()
	{
		const string source = """
			class C
			{
				int Value =>
			#if STANZA_UNDEFINED
					Build(
						1
					);
			#else
					Build(
						2
					);
			#endif

				int Build(int value)
				{
					return value;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<UseBlockBodyForMultiLineValueMemberAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
