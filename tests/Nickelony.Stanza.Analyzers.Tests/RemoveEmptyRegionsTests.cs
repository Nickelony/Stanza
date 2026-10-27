using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Regions;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class RemoveEmptyRegionsTests
{
	private static IReadOnlyDictionary<string, ReportDiagnostic> EmptyRuleOnly { get; } =
		TestRuleSets.Only(DiagnosticDescriptors.RemoveEmptyRegions);

	[TestMethod]
	public async Task AnEmptyRegionIsTakenAway()
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

				private int _sum;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveEmptyRegionsAnalyzer>(
			source,
			EmptyRuleOnly
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);

		// The change rides the diagnostic, so a Fix All never finds the file's edits once per diagnostic.
		Assert.IsNotNull(FixOptions.Parse(result.Diagnostics[0]).Change);
	}

	[TestMethod]
	public async Task ATightEmptyRegionLeavesNoBlankLineBehind()
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
				private int _sum;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveEmptyRegionsAnalyzer>(
			source,
			EmptyRuleOnly
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionThatHoldsANoteIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				#region Fields
				// TODO: fill this in.
				#endregion
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveEmptyRegionsAnalyzer>(
			source,
			EmptyRuleOnly
		);

		Assert.IsEmpty(result.Diagnostics, "A region that holds a note is taken away.");
	}

	[TestMethod]
	public async Task ARegionThatHoldsCodeIsLeftAlone()
	{
		const string source = """
			internal sealed class Sample
			{
				#region Fields
				private int _count;
				#endregion
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveEmptyRegionsAnalyzer>(
			source,
			EmptyRuleOnly
		);

		Assert.IsEmpty(result.Diagnostics);
	}

	[TestMethod]
	public async Task NestedEmptyRegionsAreTakenAwayAtOnce()
	{
		const string source = """
			internal sealed class Sample
			{
				#region Outer
				#region Inner
				#endregion
				#endregion
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
			}
			""";

		// The regions go and leave an empty body, which the default rule ZA0037 collapses in its own fix.
		using IDisposable skipped = RoslynTestHarness.SkippingDefaultRules();

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveEmptyRegionsAnalyzer>(
			source,
			EmptyRuleOnly
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionThatHoldsAFullRegionIsKept()
	{
		const string source = """
			internal sealed class Sample
			{
				#region Outer
				#region Inner
				#endregion
				private int _count;
				#endregion
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				#region Outer
				private int _count;
				#endregion
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveEmptyRegionsAnalyzer>(
			source,
			EmptyRuleOnly
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEmptyRegionFixWorksWithoutTheAdditionalLocation()
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

				private int _sum;
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunFixWithoutAdditionalLocationsAsync<RemoveEmptyRegionsAnalyzer>(
				source,
				EmptyRuleOnly
			);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task ARegionThatIsTheWholeFileLeavesNothingBehind()
	{
		const string source = """
			#region Fields
			#endregion
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveEmptyRegionsAnalyzer>(
			source,
			EmptyRuleOnly
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(string.Empty, result);
	}

	[TestMethod]
	public async Task TwoEmptyRegionsBesideABlankLineAreTakenAwayTogether()
	{
		const string source = """
			#region First
			#endregion First

			#region Second
			#endregion Second
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, EmptyRuleOnly);

		Assert.HasCount(
			1,
			result.Diagnostics,
			"The two regions share one run, so one diagnostic removes both."
		);

		RoslynTestHarness.AssertFixedText(string.Empty, result);
	}

	[TestMethod]
	public async Task TwoEmptyRegionsWithCodeBetweenThemAreTwoRuns()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _first;

				#region First
				#endregion First

				private int _total;

				#region Second
				#endregion Second

				private int _last;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _first;

				private int _total;

				private int _last;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunFixAllAsync(source, EmptyRuleOnly);

		Assert.HasCount(2, result.Diagnostics, "Code between two regions keeps them from one run.");
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEmptyRegionAtTheStartOfTheFileLeavesNoBlankLineAtTheTop()
	{
		const string source = """
			#region Header
			#endregion

			internal sealed class Sample
			{
				private int _total;
			}
			""";

		const string fixedSource = """
			internal sealed class Sample
			{
				private int _total;
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveEmptyRegionsAnalyzer>(
			source,
			EmptyRuleOnly
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AnEmptyRegionAtTheEndOfTheFileLeavesNoBlankLineAtTheBottom()
	{
		const string source = """
			internal sealed class Sample
			{
				private int _total;
			}

			#region Footer
			#endregion
			""";

		const string fixedSource =
			"""
				internal sealed class Sample
				{
					private int _total;
				}
				""" + "\r\n";

		AnalyzerRunResult result = await RoslynTestHarness.RunAsync<RemoveEmptyRegionsAnalyzer>(
			source,
			EmptyRuleOnly
		);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
