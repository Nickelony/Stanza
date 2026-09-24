using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

/// <summary>Builds the text changes that add, remove or join the braces of a block.</summary>
internal static class BraceEdits
{
	/// <summary>Adds the braces around a body on their own lines, leaving every existing token unchanged.</summary>
	public static ImmutableArray<TextChange> CreateBraceInsertions(SourceText sourceText, TextSpan headerSpan, StatementSyntax body)
	{
		TextLine bodyLine = sourceText.Lines.GetLineFromPosition(body.SpanStart);

		if (!StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(bodyLine.Start, body.SpanStart)))
			return [];

		if (body.ContainsDirectives)
			return [];

		// The closing brace opens a line of its own,
		// so a body is not wrapped when a following statement shares its last line:
		// the brace and that statement would end up together.
		SyntaxToken bodyEnd = body.GetLastToken();

		if (HasATokenAfterItOnItsLine(sourceText, bodyEnd))
			return [];

		int headerLastLine = sourceText.Lines.GetLineFromPosition(headerSpan.End).LineNumber;

		// A comment above the body travels in with the unit it belongs to.
		StanzaSyntax.LeadingUnit leadingUnit = StanzaSyntax.GetLeadingUnit(body);

		int insertPosition =
			leadingUnit.StartLineIndex < bodyLine.LineNumber && leadingUnit.StartLineIndex > headerLastLine
				? sourceText.Lines[leadingUnit.StartLineIndex].Start
				: bodyLine.Start;

		// The opening brace takes the line breaks between the header and the insert position away,
		// so the gap may hold only whitespace:
		// a comment there that the leading unit does not carry would be deleted.
		if (!StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(sourceText.Lines[headerLastLine].End, insertPosition)))
			return [];

		string lineEnding = StanzaText.ReadPreferredLineEnding(sourceText, bodyLine.LineNumber);
		string indentation = StanzaText.GetIndentation(sourceText, headerSpan.Start);
		string openBraceText = indentation + "{" + lineEnding;
		string closeBraceText = lineEnding + indentation + "}";

		return
		[
			new TextChange(TextSpan.FromBounds(sourceText.Lines[headerLastLine].End, insertPosition), lineEnding + openBraceText),
			new TextChange(new TextSpan(StanzaBraces.GetClosingBracePosition(bodyEnd), 0), closeBraceText),
		];
	}

	private static bool HasATokenAfterItOnItsLine(SourceText sourceText, SyntaxToken token)
	{
		SyntaxToken next = token.GetNextToken();
		return sourceText.Lines.GetLineFromPosition(next.SpanStart).LineNumber == sourceText.Lines.GetLineFromPosition(token.Span.End).LineNumber;
	}

	public static ImmutableArray<TextChange> CreateSafeBraceRemoval(SourceText sourceText, BlockSyntax block, int tabColumns)
	{
		ImmutableArray<TextChange> removal = CreateBraceRemoval(sourceText, block, tabColumns);
		bool canRemove = false;

		if (!removal.IsEmpty && BraceTargets.FindTarget(block) is not null)
			canRemove = !BraceTargets.CanSwallowElse(block.Statements[0]) || !IsFollowedByElseOrDirective(block);

		return canRemove ? removal : [];
	}

	/// <summary>Whether an <see langword="else"/> or a directive follows the block.</summary>
	/// <remarks>
	/// An <see langword="else"/> in an inactive <c>#if</c> is disabled text, not a token.
	/// </remarks>
	private static bool IsFollowedByElseOrDirective(BlockSyntax block)
	{
		SyntaxToken next = block.CloseBraceToken.GetNextToken();

		return next.IsKind(SyntaxKind.ElseKeyword)
			|| next.LeadingTrivia.Any(static trivia => trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia));
	}

	/// <summary>Refuses a removal the compiler needs, or one that makes an <c>else if</c> from a block.</summary>
	/// <remarks>
	/// It also refuses a layout that is not a plain one-statement block.
	/// </remarks>
	private static ImmutableArray<TextChange> CreateBraceRemoval(SourceText sourceText, BlockSyntax block, int tabColumns)
	{
		if (block.Statements.Count != 1)
			return [];

		if (block.Statements[0] is EmptyStatementSyntax)
			return [];

		if (block.Statements[0] is LocalDeclarationStatementSyntax or LabeledStatementSyntax or LocalFunctionStatementSyntax)
			return [];

		if (BraceTargets.IsElseHoldingAnIf(block))
			return [];

		TextLine openLine = sourceText.Lines.GetLineFromPosition(block.OpenBraceToken.SpanStart);
		TextLine closeLine = sourceText.Lines.GetLineFromPosition(block.CloseBraceToken.SpanStart);

		TextLine bodyStartLine = sourceText.Lines.GetLineFromPosition(block.Statements[0].SpanStart);
		TextLine bodyEndLine = sourceText.Lines.GetLineFromPosition(block.Statements[0].Span.End);

		ImmutableArray<TextChange> removal = [];

		if (
			bodyStartLine.LineNumber == openLine.LineNumber + 1
			&& bodyEndLine.LineNumber == closeLine.LineNumber - 1
			&& IsLineAlone(sourceText, openLine, '{')
			&& IsLineAlone(sourceText, closeLine, '}')
			&& IsIndentedDeeper(sourceText, inner: bodyStartLine, outer: openLine, tabColumns)
			&& !block.ContainsDirectives
		)
		{
			removal =
			[
				new TextChange(GetLineWithBreak(sourceText, openLine.LineNumber), string.Empty),
				new TextChange(GetLineWithBreak(sourceText, closeLine.LineNumber), string.Empty),
			];
		}

		return removal;
	}

	private static bool IsLineAlone(SourceText sourceText, TextLine line, char expected)
	{
		int position = StanzaText.SkipWhitespace(sourceText, line.Start, line.End);
		bool opensWithExpected = position < line.End && sourceText[position] == expected;

		return opensWithExpected && StanzaText.SkipWhitespace(sourceText, position + 1, line.End) == line.End;
	}

	private static bool IsIndentedDeeper(SourceText sourceText, TextLine inner, TextLine outer, int tabColumns) =>
		StanzaText.GetIndentationColumns(sourceText, inner, tabColumns) > StanzaText.GetIndentationColumns(sourceText, outer, tabColumns);

	private static TextSpan GetLineWithBreak(SourceText sourceText, int lineIndex)
	{
		TextLine line = sourceText.Lines[lineIndex];

		if (line.EndIncludingLineBreak > line.End)
			return TextSpan.FromBounds(line.Start, line.EndIncludingLineBreak);

		// The last line has no break of its own, so the break before it is taken instead;
		// the first line has none before it either, so it is deleted as it is.
		return lineIndex == 0 ? line.Span : TextSpan.FromBounds(sourceText.Lines[lineIndex - 1].End, line.End);
	}

	public static TextChange? CreateEmptyBlockCollapse(SourceText sourceText, SyntaxToken openBrace, SyntaxToken closeBrace)
	{
		if (!openBrace.IsKind(SyntaxKind.OpenBraceToken) || !closeBrace.IsKind(SyntaxKind.CloseBraceToken))
			return null;

		var gap = TextSpan.FromBounds(openBrace.Span.End, closeBrace.SpanStart);
		return StanzaText.IsWhitespaceOnly(sourceText, gap) && StanzaSyntax.IsMultiLine(sourceText, gap) ? new TextChange(gap, " ") : null;
	}
}
