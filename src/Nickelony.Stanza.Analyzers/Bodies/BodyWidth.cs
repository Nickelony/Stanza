using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Markup.Core;

namespace Nickelony.Stanza.Analyzers.Bodies;

internal static class BodyWidth
{
	/// <summary>The columns an arrow takes on its line, including the space beside it.</summary>
	private const int ArrowColumns = 3;

	public static bool ArrowPaysForItsLine(MultiLineTrigger trigger) => trigger is MultiLineTrigger.Either or MultiLineTrigger.Signature;

	/// <summary>Whether the member fits one line; a bound below one skips the check.</summary>
	public static bool FitsOneLine(SourceText sourceText, TextSpan signatureSpan, string bodyText, StanzaOptions options)
	{
		if (options.MaxLineLength <= 0)
			return true;

		int linePrefixColumns = GetLinePrefixColumns(sourceText, signatureSpan, options.TabColumns);
		int signatureColumns = StanzaText.GetColumns(sourceText, signatureSpan, options.TabColumns);

		// The arrow sits between the signature and the body with a space on each side.
		int bodyStartColumn = linePrefixColumns + signatureColumns + ArrowColumns + 1;

		return bodyStartColumn + MarkupColumns.GetColumns(bodyText, options.TabColumns, bodyStartColumn) <= options.MaxLineLength;
	}

	/// <summary>Whether the signature and continuation lines fit; a bound below one skips the check.</summary>
	public static bool FitsTwoLines(SourceText sourceText, TextSpan signatureSpan, string bodyText, StanzaOptions options, ArrowPlacement placement)
	{
		int maxLineLength = options.MaxLineLength;
		int tabColumns = options.TabColumns;

		if (maxLineLength <= 0)
			return true;

		int linePrefixColumns = GetLinePrefixColumns(sourceText, signatureSpan, tabColumns);
		int indentationColumns = StanzaText.GetIndentationColumns(sourceText, signatureSpan.Start, tabColumns);
		int continuationStepColumns = GetContinuationStepColumns(sourceText, signatureSpan, tabColumns, indentStepColumns: options.IndentStepColumns);
		int signatureColumns = StanzaText.GetColumns(sourceText, signatureSpan, tabColumns);
		int arrowColumns = ArrowPaysForItsLine(options.MultiLineTrigger) ? ArrowColumns : 0;

		int bodyStartColumn = indentationColumns + continuationStepColumns + (placement == ArrowPlacement.Leading ? arrowColumns : 0);

		return linePrefixColumns + signatureColumns + (placement == ArrowPlacement.Trailing ? arrowColumns : 0) <= maxLineLength
			&& bodyStartColumn + MarkupColumns.GetColumns(bodyText, tabColumns, bodyStartColumn) <= maxLineLength;
	}

	private static int GetLinePrefixColumns(SourceText sourceText, TextSpan signatureSpan, int tabColumns)
	{
		TextLine line = sourceText.Lines.GetLineFromPosition(signatureSpan.Start);
		return StanzaText.GetColumns(sourceText, TextSpan.FromBounds(line.Start, signatureSpan.Start), tabColumns);
	}

	private static int GetContinuationStepColumns(SourceText sourceText, TextSpan signatureSpan, int tabColumns, int indentStepColumns)
	{
		// The step is the one StanzaText.WithOneIndentStep writes.
		return StanzaText.IndentsWithTabsAt(sourceText, signatureSpan.Start) ? tabColumns : indentStepColumns;
	}
}
