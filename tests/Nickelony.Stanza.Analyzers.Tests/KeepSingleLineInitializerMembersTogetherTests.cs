using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepSingleLineInitializerMembersTogetherTests
{
	// The pair rows hold an object initializer, so the members of a `with` expression are pinned here.
	[TestMethod]
	public async Task TheBlankLineBetweenTheMembersOfAWithExpressionIsRemoved()
	{
		const string source = """
			record Span(int First, int Second)
			{
				Span Copy()
				{
					return this with
					{
						First = 1,

						Second = 2,
					};
				}
			}
			""";

		const string fixedSource = """
			record Span(int First, int Second)
			{
				Span Copy()
				{
					return this with
					{
						First = 1,
						Second = 2,
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineInitializerMembersTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineInitializerMembersTogether)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TheBlankLineBetweenTheMembersOfAnAnonymousObjectIsRemoved()
	{
		const string source = """
			class C
			{
				object Build()
				{
					return new
					{
						First = 1,

						Second = 2,
					};
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				object Build()
				{
					return new
					{
						First = 1,
						Second = 2,
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineInitializerMembersTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineInitializerMembersTogether)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MembersThatAlreadyStandTogetherAreLeftAlone()
	{
		const string source = """
			record Span(int First, int Second)
			{
				Span Copy()
				{
					return this with
					{
						First = 1,
						Second = 2,
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepSingleLineInitializerMembersTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepSingleLineInitializerMembersTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
