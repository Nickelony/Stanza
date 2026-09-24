using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Documentation;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class OrderItemsOfDocumentationListTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> OrderRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.OrderItemsOfDocumentationList);

	[TestMethod]
	public async Task TheHeaderOfAListIsWrittenBeforeItsItems()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="table">
				/// <item>
				/// <term>One</term>
				/// <description>The first.</description>
				/// </item>
				/// <listheader>
				/// <term>Name</term>
				/// <description>Value</description>
				/// </listheader>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="table">
				/// <listheader>
				/// <term>Name</term>
				/// <description>Value</description>
				/// </listheader>
				/// <item>
				/// <term>One</term>
				/// <description>The first.</description>
				/// </item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderItemsOfDocumentationListAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnElementTheOrderDoesNotNameIsNotMoved()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item>Second</item>
				/// <remarks>A note.</remarks>
				/// <item>First</item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderItemsOfDocumentationListAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "a note is not an item of the list.");
	}

	[TestMethod]
	public async Task TheTermOfARowIsWrittenBeforeItsDescription()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="definition">
				/// <item>
				/// <description>The first.</description>
				/// <term>One</term>
				/// </item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="definition">
				/// <item>
				/// <term>One</term>
				/// <description>The first.</description>
				/// </item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderItemsOfDocumentationListAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task EachListOfACommentIsReportedAtItsItemAndFixedOnItsOwn()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="table">
				/// <item>One</item>
				/// <listheader>Name</listheader>
				/// </list>
				/// <list type="table">
				/// <item>Two</item>
				/// <listheader>Other</listheader>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="table">
				/// <listheader>Name</listheader>
				/// <item>One</item>
				/// </list>
				/// <list type="table">
				/// <listheader>Other</listheader>
				/// <item>Two</item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string firstOnly = """
			internal sealed class Sample
			{
				/// <list type="table">
				/// <listheader>Name</listheader>
				/// <item>One</item>
				/// </list>
				/// <list type="table">
				/// <item>Two</item>
				/// <listheader>Other</listheader>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string secondOnly = """
			internal sealed class Sample
			{
				/// <list type="table">
				/// <item>One</item>
				/// <listheader>Name</listheader>
				/// </list>
				/// <list type="table">
				/// <listheader>Other</listheader>
				/// <item>Two</item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderItemsOfDocumentationListAnalyzer>(
				source,
				OrderRuleOnly
			);

		string[] alone = await RoslynTestHarness.ApplyEachFixAloneAsync(
			source,
			DiagnosticDescriptors.OrderItemsOfDocumentationList,
			OrderRuleOnly
		);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
		Assert.AreSequenceEqual([firstOnly, secondOnly], alone);
	}

	[TestMethod]
	public async Task TheRunsAroundANamedElementAreBroughtInOrder()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="table">
				/// <item>One</item>
				/// <listheader>Name</listheader>
				/// <note>Not a row.</note>
				/// <listheader>Other</listheader>
				/// <item>Two</item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="table">
				/// <listheader>Name</listheader>
				/// <item>One</item>
				/// <note>Not a row.</note>
				/// <listheader>Other</listheader>
				/// <item>Two</item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderItemsOfDocumentationListAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task BothLevelsOfAListAreOrderedAtOnce()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="table">
				/// <item>
				/// <term>One</term>
				/// <description>The first.</description>
				/// </item>
				/// <listheader>
				/// <description>Value</description>
				/// <term>Name</term>
				/// </listheader>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				/// <list type="table">
				/// <listheader>
				/// <term>Name</term>
				/// <description>Value</description>
				/// </listheader>
				/// <item>
				/// <term>One</term>
				/// <description>The first.</description>
				/// </item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderItemsOfDocumentationListAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ATagThatSharesALineWithTheRunIsLeftWhereItStands()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="definition">
				/// <item><description>The first.</description>
				/// <term>One</term></item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderItemsOfDocumentationListAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.IsEmpty(
			result.Diagnostics,
			"A tag that shares a line with the run travels with it."
		);
	}

	[TestMethod]
	public async Task ItemsOfTheSameKindKeepTheirOrder()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="bullet">
				/// <item>One.</item>
				/// <item>Two.</item>
				/// <item>Three.</item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderItemsOfDocumentationListAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "A stable run of items is rearranged.");
	}

	[TestMethod]
	public async Task AListASeparatedByABlankLineIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="table">
				/// <item>
				/// <term>One</term>
				/// <description>The first.</description>
				/// </item>
				///
				/// <listheader>
				/// <term>Name</term>
				/// <description>Value</description>
				/// </listheader>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderItemsOfDocumentationListAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "A separated list is ordered.");
	}

	[TestMethod]
	public async Task AListThatHoldsCodeIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="table">
				/// <item>
				/// <term>One</term>
				/// <description><code>var value = Call(1, 2);</code></description>
				/// </item>
				/// <listheader>
				/// <term>Name</term>
				/// <description>Value</description>
				/// </listheader>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderItemsOfDocumentationListAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "A list that holds code is ordered.");
	}

	[TestMethod]
	public async Task ARowWrittenInsideOneLineIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				/// <list type="definition">
				/// <item><description>The first.</description><term>One</term></item>
				/// </list>
				internal int Bind() => 1;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<OrderItemsOfDocumentationListAnalyzer>(
				source,
				OrderRuleOnly
			);

		Assert.IsEmpty(result.Diagnostics, "A row written inside one line is ordered.");
	}
}
