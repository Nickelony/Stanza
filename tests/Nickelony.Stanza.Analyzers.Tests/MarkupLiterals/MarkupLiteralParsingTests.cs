using System.Collections.Immutable;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.MarkupLiterals;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.Options;
using Nickelony.Stanza.Markup.Core.Xml;

namespace Nickelony.Stanza.Analyzers.Tests.MarkupLiterals;

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
