using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Counts the blank lines past a cap in each run that token trivia holds, and removes them.</summary>
internal static class BlankLineSurplus
{
	/// <summary>Collects the span of the lines past the cap in each run of blank lines.</summary>
	/// <remarks>
	/// A comment or a directive ends a run, and text that is not layout, such as a string, holds none.
	/// </remarks>
	public static List<TextSpan> CollectSurplusSpans(SyntaxNode root, SourceText sourceText, int cap, CancellationToken cancellationToken)
	{
		List<TextSpan> spans = [];

		// A file of nothing but whitespace stays as written.
		if (StanzaText.IsWhitespaceOnly(sourceText, new TextSpan(0, sourceText.Length)))
			return spans;

		foreach (SyntaxToken token in root.DescendantTokens())
		{
			cancellationToken.ThrowIfCancellationRequested();

			SyntaxTriviaList leading = token.LeadingTrivia;

			// Every blank line of a run is a line break of its own, so a list with no more than the cap holds no surplus.
			if (leading.Count > cap)
				CollectFromList(leading, sourceText, cap, spans);
		}

		return spans;
	}

	/// <summary>Creates the removal of a span of whole blank lines, or returns <see langword="null"/>.</summary>
	public static TextChange? CreateSurplusRemoval(SourceText sourceText, TextSpan span)
	{
		if (span.IsEmpty || span.End > sourceText.Length)
			return null;

		bool startsALine = sourceText.Lines.GetLineFromPosition(span.Start).Start == span.Start;
		bool endsALine = sourceText.Lines.GetLineFromPosition(span.End).Start == span.End;

		return startsALine && endsALine && StanzaText.IsWhitespaceOnly(sourceText, span) ? new TextChange(span, string.Empty) : null;
	}

	private static void CollectFromList(SyntaxTriviaList leading, SourceText sourceText, int cap, List<TextSpan> spans)
	{
		// A list that opens mid-line is a token on the line of the one before it, which has no blank line to count.
		bool onBlankLine = IsLineStart(sourceText, leading.FullSpan.Start);

		int firstLineIndex = 0;
		int count = 0;

		foreach (SyntaxTrivia trivia in leading)
		{
			if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
				continue;

			if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
			{
				if (onBlankLine)
				{
					firstLineIndex = count == 0 ? sourceText.Lines.GetLineFromPosition(trivia.SpanStart).LineNumber : firstLineIndex;
					count++;
				}
				else
				{
					AddSurplus(spans, sourceText, firstLineIndex, count, cap);
					count = 0;
				}

				onBlankLine = true;
				continue;
			}

			// Any other trivia holds text, so it ends the run, and a directive or a documentation comment takes its own line break.
			AddSurplus(spans, sourceText, firstLineIndex, count, cap);

			count = 0;
			onBlankLine = IsLineStart(sourceText, trivia.FullSpan.End);
		}

		AddSurplus(spans, sourceText, firstLineIndex, count, cap);
	}

	/// <summary>Adds the span of the first lines of a run past the cap, which keeps the last ones.</summary>
	private static void AddSurplus(List<TextSpan> spans, SourceText sourceText, int firstLineIndex, int count, int cap)
	{
		if (count <= cap)
			return;

		int surplus = count - cap;

		spans.Add(TextSpan.FromBounds(sourceText.Lines[firstLineIndex].Start, sourceText.Lines[firstLineIndex + surplus].Start));
	}

	private static bool IsLineStart(SourceText sourceText, int position) => sourceText.Lines.GetLineFromPosition(position).Start == position;
}
