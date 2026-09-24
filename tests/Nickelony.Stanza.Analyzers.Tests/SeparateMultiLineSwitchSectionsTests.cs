using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateMultiLineSwitchSectionsTests
{
	[TestMethod]
	public async Task SingleLineSwitchSectionsStayTight()
	{
		const string source = """
			class C
			{
				int M(int value)
				{
					switch (value)
					{
						case 1: return 1;
						case 2: return 2;
						default: return 0;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineSwitchSectionsAnalyzer>(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task MultiLineSwitchSectionsGetSeparated()
	{
		const string source = """
			class C
			{
				int M(int value)
				{
					switch (value)
					{
						case 1:
							return 1;
						case 2:
							return 2;
					}

					return 0;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int M(int value)
				{
					switch (value)
					{
						case 1:
							return 1;

						case 2:
							return 2;
					}

					return 0;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineSwitchSectionsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SingleLineSectionAfterMultiLineSectionGetsSeparated()
	{
		const string source = """
			class C
			{
				int M(int value)
				{
					switch (value)
					{
						case 1:
							return 1;
						default: return 0;
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int M(int value)
				{
					switch (value)
					{
						case 1:
							return 1;

						default: return 0;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineSwitchSectionsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AttachedCommentMovesWithTheNextSwitchSection()
	{
		const string source = """
			class C
			{
				int M(int value)
				{
					switch (value)
					{
						case 1:
							return 1;
						// second
						case 2:
							return 2;
					}

					return 0;
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				int M(int value)
				{
					switch (value)
					{
						case 1:
							return 1;

						// second
						case 2:
							return 2;
					}

					return 0;
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineSwitchSectionsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
