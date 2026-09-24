using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class KeepMultiLineAccessorsTogetherTests
{
	[TestMethod]
	public async Task AMixedAccessorPairIsJoined()
	{
		const string source = """
			class C
			{
				private int _value;

				public int Value
				{
					get { return _value; }

					set
					{
						_value = value;
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _value;

				public int Value
				{
					get { return _value; }
					set
					{
						_value = value;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineAccessorsTogetherAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnInitAccessorStaysWithTheGetAccessorBeforeItUnderTheTightRule()
	{
		const string source = """
			class C
			{
				private int _value;

				public int Value
				{
					get
					{
						return _value;
					}

					init
					{
						_value = value;
					}
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				private int _value;

				public int Value
				{
					get
					{
						return _value;
					}
					init
					{
						_value = value;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineAccessorsTogetherAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AccessorsThatAlreadyStandTogetherAreLeftAlone()
	{
		const string source = """
			class C
			{
				private int _value;

				public int Value
				{
					get { return _value; }
					set
					{
						_value = value;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<KeepMultiLineAccessorsTogetherAnalyzer>(
				source,
				TestRuleSets.GapRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
