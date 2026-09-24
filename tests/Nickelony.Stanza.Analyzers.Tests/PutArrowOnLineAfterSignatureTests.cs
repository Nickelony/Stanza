using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Bodies;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class PutArrowOnLineAfterSignatureTests
{
	[TestMethod]
	public async Task ALeadingArrowOpensItsLineWithTheFileIndentationStep()
	{
		const string source = """
			class C
			{
			  int Build(int value) =>
			    value + 1;
			}
			""";

		const string fixedSource = """
			class C
			{
			  int Build(int value)
			    => value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<PutArrowOnLineAfterSignatureAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled,
				analyzerConfigOptions: new Dictionary<string, string> { ["indent_size"] = "2" }
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnArrowAloneOnItsLineTakesTheExpressionAfterIt()
	{
		const string source = """
			class C
			{
				int Build(int value)
					=>
					value + 1;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Build(int value)
					=> value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<PutArrowOnLineAfterSignatureAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnArrowAtTheMembersOwnColumnIsLeftAlone()
	{
		const string source = """
			class C
			{
				int Build(int value)
			    => value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<PutArrowOnLineAfterSignatureAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnExpressionOnItsOwnLineFollowsTheArrow()
	{
		const string source = """
			class C
			{
				int Build(int value) =>
					value + 1;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Build(int value)
					=> value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<PutArrowOnLineAfterSignatureAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnArrowLineBeforeTheMemberIsIndentedAsWell()
	{
		const string source = """
			class C
			{
				int Build(int value)
			=> value + 1;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Build(int value)
					=> value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<PutArrowOnLineAfterSignatureAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AValueMemberOpensItsArrowLineAsWell()
	{
		const string source = """
			class C
			{
				int Count =>
					_value;

				private int _value;
			}
			""";

		const string fixedSource = """
			class C
			{
				int Count
					=> _value;

				private int _value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<PutArrowOnLineAfterSignatureAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AOneLineMemberIsNeverStretched()
	{
		const string source = """
			class C
			{
				int Build(int value) => value + 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<PutArrowOnLineAfterSignatureAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
