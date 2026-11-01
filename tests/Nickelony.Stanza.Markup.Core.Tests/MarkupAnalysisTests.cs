using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Markup.Core.Tests;

[TestClass]
public sealed class MarkupAnalysisTests
{
	private static readonly MarkupOptions s_defaults = new(new MarkupPolicy(ContainersOnly: false));

	[TestMethod]
	public void TwoNeighborsOneOfThemSpanningLinesGetAGapWhereNoBlankLineSeparatesThem()
	{
		MarkupFindings findings = Measure("<root>\n  <a>\n    1\n  </a>\n  <b>2</b>\n</root>");

		Assert.AreEqual(
			new MarkupGap(PreviousEndLineIndex: 3, NextStartLineIndex: 4),
			Assert.ContainsSingle(findings.Gaps)
		);

		Assert.IsEmpty(findings.Removals);
	}

	[TestMethod]
	public void TwoSingleLineNeighborsGetNoGap()
	{
		MarkupFindings findings = Measure("<root>\n  <a>1</a>\n  <b>2</b>\n</root>");

		Assert.IsEmpty(findings.Gaps);
		Assert.IsEmpty(findings.Removals);
	}

	[TestMethod]
	public void ABlankLineBesideAMultiLineNeighborGoesToTheMultiLineTightRule()
	{
		MarkupFindings findings = Measure("<root>\n  <a>\n    1\n  </a>\n\n  <b>2</b>\n</root>");

		MarkupRemoval removal = Assert.ContainsSingle(findings.Removals);

		Assert.AreEqual(MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId, removal.Id);
		Assert.AreEqual(0, removal.KeptBlankLines);
		Assert.AreEqual(new MarkupGap(3, 5), removal.Gap);
	}

	[TestMethod]
	public void ABlankLineBetweenSingleLineNeighborsGoesToTheSingleLineTightRule()
	{
		MarkupFindings findings = Measure("<root>\n  <a>1</a>\n\n  <b>2</b>\n</root>");

		MarkupRemoval removal = Assert.ContainsSingle(findings.Removals);

		Assert.AreEqual(MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId, removal.Id);
		Assert.AreEqual(0, removal.KeptBlankLines);
	}

	[TestMethod]
	public void ACommentAboveASingleLineNeighborGetsACommentedGapAtTheFirstLineOfTheComment()
	{
		MarkupFindings findings = Measure(
			"<root>\n  <a>1</a>\n  <!-- one -->\n  <!-- two -->\n  <b>2</b>\n</root>"
		);

		Assert.AreEqual(
			new MarkupGap(PreviousEndLineIndex: 1, NextStartLineIndex: 2),
			Assert.ContainsSingle(findings.CommentedGaps)
		);

		Assert.IsEmpty(findings.Gaps);
		Assert.IsEmpty(findings.Removals);
	}

	[TestMethod]
	public void ACommentedGapStaysWithTheMultiLineRuleBesideAMultiLineNeighbor()
	{
		MarkupFindings findings = Measure(
			"<root>\n  <a>1</a>\n  <!-- one -->\n  <b>\n    2\n  </b>\n</root>"
		);

		Assert.IsEmpty(findings.CommentedGaps);
		Assert.AreEqual(new MarkupGap(1, 2), Assert.ContainsSingle(findings.Gaps));
	}

	[TestMethod]
	[DataRow("<root>\n  <!-- one -->\n  <a>1</a>\n</root>", "the first child")]
	[DataRow("<root>\n  <a>1</a>\n\n  <!-- one -->\n  <b>2</b>\n</root>", "a blank line")]
	[DataRow("<root>\n  <a>1</a> <!-- one -->\n  <b>2</b>\n</root>", "a trailing comment")]
	[DataRow("<root>\n  <a>1</a>\n  <!-- one -->\n\n  <b>2</b>\n</root>", "a detached comment")]
	[DataRow("<root>\n  <a>1</a>\n  <!-- one --> <b>2</b>\n</root>", "a comment on the line")]
	[DataRow("<root>\n  <a>1</a>\n  <b>2</b>\n</root>", "no comment")]
	public void NoCommentedGapStandsWhereTheBlankLineIsNotMissing(string markup, string name) =>
		Assert.IsEmpty(Measure(markup).CommentedGaps, name);

	[TestMethod]
	public void ACommentOnItsOwnLineBelowATrailingCommentGetsACommentedGap()
	{
		MarkupFindings findings = Measure(
			"<root>\n  <a>1</a> <!-- one -->\n  <!-- two -->\n  <b>2</b>\n</root>"
		);

		Assert.AreEqual(
			new MarkupGap(PreviousEndLineIndex: 1, NextStartLineIndex: 2),
			Assert.ContainsSingle(findings.CommentedGaps)
		);
	}

	[TestMethod]
	public void AWrappedLeafGetsNoCommentedGapUnderEitherScope()
	{
		const string markup = "<root>\n  <a>1</a>\n  <!-- one -->\n  <b\n    x=\"1\" />\n</root>";

		MarkupOptions containers = s_defaults with
		{
			Policy = new MarkupPolicy(ContainersOnly: true),
		};

		Assert.IsEmpty(Measure(markup).CommentedGaps);
		Assert.IsEmpty(Measure(markup, containers).CommentedGaps);
	}

	[TestMethod]
	public void AMemberTheReprintOwnsGetsNoCommentedGap()
	{
		const string markup =
			"<doc>\n  <member name=\"M\">\n    <a>1</a>\n    <!-- one -->\n    <b>2</b>\n  </member>\n</doc>";

		Assert.IsNotEmpty(Measure(markup).CommentedGaps);
		Assert.IsEmpty(Measure(markup, s_defaults, skipsMembers: true).CommentedGaps);
	}

	[TestMethod]
	public void ABlankLineAboveACommentGoesToTheSingleLineTightRuleWhereTheCommentedRuleIsEnabled()
	{
		const string markup = "<root>\n  <a>1</a>\n\n  <!-- one -->\n  <b>2</b>\n</root>";

		MarkupOptions both = s_defaults with
		{
			Rules = MarkupRuleSet.From([
				MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId,
				MarkupDiagnostics.SeparateCommentedMarkupElementId,
			]),
		};

		MarkupRemoval removal = Assert.ContainsSingle(Measure(markup, both).Removals);

		Assert.AreEqual(MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId, removal.Id);
		Assert.AreEqual(0, removal.KeptBlankLines);
	}

	[TestMethod]
	public void ACommentedGapStandsNoLongerWhereTheSingleLineTightRuleIsEnabled()
	{
		const string markup = "<root>\n  <a>1</a>\n  <!-- one -->\n  <b>2</b>\n</root>";

		MarkupOptions tight = s_defaults with
		{
			Rules = MarkupRuleSet.From([MarkupDiagnostics.KeepSingleLineMarkupElementsTogetherId]),
		};

		Assert.IsNotEmpty(Measure(markup).CommentedGaps);
		Assert.IsEmpty(Measure(markup, tight).CommentedGaps);
	}

	[TestMethod]
	public void ARunAboveACommentIsCutBackToTheCapWhereOnlyTheCommentedRuleIsEnabled()
	{
		const string markup = "<root>\n  <a>1</a>\n\n\n\n  <!-- one -->\n  <b>2</b>\n</root>";

		MarkupOptions commented = s_defaults with
		{
			Rules = MarkupRuleSet.From([MarkupDiagnostics.SeparateCommentedMarkupElementId]),
		};

		MarkupRemoval cut = Assert.ContainsSingle(Measure(markup, commented).Removals);

		Assert.AreEqual(MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId, cut.Id);
		Assert.AreEqual(1, cut.KeptBlankLines);
	}

	[TestMethod]
	public void AGapAboveTheCapIsCutBackToTheCapUnlessATightRuleIsEnabled()
	{
		const string markup = "<root>\n  <a>\n    1\n  </a>\n\n\n\n  <b>\n    2\n  </b>\n</root>";

		MarkupRemoval cut = Assert.ContainsSingle(Measure(markup).Removals);

		Assert.AreEqual(MarkupDiagnostics.CollapseExtraBlankLinesBetweenMarkupElementsId, cut.Id);
		Assert.AreEqual(1, cut.KeptBlankLines);

		MarkupOptions tight = s_defaults with
		{
			Rules = MarkupRuleSet.From([MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId]),
		};

		MarkupRemoval owned = Assert.ContainsSingle(Measure(markup, tight).Removals);

		Assert.AreEqual(MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId, owned.Id);
		Assert.AreEqual(0, owned.KeptBlankLines);
	}

	[TestMethod]
	public void ACapOfZeroNeverCutsAGapBack()
	{
		MarkupOptions uncapped = s_defaults with { MaxBlankLines = 0 };

		MarkupFindings findings = Measure(
			"<root>\n  <a>\n    1\n  </a>\n\n\n\n  <b>\n    2\n  </b>\n</root>",
			uncapped
		);

		// Only the tight rule owns what the cap leaves, and it removes every blank line.
		MarkupRemoval removal = Assert.ContainsSingle(findings.Removals);

		Assert.AreEqual(MarkupDiagnostics.KeepMultiLineMarkupElementsTogetherId, removal.Id);
	}

	[TestMethod]
	public void ACommentOnTheLineAboveAnElementMovesTheGapToItsFirstLine()
	{
		MarkupFindings findings = Measure(
			"<root>\n  <a>\n    1\n  </a>\n\n  <!-- note -->\n  <b>\n    2\n  </b>\n</root>"
		);

		MarkupRemoval removal = Assert.ContainsSingle(findings.Removals);

		Assert.AreEqual(new MarkupGap(PreviousEndLineIndex: 3, NextStartLineIndex: 5), removal.Gap);
	}

	[TestMethod]
	public void ANeighborOnTheLineOfTheOneBeforeItIsLeftAsWritten()
	{
		MarkupFindings findings = Measure("<root><a>\n1\n</a><b>\n2\n</b></root>");

		Assert.IsEmpty(findings.Gaps);
		Assert.IsEmpty(findings.Removals);
	}

	[TestMethod]
	public void AnElementWithTextBesideItsChildrenIsNotLaidOut()
	{
		MarkupFindings findings = Measure(
			"<root>\ntext\n  <a>\n    1\n  </a>\n  <b>\n    2\n  </b>\n</root>"
		);

		Assert.IsEmpty(findings.Gaps);
	}

	[TestMethod]
	public void AnElementThatPreservesSpaceIsNotLaidOut()
	{
		MarkupFindings findings = Measure(
			"<root xml:space=\"preserve\">\n  <a>\n    1\n  </a>\n  <b>\n    2\n  </b>\n</root>"
		);

		Assert.IsEmpty(findings.Gaps);
	}

	[TestMethod]
	public void APropertyElementTheProfileSkipsKeepsItsChildrenAsWritten()
	{
		const string markup =
			"<Grid>\n  <Grid.Rows>\n    <a>\n      1\n    </a>\n    <b>\n      2\n    </b>\n  </Grid.Rows>\n</Grid>";

		Assert.IsNotEmpty(Measure(markup).Gaps);

		MarkupOptions skipping = s_defaults with
		{
			Policy = new MarkupPolicy(ContainersOnly: false, SkipsPropertyElements: true),
		};

		Assert.IsEmpty(Measure(markup, skipping).Gaps);
	}

	[TestMethod]
	public void AMemberTheReprintOwnsIsSkippedWhenTheMeasureIsToldSo()
	{
		const string markup =
			"<doc>\n  <member name=\"M\">\n    <a>\n      1\n    </a>\n    <b>\n      2\n    </b>\n  </member>\n</doc>";

		Assert.IsNotEmpty(Measure(markup).Gaps);
		Assert.IsEmpty(Measure(markup, s_defaults, skipsMembers: true).Gaps);
	}

	[TestMethod]
	public void ACommentMarkerThatTouchesItsTextIsMeasuredAtBothEnds()
	{
		MarkupFindings findings = Measure("<root>\n  <!--note-->\n</root>");

		Assert.HasCount(2, findings.CommentMarkers);

		Assert.AreEqual(
			new MarkupCommentMarker(LineIndex: 1, MarkerOffset: 2, Length: 4, SpaceOffset: 6),
			findings.CommentMarkers[0]
		);

		Assert.AreEqual(
			new MarkupCommentMarker(LineIndex: 1, MarkerOffset: 10, Length: 3, SpaceOffset: 10),
			findings.CommentMarkers[1]
		);
	}

	[TestMethod]
	public void ACommentMarkerThatAlreadyStandsApartIsLeftAlone()
	{
		Assert.IsEmpty(Measure("<root>\n  <!-- note -->\n</root>").CommentMarkers);
		Assert.IsEmpty(Measure("<root>\n  <!---->\n</root>").CommentMarkers);
	}

	[TestMethod]
	public void ACommentThatPreservesSpaceKeepsItsMarkersAsWritten()
	{
		Assert.IsEmpty(
			Measure("<root xml:space=\"preserve\">\n  <!--note-->\n</root>").CommentMarkers
		);
	}

	[TestMethod]
	public void ACommentInsideASkippedPropertyElementKeepsItsMarkersAsWritten()
	{
		MarkupOptions skipping = s_defaults with
		{
			Policy = new MarkupPolicy(ContainersOnly: false, SkipsPropertyElements: true),
		};

		Assert.IsNotEmpty(Measure("<Grid.Rows>\n  <!--note-->\n</Grid.Rows>").CommentMarkers);

		Assert.IsEmpty(
			Measure(
				"<Grid>\n<Grid.Rows>\n  <!--note-->\n</Grid.Rows>\n</Grid>",
				skipping
			).CommentMarkers
		);
	}

	[TestMethod]
	public void ADocumentOfOnlyCommentsStillHasItsMarkersMeasured()
	{
		MarkupFindings findings = Measure("<!--note-->");

		Assert.IsNull(findings.Root);
		Assert.IsNull(findings.Failure);
		Assert.HasCount(2, findings.CommentMarkers);
	}

	[TestMethod]
	public void ADocumentThatIsNotWellFormedReportsWhereItFailsAndNothingElse()
	{
		MarkupFindings findings = Measure("<root>\n  <a>\n</root>");

		Assert.IsNotNull(findings.Failure);
		Assert.AreEqual(3, findings.Failure.Value.Line);
		Assert.IsNull(findings.Root);
		Assert.IsEmpty(findings.Gaps);
		Assert.IsEmpty(findings.Removals);
		Assert.IsEmpty(findings.CommentMarkers);
	}

	[TestMethod]
	public void ADocumentPastTheBoundIsRefusedBeforeItIsRead()
	{
		MarkupFindings findings = MarkupAnalysis.Measure(
			sourceText: MarkupSourceText.Build("<root><a/></root>"),
			options: s_defaults,
			skipsMembers: false,
			maxDocumentCharacters: 5,
			cancellationToken: CancellationToken.None
		);

		Assert.IsNotNull(findings.Failure);
		Assert.Contains("past the 5", findings.Failure.Value.Reason);
	}

	[TestMethod]
	public void AParseOfOneDocumentServesTwoSetsOfOptions()
	{
		var source = MarkupSourceText.Build(
			"<root>\n  <a>\n    1\n  </a>\n\n\n\n  <b>\n    2\n  </b>\n</root>"
		);

		MarkupParse parse = MarkupAnalysis.Parse(
			source,
			maxDocumentCharacters: 0,
			cancellationToken: CancellationToken.None
		);

		MarkupFindings capped = MarkupAnalysis.Measure(
			sourceText: source,
			parse,
			options: s_defaults,
			skipsMembers: false,
			cancellationToken: CancellationToken.None
		);

		MarkupFindings uncapped = MarkupAnalysis.Measure(
			sourceText: source,
			parse,

			options: s_defaults with
			{
				MaxBlankLines = 5,
			},

			skipsMembers: false,
			cancellationToken: CancellationToken.None
		);

		Assert.AreEqual(1, Assert.ContainsSingle(capped.Removals).KeptBlankLines);
		Assert.AreEqual(0, Assert.ContainsSingle(uncapped.Removals).KeptBlankLines);
		Assert.AreSame(capped.Root, uncapped.Root);
	}

	[TestMethod]
	public void ACanceledMeasureStopsInsteadOfFinishing()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.ThrowsExactly<OperationCanceledException>(() =>
			MarkupAnalysis.Measure(
				sourceText: MarkupSourceText.Build(
					"<root>\n  <a>\n    1\n  </a>\n  <b>2</b>\n</root>"
				),

				options: s_defaults,
				skipsMembers: false,
				maxDocumentCharacters: 0,
				cancellationToken: cancellation.Token
			)
		);
	}

	private static MarkupFindings Measure(
		string markup,
		MarkupOptions? options = null,
		bool skipsMembers = false
	)
	{
		return MarkupAnalysis.Measure(
			sourceText: MarkupSourceText.Build(markup),
			options: options ?? s_defaults,
			skipsMembers,
			maxDocumentCharacters: 0,
			cancellationToken: CancellationToken.None
		);
	}
}
