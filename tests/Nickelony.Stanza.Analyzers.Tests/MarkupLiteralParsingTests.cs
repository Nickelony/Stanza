using System.Collections.Immutable;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.MarkupLiterals;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class MarkupLiteralParsingTests
{
	private static MarkupOptions Scope(bool containersOnly = false) =>
		new(new MarkupPolicy(containersOnly));

	private static MarkupOptions Budget(int maxBlankLines) => new(default, maxBlankLines);

	// Roslyn counts a line separator as a line break inside a value line, so the file line runs ahead of the value line there.
	[TestMethod]
	public void EachValueLineMapsToTheFileLineItStartsOn()
	{
		var lines = new MarkupValueLines("a\r\nb\u2028c\nd");

		Assert.AreEqual(0, lines.GetFileLineIndex(0));
		Assert.AreEqual(1, lines.GetFileLineIndex(1));
		Assert.AreEqual(3, lines.GetFileLineIndex(2));
		Assert.AreEqual(3, lines.GetFileLineIndex(5));

		Assert.AreEqual(
			(2, 5),
			lines.GetFileLineAndOffset(lineIndex: 1, offsetInLine: 3, indentationLength: 4)
		);
	}

	[TestMethod]
	public void AGapBesideAWrappedLeafKeepsItsBlankLineUnderContainers()
	{
		const string wrapped = "<root>\n  <child\n    prop=\"1\" />\n\n  <child />\n</root>\n";
		const string singleLine = "<root>\n  <child />\n\n  <child />\n</root>\n";

		Assert.IsEmpty(
			MarkupLiteralAnalysis.ReadRemovals(
				text: wrapped,
				markup: Scope(containersOnly: true),
				id: MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId,
				cancellationToken: CancellationToken.None
			)
		);

		Assert.HasCount(
			1,

			MarkupLiteralAnalysis.ReadRemovals(
				text: singleLine,
				markup: Scope(containersOnly: true),
				id: MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId,
				cancellationToken: CancellationToken.None
			)
		);
	}

	[TestMethod]
	public void CDataCountsAsTextOfItsOwn()
	{
		const string withCData =
			"<root>\n  <![CDATA[text]]>\n  <a>\n    one\n  </a>\n  <b>\n    two\n  </b>\n</root>\n";

		const string withoutCData =
			"<root>\n  <a>\n    one\n  </a>\n  <b>\n    two\n  </b>\n</root>\n";

		Assert.IsEmpty(MarkupLiteralAnalysis.ReadGaps(withCData, Scope(), CancellationToken.None));

		Assert.HasCount(
			1,
			MarkupLiteralAnalysis.ReadGaps(withoutCData, Scope(), CancellationToken.None)
		);
	}

	[TestMethod]
	public void AnInnerDefaultBringsTheLayoutBackUnderPreservedSpace()
	{
		const string preserved =
			"<root xml:space=\"preserve\">\n  <a>\n    one\n  </a>\n  <b>\n    two\n  </b>\n</root>\n";

		Assert.IsEmpty(MarkupLiteralAnalysis.ReadGaps(preserved, Scope(), CancellationToken.None));

		const string reopened =
			"<root xml:space=\"preserve\">\n  <group xml:space=\"default\">\n    <a>\n      one\n    </a>\n    <b>\n      two\n    </b>\n  </group>\n</root>\n";

		ImmutableArray<MarkupGap> gaps = MarkupLiteralAnalysis.ReadGaps(
			reopened,
			Scope(),
			CancellationToken.None
		);

		Assert.HasCount(1, gaps);
		Assert.AreEqual(4, gaps[0].PreviousEndLineIndex);
		Assert.AreEqual(5, gaps[0].NextStartLineIndex);
	}

	[TestMethod]
	public void TheElementsThatShareALineAreLeftAlone()
	{
		const string markup = "<root>\n  <a>\n    one\n  </a><b>\n    two\n  </b>\n</root>\n";
		Assert.IsEmpty(MarkupLiteralAnalysis.ReadGaps(markup, Scope(), CancellationToken.None));
	}

	[TestMethod]
	public void ATextThatIsNotAWholeDocumentIsLeftAlone()
	{
		Assert.IsEmpty(
			MarkupLiteralAnalysis.ReadGaps(
				"stanza-markup <command> [options] <path>\n",
				Scope(),
				CancellationToken.None
			)
		);
	}

	[TestMethod]
	public void ARunOfBlankLinesBeyondTheBudgetIsReported()
	{
		const string markup = "<root>\n  <a>1</a>\n\n\n  <b>2</b>\n</root>\n";

		ImmutableArray<MarkupRemoval> removals = MarkupLiteralAnalysis.ReadRemovals(
			text: markup,
			markup: Budget(1),
			id: MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
			cancellationToken: CancellationToken.None
		);

		Assert.HasCount(1, removals);
		Assert.AreEqual(1, removals[0].Gap.PreviousEndLineIndex);
		Assert.AreEqual(4, removals[0].Gap.NextStartLineIndex);

		Assert.IsEmpty(MarkupLiteralAnalysis.ReadGaps(markup, Scope(), CancellationToken.None));
	}

	[TestMethod]
	public void ARunThatFitsTheBudgetIsLeftAlone()
	{
		const string markup = "<root>\n  <a>1</a>\n\n  <b>2</b>\n</root>\n";

		Assert.IsEmpty(
			MarkupLiteralAnalysis.ReadRemovals(
				text: markup,
				markup: Budget(1),
				id: MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
				cancellationToken: CancellationToken.None
			)
		);

		// A cap of zero cuts no gap back, so it reports no blank-line run.
		Assert.IsEmpty(
			MarkupLiteralAnalysis.ReadRemovals(
				text: markup,
				markup: Budget(0),
				id: MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
				cancellationToken: CancellationToken.None
			)
		);
	}

	[TestMethod]
	public void AUnitUnderPreservedSpaceKeepsItsRuns()
	{
		const string preserved =
			"<root xml:space=\"preserve\">\n  <a>1</a>\n\n\n  <b>2</b>\n</root>\n";

		Assert.IsEmpty(
			MarkupLiteralAnalysis.ReadRemovals(
				text: preserved,
				markup: Budget(1),
				id: MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
				cancellationToken: CancellationToken.None
			)
		);

		const string wrapped = "<root>\n  <a\n    prop=\"1\" />\n\n\n  <b />\n</root>\n";

		Assert.HasCount(
			1,

			MarkupLiteralAnalysis.ReadRemovals(
				text: wrapped,
				markup: Budget(1),
				id: MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
				cancellationToken: CancellationToken.None
			)
		);
	}

	[TestMethod]
	public void TheCapOwnsARunAboveTheBudgetAndTheTightRuleTheRest()
	{
		const string markup = "<root>\n  <a>1</a>\n\n\n  <b>2</b>\n</root>\n";

		Assert.HasCount(
			1,
			Removals(markup, 1, MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId)
		);

		Assert.IsEmpty(
			Removals(markup, 1, MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId)
		);

		Assert.HasCount(
			1,
			Removals(markup, 2, MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId)
		);

		Assert.IsEmpty(
			Removals(markup, 2, MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId)
		);
	}

	[TestMethod]
	public void AnEnabledTightRuleOwnsTheWholeRunAboveTheBudget()
	{
		const string markup = "<root>\n  <a>1</a>\n\n\n  <b>2</b>\n</root>\n";

		MarkupOptions options = Budget(1) with
		{
			Rules = MarkupRuleSet.From([MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId]),
		};

		ImmutableArray<MarkupRemoval> removals = MarkupLiteralAnalysis.ReadRemovals(
			text: markup,
			markup: options,
			id: MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId,
			cancellationToken: CancellationToken.None
		);

		Assert.HasCount(1, removals);
		Assert.AreEqual(0, removals[0].KeptBlankLines);

		Assert.IsEmpty(
			MarkupLiteralAnalysis.ReadRemovals(
				text: markup,
				markup: options,
				id: MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId,
				cancellationToken: CancellationToken.None
			)
		);
	}

	private static ImmutableArray<MarkupRemoval> Removals(string text, int maxBlankLines, string id)
	{
		return MarkupLiteralAnalysis.ReadRemovals(
			text,
			markup: Budget(maxBlankLines),
			id,
			cancellationToken: CancellationToken.None
		);
	}
}
