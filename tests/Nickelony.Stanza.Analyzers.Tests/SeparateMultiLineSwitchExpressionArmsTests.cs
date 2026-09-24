using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateMultiLineSwitchExpressionArmsTests
{
	[TestMethod]
	public async Task SingleLineSwitchExpressionArmsStayTight()
	{
		const string source = """
			class C
			{
				string M(int value)
				{
					return value switch
					{
						0 => "zero",
						int n when n > 0 => "positive",
						_ => throw new ArgumentOutOfRangeException(nameof(value)),
					};
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineSwitchExpressionArmsAnalyzer>(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task MultiLineSwitchExpressionArmGetsSeparated()
	{
		const string source = """
			class C
			{
				string M(int value)
				{
					return value switch
					{
						1 => "one",
						2 => Build(
							value
						),
						_ => "other",
					};
				}

				string Build(int value)
				{
					return value.ToString();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				string M(int value)
				{
					return value switch
					{
						1 => "one",

						2 => Build(
							value
						),

						_ => "other",
					};
				}

				string Build(int value)
				{
					return value.ToString();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineSwitchExpressionArmsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task MultiLineSwitchExpressionArmTableGetsSeparated()
	{
		const string source = """
			class C
			{
				string M(int value)
				{
					return value switch
					{
						1 => Build(
							value
						),
						2 => Build(
							value
						),
					};
				}

				string Build(int value)
				{
					return value.ToString();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				string M(int value)
				{
					return value switch
					{
						1 => Build(
							value
						),

						2 => Build(
							value
						),
					};
				}

				string Build(int value)
				{
					return value.ToString();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineSwitchExpressionArmsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AttachedCommentMovesWithTheNextSwitchExpressionArm()
	{
		const string source = """
			class C
			{
				string M(int value)
				{
					return value switch
					{
						1 => Build(
							value
						),
						// fallback
						_ => "other",
					};
				}

				string Build(int value)
				{
					return value.ToString();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				string M(int value)
				{
					return value switch
					{
						1 => Build(
							value
						),

						// fallback
						_ => "other",
					};
				}

				string Build(int value)
				{
					return value.ToString();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineSwitchExpressionArmsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task SameLineSwitchExpressionArmsAreSeparated()
	{
		const string source = """
			class C
			{
				string M(int value)
				{
					return value switch
					{
						1 => Build(
							value
						), 2 => "two",
					};
				}

				string Build(int value)
				{
					return value.ToString();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				string M(int value)
				{
					return value switch
					{
						1 => Build(
							value
						),

						2 => "two",
					};
				}

				string Build(int value)
				{
					return value.ToString();
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineSwitchExpressionArmsAnalyzer>(source);

		Assert.HasCount(1, result.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
