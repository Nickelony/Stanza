using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SeparateMultiLineAccessorsTests
{
	[TestMethod]
	public async Task BlockBodiedAccessorsGetSeparated()
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
					get
					{
						return _value;
					}

					set
					{
						_value = value;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineAccessorsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.HasCount(1, result.Diagnostics);

		// The diagnostic anchors on the accessor that needs the blank line before it.
		Assert.AreEqual(10, result.DiagnosticLines[0]);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnInitAccessorIsSeparatedFromTheGetAccessorBeforeIt()
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
			await RoslynTestHarness.RunAsync<SeparateMultiLineAccessorsAnalyzer>(
				source,
				TestRuleSets.Only(DiagnosticDescriptors.SeparateMultiLineAccessors)
			);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AccessorsThatAlreadyStandApartAreLeftAlone()
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

					set
					{
						_value = value;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateMultiLineAccessorsAnalyzer>(
				source,
				TestRuleSets.StandaloneRulesEnabled
			);

		Assert.IsEmpty(result.Diagnostics);
	}
}
