using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepMultiLineInitializerMembersTogetherTests
{
	// The pair rows hold an object initializer, so the members of an anonymous object are pinned here.
	[TestMethod]
	public async Task TheBlankLineBetweenTheMembersOfAnAnonymousObjectIsRemoved()
	{
		const string source = """
			class C
			{
				object M()
				{
					return new
					{
						Name = Build(
							1
						),

						Count = Build(
							2
						),
					};
				}

				static int Build(int value) => value;
			}
			""";

		const string fixedSource = """
			class C
			{
				object M()
				{
					return new
					{
						Name = Build(
							1
						),
						Count = Build(
							2
						),
					};
				}

				static int Build(int value) => value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineInitializerMembersTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineInitializerMembersTogether)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MembersThatAlreadyStandTogetherAreLeftAlone()
	{
		const string source = """
			class C
			{
				object M()
				{
					return new
					{
						Name = Build(
							1
						),
						Count = Build(
							2
						),
					};
				}

				static int Build(int value) => value;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineInitializerMembersTogetherAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.KeepMultiLineInitializerMembersTogether)
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
