using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Regions;

namespace Nickelony.Stanza.Analyzers.Tests;

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
}
