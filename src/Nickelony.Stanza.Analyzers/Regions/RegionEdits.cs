using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Regions;

/// <summary>Builds the edits for region and conditional directives, including the regions in inactive code.</summary>
internal static class RegionEdits
{
	public static List<RegionEdit> CreateMarkerGapEdits(SyntaxTree syntaxTree, SourceText sourceText, CancellationToken cancellationToken)
	{
		return CreateGapEdits(
			markers: StanzaCommentCache.GetRegionMarkers(syntaxTree, cancellationToken),
			sourceText,
			readSides: static _ => MarkerSides.Both,
			cancellationToken
		);
	}

	/// <summary>Builds the edits that write a blank line above each <c>#if</c> and below each <c>#endif</c>.</summary>
	public static List<RegionEdit> CreateConditionalBlockGapEdits(SyntaxTree syntaxTree, SourceText sourceText, CancellationToken cancellationToken)
	{
		return CreateGapEdits(
			markers: StanzaCommentCache.GetConditionalBlockMarkers(syntaxTree, cancellationToken),
			sourceText,
			readSides: ReadConditionalBlockSides,
			cancellationToken
		);
	}

	private static List<RegionEdit> CreateGapEdits(
		ImmutableArray<SyntaxTrivia> markers,
		SourceText sourceText,
		Func<SyntaxTrivia, MarkerSides> readSides,
		CancellationToken cancellationToken
	)
	{
		List<RegionEdit> edits = [];

		foreach (SyntaxTrivia trivia in markers)
		{
			cancellationToken.ThrowIfCancellationRequested();

			MarkerSides sides = readSides(trivia);

			if (sides == MarkerSides.None || !TryReadList(trivia, out SyntaxTriviaList list, out int index))
				continue;

			int markerLineIndex = GetLineIndex(sourceText, trivia.SpanStart);
			(int unitLineIndex, int unitIndex) = GetUnitAbove(list, index, sourceText, markerLineIndex);

			if (
				sides.HasFlag(MarkerSides.Above)
				&& FindNeighborAbove(marker: trivia, list, index: unitIndex, sourceText) is { IsBrace: false } above
				&& unitLineIndex - above.LineIndex - 1 == 0
			)
			{
				edits.Add(CreateGapEdit(sourceText, unitLineIndex, above.Span));
			}

			if (
				sides.HasFlag(MarkerSides.Below)
				&& FindNeighborBelow(marker: trivia, list, index, sourceText) is { IsBrace: false } below
				&& below.LineIndex - markerLineIndex - 1 == 0
			)
			{
				edits.Add(CreateGapEdit(sourceText, below.LineIndex, trivia.Span));
			}
		}

		return edits;
	}

	private static MarkerSides ReadConditionalBlockSides(SyntaxTrivia trivia)
	{
		if (trivia.GetStructure() is not DirectiveTriviaSyntax directive || !ConditionalBlocks.IsBetweenUnits(directive))
			return MarkerSides.None;

		return directive is IfDirectiveTriviaSyntax ? MarkerSides.Above : MarkerSides.Below;
	}

	public static List<RegionEdit> CreateEndRegionNameEdits(SyntaxTree syntaxTree, CancellationToken cancellationToken)
	{
		List<RegionEdit> edits = [];

		// The stack matches each closing marker with the innermost open region.
		Stack<RegionDirectiveTriviaSyntax> open = new();

		foreach (SyntaxTrivia trivia in StanzaCommentCache.GetRegionMarkers(syntaxTree, cancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (trivia.GetStructure() is RegionDirectiveTriviaSyntax region)
			{
				open.Push(region);
				continue;
			}

			if (trivia.GetStructure() is not EndRegionDirectiveTriviaSyntax end || open.Count == 0)
				continue;

			RegionDirectiveTriviaSyntax start = open.Pop();

			string name = GetMessage(start, start.RegionKeyword, cancellationToken);
			string closing = GetMessage(end, end.EndRegionKeyword, cancellationToken);

			// The edit writes the start marker's name over the whole closing message.
			// A closing marker that carries a note is left alone, so the note survives.
			// Empty names and names already in place are skipped.
			if (name.Length == 0 || closing == name || HasNote(closing))
				continue;

			edits.Add(
				new RegionEdit(new TextChange(TextSpan.FromBounds(end.EndRegionKeyword.Span.End, end.Span.End), " " + name), end.Span, start.Span)
			);
		}

		return edits;
	}

	/// <summary>Builds the edits that write the condition of an <c>#if</c> after its <c>#endif</c>.</summary>
	public static List<RegionEdit> CreateEndIfNameEdits(
		SyntaxTree syntaxTree,
		SourceText sourceText,
		int minLines,
		CancellationToken cancellationToken
	)
	{
		List<RegionEdit> edits = [];

		// The stack matches each #endif with the innermost open #if, in disabled text as well.
		Stack<IfDirectiveTriviaSyntax> open = new();

		foreach (SyntaxTrivia trivia in StanzaCommentCache.GetConditionalBlockMarkers(syntaxTree, cancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (trivia.GetStructure() is IfDirectiveTriviaSyntax start)
			{
				open.Push(start);
				continue;
			}

			if (trivia.GetStructure() is not EndIfDirectiveTriviaSyntax end || open.Count == 0)
				continue;

			start = open.Pop();

			// A directive in disabled text stays as written, and a view that reads it names it.
			if (!start.IsActive || !end.IsActive)
				continue;

			string name = NormalizeSpaces(start.Condition.ToString());

			if (name.Length == 0 || start.Condition.IsMissing || CountLinesBetween(sourceText, start, end) < minLines)
				continue;

			string closing = sourceText.ToString(TextSpan.FromBounds(end.EndIfKeyword.Span.End, end.Span.End)).Trim();

			// Text on the line is a name or a note, and either stays, as a closing region marker with a note is left alone.
			if (closing.Length > 0)
				continue;

			edits.Add(
				new RegionEdit(new TextChange(TextSpan.FromBounds(end.EndIfKeyword.Span.End, end.Span.End), " // " + name), end.Span, start.Span)
			);
		}

		return edits;
	}

	private static int CountLinesBetween(SourceText sourceText, DirectiveTriviaSyntax start, DirectiveTriviaSyntax end) =>
		GetLineIndex(sourceText, end.SpanStart) - GetLineIndex(sourceText, start.SpanStart) - 1;

	private static string NormalizeSpaces(string text) => string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

	/// <summary>Builds the edit that removes each empty region run and the blank lines beside it.</summary>
	public static List<RegionEdit> CreateEmptyRegionEdits(SyntaxTree syntaxTree, SourceText sourceText, CancellationToken cancellationToken)
	{
		List<RegionEdit> edits = [];

		foreach ((int startLineIndex, int endLineIndex) in FindEmptyRegionRuns(syntaxTree, sourceText, cancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();

			int aboveLineIndex = startLineIndex;

			while (aboveLineIndex > 0 && StanzaText.IsBlankLine(sourceText, aboveLineIndex - 1))
				aboveLineIndex--;

			int belowLineIndex = endLineIndex + 1;

			while (belowLineIndex < sourceText.Lines.Count && StanzaText.IsBlankLine(sourceText, belowLineIndex))
				belowLineIndex++;

			// The blank lines around the run go with it.
			// One blank line stays only where a line remains on both sides,
			// and only if a blank line stood beside the run.
			bool keepsTheLineAbove = aboveLineIndex > 0;
			bool keepsTheLineBelow = belowLineIndex < sourceText.Lines.Count;

			int linesAbove = startLineIndex - aboveLineIndex;
			int linesBelow = belowLineIndex - endLineIndex - 1;

			edits.Add(
				new RegionEdit(
					new TextChange(
						TextSpan.FromBounds(
							keepsTheLineAbove ? sourceText.Lines[aboveLineIndex - 1].EndIncludingLineBreak : 0,
							keepsTheLineBelow ? sourceText.Lines[belowLineIndex].Start : sourceText.Length
						),

						keepsTheLineAbove && keepsTheLineBelow && linesAbove + linesBelow > 0
							? StanzaText.ReadPreferredLineEnding(sourceText, startLineIndex)
							: string.Empty
					),

					TextSpan.FromBounds(sourceText.Lines[startLineIndex].Start, sourceText.Lines[endLineIndex].End),
					null
				)
			);
		}

		return edits;
	}

	/// <summary>Groups empty regions separated only by blank lines into one run.</summary>
	private static List<(int Start, int End)> FindEmptyRegionRuns(SyntaxTree syntaxTree, SourceText sourceText, CancellationToken cancellationToken)
	{
		List<(int Start, int End)> runs = [];

		foreach (SyntaxTrivia trivia in StanzaCommentCache.GetRegionMarkers(syntaxTree, cancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (trivia.GetStructure() is not RegionDirectiveTriviaSyntax)
				continue;

			if (!TryReadList(trivia, out SyntaxTriviaList list, out int index))
				continue;

			if (!TryReadEmptyRegion(list, index, out int endIndex))
				continue;

			int startLineIndex = GetLineIndex(sourceText, trivia.SpanStart);
			int endLineIndex = GetLineIndex(sourceText, list[endIndex].SpanStart);

			// A region starting inside the previous run is already covered by it.
			if (runs.Count > 0 && startLineIndex <= runs[runs.Count - 1].End)
				continue;

			if (runs.Count > 0 && IsBlankBetween(sourceText, runs[runs.Count - 1].End, startLineIndex))
			{
				runs[runs.Count - 1] = (runs[runs.Count - 1].Start, endLineIndex);
				continue;
			}

			runs.Add((startLineIndex, endLineIndex));
		}

		return runs;
	}

	private static bool IsBlankBetween(SourceText sourceText, int endLineIndex, int startLineIndex)
	{
		for (int lineIndex = endLineIndex + 1; lineIndex < startLineIndex; lineIndex++)
		{
			if (!StanzaText.IsBlankLine(sourceText, lineIndex))
				return false;
		}

		return true;
	}

	private static bool TryReadEmptyRegion(SyntaxTriviaList list, int index, out int endIndex)
	{
		int depth = 1;

		for (int triviaIndex = index + 1; triviaIndex < list.Count; triviaIndex++)
		{
			SyntaxTrivia trivia = list[triviaIndex];

			if (IsLayoutOrDocumentationExterior(trivia))
				continue;

			if (trivia.GetStructure() is RegionDirectiveTriviaSyntax)
			{
				depth++;
				continue;
			}

			if (trivia.GetStructure() is EndRegionDirectiveTriviaSyntax)
			{
				depth--;

				if (depth == 0)
				{
					endIndex = triviaIndex;
					return true;
				}

				continue;
			}

			break;
		}

		endIndex = -1;
		return false;
	}

	private static RegionEdit CreateGapEdit(SourceText sourceText, int anchorLineIndex, TextSpan neighbor)
	{
		TextLine line = sourceText.Lines[anchorLineIndex];
		return new RegionEdit(StanzaText.CreateBlankLineAt(sourceText, anchorLineIndex), line.Span, neighbor);
	}

	private static bool TryReadList(SyntaxTrivia marker, out SyntaxTriviaList list, out int index)
	{
		if (marker.SpanStart > marker.Token.SpanStart)
		{
			list = default;
			index = -1;

			return false;
		}

		list = marker.Token.LeadingTrivia;
		index = list.IndexOf(marker);

		return index >= 0;
	}

	private static (int LineIndex, int Index) GetUnitAbove(SyntaxTriviaList list, int index, SourceText sourceText, int markerLineIndex)
	{
		int lineIndex = markerLineIndex;
		int first = index;

		for (int triviaIndex = index - 1; triviaIndex >= 0; triviaIndex--)
		{
			SyntaxTrivia trivia = list[triviaIndex];

			if (IsLayoutOrDocumentationExterior(trivia))
				continue;

			if (!StanzaTrivia.IsComment(trivia) || lineIndex - GetLineIndex(sourceText, trivia.FullSpan.End) > 1)
				break;

			lineIndex = GetLineIndex(sourceText, trivia.FullSpan.Start);
			first = triviaIndex;
		}

		return (lineIndex, first);
	}

	private static RegionNeighbor? FindNeighborAbove(SyntaxTrivia marker, SyntaxTriviaList list, int index, SourceText sourceText)
	{
		for (int triviaIndex = index - 1; triviaIndex >= 0; triviaIndex--)
		{
			SyntaxTrivia trivia = list[triviaIndex];

			if (IsLayoutOrDocumentationExterior(trivia))
				continue;

			if (trivia.IsKind(SyntaxKind.DisabledTextTrivia))
			{
				if (FindDisabledTextLine(sourceText, trivia.FullSpan, fromEnd: true) is { } line)
					return new RegionNeighbor(line.LineNumber, line.Span, isBrace: EndsWithOpeningBraceOrLabel(sourceText.ToString(line.Span)));

				continue;
			}

			return trivia.IsDirective ? null : new RegionNeighbor(GetLineIndex(sourceText, trivia.FullSpan.End), trivia.FullSpan, false);
		}

		SyntaxToken previous = marker.Token.GetPreviousToken();

		return previous.IsKind(SyntaxKind.None)
			? null
			: new RegionNeighbor(
				GetLineIndex(sourceText, previous.Span.End),
				previous.Span,
				isBrace: previous.IsKind(SyntaxKind.OpenBraceToken) || IsSwitchLabelColon(previous)
			);
	}

	private static bool IsSwitchLabelColon(SyntaxToken token) => token.IsKind(SyntaxKind.ColonToken) && token.Parent is SwitchLabelSyntax;

	private static RegionNeighbor? FindNeighborBelow(SyntaxTrivia marker, SyntaxTriviaList list, int index, SourceText sourceText)
	{
		for (int triviaIndex = index + 1; triviaIndex < list.Count; triviaIndex++)
		{
			SyntaxTrivia trivia = list[triviaIndex];

			if (IsLayoutOrDocumentationExterior(trivia))
				continue;

			if (trivia.IsKind(SyntaxKind.DisabledTextTrivia))
			{
				if (FindDisabledTextLine(sourceText, trivia.FullSpan, fromEnd: false) is { } line)
				{
					return new RegionNeighbor(
						line.LineNumber,
						line.Span,
						isBrace: sourceText.ToString(line.Span).TrimStart().StartsWith("}", StringComparison.Ordinal)
					);
				}

				continue;
			}

			return trivia.IsDirective ? null : new RegionNeighbor(GetLineIndex(sourceText, trivia.FullSpan.Start), trivia.FullSpan, false);
		}

		SyntaxToken token = marker.Token;

		return token.IsKind(SyntaxKind.EndOfFileToken)
			? null
			: new RegionNeighbor(GetLineIndex(sourceText, token.SpanStart), token.Span, isBrace: token.IsKind(SyntaxKind.CloseBraceToken));
	}

	/// <summary>
	/// The first or last line of disabled text that holds text, which decides where the neighboring content sits.
	/// </summary>
	private static TextLine? FindDisabledTextLine(SourceText sourceText, TextSpan span, bool fromEnd)
	{
		int firstLineIndex = GetLineIndex(sourceText, span.Start);
		int lastLineIndex = GetLineIndex(sourceText, Math.Max(span.Start, span.End - 1));

		// Disabled text is one trivia that runs through its line breaks, and it absorbs a blank line a fix inserts beside it.
		for (int distance = 0; firstLineIndex + distance <= lastLineIndex; distance++)
		{
			int lineIndex = fromEnd ? lastLineIndex - distance : firstLineIndex + distance;

			if (!StanzaText.IsBlankLine(sourceText, lineIndex))
				return sourceText.Lines[lineIndex];
		}

		return null;
	}

	/// <summary>
	/// Whether the last token of a line of disabled text is an opening brace or the colon of a switch label.
	/// </summary>
	private static bool EndsWithOpeningBraceOrLabel(string line)
	{
		// A comment after the token is trivia in the active view, so it is no token here either.
		SyntaxToken[] tokens = [.. SyntaxFactory.ParseTokens(line).Where(static token => !token.IsKind(SyntaxKind.EndOfFileToken))];

		if (tokens.Length == 0)
			return false;

		SyntaxToken last = tokens[tokens.Length - 1];

		if (last.IsKind(SyntaxKind.OpenBraceToken))
			return true;

		return last.IsKind(SyntaxKind.ColonToken) && OpensALabel(tokens);
	}

	/// <summary>Whether a line opens a label, or ends the one that wraps before its <c>when</c> clause.</summary>
	private static bool OpensALabel(SyntaxToken[] tokens)
	{
		// A label opens with `case`, or is `default` and its colon alone.
		if (tokens[0].IsKind(SyntaxKind.CaseKeyword))
			return true;

		if (tokens[0].IsKind(SyntaxKind.DefaultKeyword))
			return tokens.Length == 2;

		// A lone `when:` names a goto label instead.
		return tokens[0].Text == "when" && tokens.Length > 2;
	}

	private static bool IsLayoutOrDocumentationExterior(SyntaxTrivia trivia) =>
		StanzaTrivia.IsLayout(trivia) || trivia.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia);

	private static string GetMessage(DirectiveTriviaSyntax directive, SyntaxToken keyword, CancellationToken cancellationToken) =>
		directive.SyntaxTree.GetText(cancellationToken).ToString(TextSpan.FromBounds(keyword.Span.End, directive.Span.End)).Trim();

	private static bool HasNote(string message) =>
		message.IndexOf("//", StringComparison.Ordinal) >= 0 || message.IndexOf("/*", StringComparison.Ordinal) >= 0;

	private static int GetLineIndex(SourceText sourceText, int position) => sourceText.Lines.GetLineFromPosition(position).LineNumber;

	/// <summary>The sides of a marker that need a blank line.</summary>
	[Flags]
	private enum MarkerSides
	{
		None = 0,
		Above = 1,
		Below = 2,
		Both = Above | Below,
	}

	/// <summary>
	/// A brace that stops the gap is the container's own: its opening brace above a marker, or its closing one below.
	/// A marker also stops at the switch label above it, which opens the section that holds it.
	/// </summary>
	private readonly struct RegionNeighbor(int lineIndex, TextSpan span, bool isBrace)
	{
		public bool IsBrace { get; } = isBrace;
		public int LineIndex { get; } = lineIndex;
		public TextSpan Span { get; } = span;
	}
}
