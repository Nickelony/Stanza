using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Checks that a Fix All applies what one fix at a time would.</summary>
[TestClass]
public sealed class FixAllTests
{
	[TestMethod]
	public async Task FixAllSkipsADiagnosticItsProviderDoesNotOwn()
	{
		const string source = """
			class C
			{
				void M()
				{
					var value = 1;
					DoWork();
					DoWork();
					return;
				}

				void DoWork()
				{
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var value = 1;

					DoWork();
					DoWork();

					return;
				}

				void DoWork()
				{
					DoThing();
				}
			}
			""";

		AnalyzerRunResult plain = await RoslynTestHarness.RunFixAllAsync(source);

		AnalyzerRunResult withForeign = await RoslynTestHarness.RunFixAllAsync(
			source,
			foreign: DiagnosticDescriptors.RemoveRedundantArgumentName
		);

		// A source that reports nothing never offers the foreign diagnostic to the provider.
		Assert.IsNotEmpty(plain.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, plain);
		RoslynTestHarness.AssertFixedText(fixedSource, withForeign);
	}

	[TestMethod]
	public async Task FixAllAppliesEveryFixInOnePass()
	{
		const string source = """
			class C
			{
				void M()
				{
					var value = 1;
					DoWork();
					DoWork();
					return;
				}

				void DoWork()
				{
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var value = 1;

					DoWork();
					DoWork();

					return;
				}

				void DoWork()
				{
					DoThing();
				}
			}
			""";

		AnalyzerRunResult fixAll = await RoslynTestHarness.RunFixAllAsync(source);
		AnalyzerRunResult oneByOne = await RoslynTestHarness.RunIterativelyAsync(source);

		Assert.HasCount(2, fixAll.Diagnostics);

		RoslynTestHarness.AssertFixedText(fixedSource, oneByOne);
		RoslynTestHarness.AssertFixedText(fixedSource, fixAll);
	}

	[TestMethod]
	public async Task FixAllSettlesTheBlankLineRulesFromTheOffsetsTheyRecorded()
	{
		const string source = """
			using System;
			class C
			{
				void M()
				{
					var value = 1;
					DoWork();
					DoWork();
					return;
				}
				int _count = 0;

				void N()
				{
					var other = 2;
					DoWork();
					return;
				}

				void DoWork()
				{
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			using System;

			class C
			{
				void M()
				{
					var value = 1;

					DoWork();
					DoWork();

					return;
				}

				int _count = 0;

				void N()
				{
					var other = 2;
					DoWork();
					return;
				}

				void DoWork()
				{
					DoThing();
				}
			}
			""";

		AnalyzerRunResult oneByOne = await RoslynTestHarness.RunIterativelyAsync(source);

		// A fix-all gathers one rule's diagnostics, so each rule takes a pass of its own.
		string text = source;
		int passes = 0;

		for (
			AnalyzerRunResult pass = await RoslynTestHarness.RunFixAllAsync(
				text,
				assertFixedPoint: false
			);
			pass.Diagnostics.Length > 0;
			pass = await RoslynTestHarness.RunFixAllAsync(text, assertFixedPoint: false)
		)
		{
			Assert.IsLessThan(10, ++passes, "the fix-all passes never settled.");
			text = pass.FixedText;
		}

		RoslynTestHarness.AssertFixedText(fixedSource, oneByOne);

		Assert.AreEqual(fixedSource, text);
	}

	[TestMethod]
	public async Task FixAllSettlesTheGapItsOwnFixWouldHaveOpened()
	{
		const string source = """
			class C
			{
				void M(int value)
				{
					if (value > 0)

						return;
					else if (value < 0)
					{
						DoThing();
						return;
					}
					else
						DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M(int value)
				{
					if (value > 0)
					{
						return;
					}
					else if (value < 0)
					{
						DoThing();
						return;
					}
					else
					{
						DoThing();
					}
				}
			}
			""";

		AnalyzerRunResult first = await RoslynTestHarness.RunFixAllAsync(
			source,
			TestRuleSets.StandaloneRulesEnabled
		);

		Assert.HasCount(2, first.Diagnostics);
		Assert.AreEqual(DiagnosticDescriptors.UseBracesConsistently.Id, first.Diagnostics[0].Id);
		Assert.AreEqual(DiagnosticDescriptors.UseBracesConsistently.Id, first.Diagnostics[1].Id);

		RoslynTestHarness.AssertFixedText(fixedSource, first);
	}
}
