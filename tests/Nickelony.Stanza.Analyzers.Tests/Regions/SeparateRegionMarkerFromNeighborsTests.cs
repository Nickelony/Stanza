using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Regions;

namespace Nickelony.Stanza.Analyzers.Tests.Regions;

[TestClass]
public sealed class SeparateRegionMarkerFromNeighborsTests
{
	[TestMethod]
	public async Task TheFourGapsOfARegionArePadded()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;
				#region Fields
				private int _count;
				#endregion
				private int _sum;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

				#region Fields

				private int _count;

				#endregion

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ASeparatedRegionIsNotReportedAgain()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;

				#region Fields

				private int _count;

				#endregion

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		Assert.IsEmpty(result.Diagnostics, "A separated region is separated again.");
	}

	[TestMethod]
	public async Task AMarkerIsSeparatedFromTheCodeButNotFromTheBraceOfItsContainer()
	{
		const string source = """
			internal sealed class Sample
			{
				private void M()
				{
					#region Locals
					int value = 1;
					#endregion
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private void M()
				{
					#region Locals

					int value = 1;

					#endregion
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// The brace a marker may touch is its container's; the brace that ends a member before it is a neighbor like any other.
	[TestMethod]
	public async Task AMarkerAfterAMemberBodyIsSeparated()
	{
		const string source = """
			class C
			{
				public void M4() { }
				#region Group
				public void M5() { }
				#endregion
				public void M6() { }
			}
			""";

		const string fixedSource = """
			class C
			{
				public void M4() { }

				#region Group

				public void M5() { }

				#endregion

				public void M6() { }
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ACommentBeforeAMarkerTakesTheBlankLine()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;
				// The counters.
				#region Fields
				private int _count;
				// Kept in sync with the total.
				#endregion
				private int _sum;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

				// The counters.
				#region Fields

				private int _count;

				// Kept in sync with the total.
				#endregion

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A comment a blank line above the marker is no comment of the marker, so the blank line already separates them.
	[TestMethod]
	public async Task ABlankLineBetweenACommentAndAMarkerSeparatesTheMarkerAlready()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;
				// The counters.

				#region Fields

				private int _count;

				#endregion

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task AnEmptyRegionKeepsItsMarkersTogether()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;
				#region Fields
				#endregion
				private int _sum;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;

				#region Fields
				#endregion

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task TwoRegionsThatFollowOneAnotherPadTheirOwnGapsOnly()
	{
		const string source = """
			internal sealed class Sample
			{
				#region First
				private int _count;
				#endregion
				#region Second
				private int _sum;
				#endregion
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				#region First

				private int _count;

				#endregion
				#region Second

				private int _sum;

				#endregion
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionAtTheEdgeOfAFilePadsItsInsideGaps()
	{
		const string source = """
			#region Start
			int value = 1;
			#endregion
			""";

		const string fixedSource = """
			#region Start

			int value = 1;

			#endregion
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		Assert.HasCount(2, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionMarkerFixWorksWithoutTheAdditionalLocation()
	{
		const string source = """
			#region Start

			int value = 1;
			#endregion
			""";

		const string fixedSource = """
			#region Start

			int value = 1;

			#endregion
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(
				source
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionInsideAnInactiveBranchSettlesAfterOneFix()
	{
		const string source = """
			internal sealed class Sample
			{
			#if X
			#region R
				private int _count;
			#endregion
			#endif
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
			#if X
			#region R

				private int _count;

			#endregion
			#endif
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEndRegionAfterDisabledCodeIsPaddedOnce()
	{
		const string source = """
			internal sealed class Sample
			{
			#if X
			#region R
				private int _count;
				private int _sum;
			#endregion
			#endif
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
			#if X
			#region R

				private int _count;
				private int _sum;

			#endregion
			#endif
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMarkerDirectlyBelowASwitchLabelIsNotSeparatedFromTheLabel()
	{
		const string source = """
			internal sealed class Sample
			{
				private void M(int value)
				{
					switch (value)
					{
						case 1:
						#region Work
							DoThing();
						#endregion
							break;
					}
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private void M(int value)
				{
					switch (value)
					{
						case 1:
						#region Work

							DoThing();

						#endregion

							break;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		Assert.HasCount(3, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMarkerBeforeTheLabelOfTheNextSectionIsStillJudgedByItsOwnLines()
	{
		const string source = """
			internal sealed class Sample
			{
				private void M(int value)
				{
					switch (value)
					{
						case 1:
							return;
						#region Work
							DoThing();
						#endregion
						case 2:
							break;
					}
				}
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private void M(int value)
				{
					switch (value)
					{
						case 1:
							return;

						#region Work

							DoThing();

						#endregion

						case 2:
							break;
					}
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		Assert.HasCount(4, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A view where the block is inactive reads the brace from the disabled text, so both views write the same text.
	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AMarkerDirectlyBelowAnOpeningBraceIsNotSeparatedFromItInAnyView(string view)
	{
		string source =
			view
			+ """
				internal sealed class Sample
				{
				#if B
					private void M()
					{
					#region Work
						DoThing();
					#endregion
					}
				#endif
				}
				""";

		string fixedSource =
			view
			+ """
				internal sealed class Sample
				{
				#if B
					private void M()
					{
					#region Work

						DoThing();

					#endregion
					}
				#endif
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AMarkerDirectlyAboveAClosingBraceIsNotSeparatedFromItInAnyView(string view)
	{
		string source =
			view
			+ """
				internal sealed class Sample
				{
				#if B
					private void M()
					{
						DoThing();
					#region Work
						DoThing();
					#endregion
					}
				#endif
				}
				""";

		string fixedSource =
			view
			+ """
				internal sealed class Sample
				{
				#if B
					private void M()
					{
						DoThing();

					#region Work

						DoThing();

					#endregion
					}
				#endif
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A comment after the brace is trivia in the view where the block is active, so the brace still stops the gap.
	[TestMethod]
	[DataRow("", "{ // note")]
	[DataRow("#define B\n", "{ // note")]
	[DataRow("", "{ /* note */")]
	[DataRow("#define B\n", "{ /* note */")]
	public async Task AMarkerBelowABraceWithACommentIsNotSeparatedFromItInAnyView(
		string view,
		string openingLine
	)
	{
		string source =
			view
			+ $$"""
				internal sealed class Sample
				{
				#if B
					private void M()
					{{openingLine}}
					#region Work
						DoThing();
					#endregion
					}
				#endif
				}
				""";

		string fixedSource =
			view
			+ $$"""
				internal sealed class Sample
				{
				#if B
					private void M()
					{{openingLine}}
					#region Work

						DoThing();

					#endregion
					}
				#endif
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("", "case 1: // one")]
	[DataRow("#define B\n", "case 1: // one")]
	[DataRow("", "default: // other")]
	[DataRow("#define B\n", "default: // other")]
	[DataRow("", "case int number\n\t\t\twhen number > 0:")]
	[DataRow("#define B\n", "case int number\n\t\t\twhen number > 0:")]
	public async Task AMarkerBelowASwitchLabelWithACommentIsNotSeparatedFromItInAnyView(
		string view,
		string label
	)
	{
		string source =
			view
			+ $$"""
				internal sealed class Sample
				{
					private void M(int value)
					{
						switch (value)
						{
				#if B
						{{label}}
						#region Work
							DoThing();
						#endregion
							break;
				#endif
						}
					}
				}
				""";

		string fixedSource =
			view
			+ $$"""
				internal sealed class Sample
				{
					private void M(int value)
					{
						switch (value)
						{
				#if B
						{{label}}
						#region Work

							DoThing();

						#endregion

							break;
				#endif
						}
					}
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	// A comment holding a brace is no brace in the view where the block is disabled either.
	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AMarkerBelowACommentHoldingABraceIsSeparatedFromItInAnyView(string view)
	{
		string source =
			view
			+ """
				internal sealed class Sample
				{
				#if B
					private void M()
					{
						DoThing(); // was {
					#region Work
						DoThing();
					#endregion
					}
				#endif
				}
				""";

		string fixedSource =
			view
			+ """
				internal sealed class Sample
				{
				#if B
					private void M()
					{
						DoThing(); // was {

					#region Work

						DoThing();

					#endregion
					}
				#endif
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("#define B\n")]
	public async Task AMarkerDirectlyBelowASwitchLabelIsNotSeparatedFromItInAnyView(string view)
	{
		string source =
			view
			+ """
				internal sealed class Sample
				{
					private void M(int value)
					{
						switch (value)
						{
				#if B
						case 1:
						#region Work
							DoThing();
						#endregion
							break;
				#endif
						}
					}
				}
				""";

		string fixedSource =
			view
			+ """
				internal sealed class Sample
				{
					private void M(int value)
					{
						switch (value)
						{
				#if B
						case 1:
						#region Work

							DoThing();

						#endregion

							break;
				#endif
						}
					}
				}
				""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
