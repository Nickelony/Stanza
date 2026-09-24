using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateMultiLineInitializerMembersTests
{
	[TestMethod]
	public async Task SingleLineInitializerMembersStayTight()
	{
		const string source = """
			class C
			{
				void M()
				{
					var point = new Point
					{
						X = 1,
						Y = 2,
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineInitializerMembersAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task MultiLineInitializerMemberGetsSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					var item = new Item
					{
						Name = "a",
						Tags = new List<string>
						{
							"x",
							"y",
						},
						Count = 3,
					};
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var item = new Item
					{
						Name = "a",

						Tags = new List<string>
						{
							"x",
							"y",
						},

						Count = 3,
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineInitializerMembersAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task FirstInitializerMemberIsNotSeparated()
	{
		const string source = """
			class C
			{
				void M()
				{
					var item = new Item
					{
						Tags = new List<string>
						{
							"x",
						},
						Count = 3,
					};
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var item = new Item
					{
						Tags = new List<string>
						{
							"x",
						},

						Count = 3,
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineInitializerMembersAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MultiLineAnonymousObjectMemberGetsSeparated()
	{
		const string source = """
			class C
			{
				object M()
				{
					return new
					{
						Name = "a",
						Tags = new List<string>
						{
							"x",
						},
					};
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				object M()
				{
					return new
					{
						Name = "a",

						Tags = new List<string>
						{
							"x",
						},
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineInitializerMembersAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task WithInitializerMembersGetSeparated()
	{
		const string source = """
			class C
			{
				Rec M(Rec rec)
				{
					return rec with
					{
						Name = "a",
						Tags = new List<string>
						{
							"x",
						},
					};
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				Rec M(Rec rec)
				{
					return rec with
					{
						Name = "a",

						Tags = new List<string>
						{
							"x",
						},
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineInitializerMembersAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
