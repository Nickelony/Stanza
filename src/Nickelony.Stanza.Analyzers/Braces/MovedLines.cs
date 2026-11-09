using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

/// <summary>Reads what a fix leaves behind when it moves statements: their comments, indentation and names.</summary>
internal static class MovedLines
{
	/// <summary>Reads the comments of a span, or fails when the span holds more than comments and whitespace.</summary>
	/// <remarks>
	/// The walk visits each child of the node, so the node given is the smallest that holds the span.
	/// </remarks>
	public static bool TryReadComments(SyntaxNode node, TextSpan span, out ImmutableArray<SyntaxTrivia> comments)
	{
		ImmutableArray<SyntaxTrivia>.Builder found = ImmutableArray.CreateBuilder<SyntaxTrivia>();
		comments = [];

		foreach (SyntaxTrivia trivia in node.DescendantTrivia(span))
		{
			if (trivia.SpanStart < span.Start || trivia.Span.End > span.End || StanzaTrivia.IsLayout(trivia))
				continue;

			if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
				return false;

			found.Add(trivia);
		}

		comments = found.ToImmutable();
		return true;
	}

	/// <summary>Finds the end of the comments that trail a token on its line, or the end of the token.</summary>
	public static int FindCommentEnd(SyntaxToken token)
	{
		int end = token.Span.End;

		foreach (SyntaxTrivia trivia in token.TrailingTrivia)
		{
			if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
				break;

			if (!StanzaTrivia.IsLayout(trivia))
				end = trivia.Span.End;
		}

		return end;
	}

	/// <summary>Writes the comments in order, on the anchor's line if they began there and below it if not.</summary>
	/// <remarks>
	/// A line comment ends its line, so a token that follows the text on its line goes to a line of its own.
	/// </remarks>
	public static string BuildCommentText(
		SourceText sourceText,
		ImmutableArray<SyntaxTrivia> comments,
		int anchorLineIndex,
		CommentPlacement placement,
		bool tokenFollows = false
	)
	{
		var builder = new StringBuilder();

		bool blankLineOwed = placement.SeparatesFromAnchor;

		foreach (SyntaxTrivia comment in comments)
		{
			if (sourceText.Lines.GetLineFromPosition(comment.SpanStart).LineNumber == anchorLineIndex)
			{
				builder.Append(' ');
			}
			else
			{
				// The blank line stands above the comments, which lead the unit that follows them.
				if (blankLineOwed)
					builder.Append(placement.LineEnding);

				blankLineOwed = false;
				builder.Append(placement.LineEnding).Append(placement.Indentation);
			}

			builder.Append(ReadDedented(comment.ToString(), placement.Indentation, placement.DedentStep));
		}

		if (blankLineOwed)
			builder.Append(placement.LineEnding);

		if (tokenFollows && EndsInLineComment(comments))
			builder.Append(placement.LineEnding).Append(placement.Indentation);

		return builder.ToString();
	}

	/// <summary>Creates the change that replaces a tail with its comments and moves a following token down.</summary>
	public static TextChange CreateTail(
		SourceText sourceText,
		TextSpan tail,
		ImmutableArray<SyntaxTrivia> comments,
		int anchorLineIndex,
		CommentPlacement placement
	)
	{
		bool tokenFollows = IsFollowedOnItsLine(sourceText, tail.End);
		string text = BuildCommentText(sourceText, comments, anchorLineIndex, placement, tokenFollows);

		return new TextChange(ReadReplacedTail(sourceText, tail, comments, tokenFollows), text);
	}

	/// <summary>Reads a text with its later lines moved out by the step, where they stand that deep.</summary>
	/// <remarks>
	/// A text moved out of a block keeps its first line, which the caller places, and no step leaves it as written.
	/// </remarks>
	public static string ReadDedented(string text, string indentation, string? step)
	{
		if (step is null || text.IndexOf('\n') < 0)
			return text;

		string[] lines = text.Split('\n');
		string prefix = indentation + step;

		for (int index = 1; index < lines.Length; index++)
		{
			if (lines[index].StartsWith(prefix, StringComparison.Ordinal))
				lines[index] = indentation + lines[index].Substring(prefix.Length);
		}

		return string.Join("\n", lines);
	}

	/// <summary>Widens the tail over the blanks before a token that its text moves to a new line.</summary>
	private static TextSpan ReadReplacedTail(SourceText sourceText, TextSpan tail, ImmutableArray<SyntaxTrivia> comments, bool tokenFollows)
	{
		if (!tokenFollows || !EndsInLineComment(comments))
			return tail;

		return TextSpan.FromBounds(tail.Start, StanzaText.SkipBlanksForward(sourceText, tail.End, sourceText.Length));
	}

	private static bool EndsInLineComment(ImmutableArray<SyntaxTrivia> comments) =>
		comments.Length > 0 && comments[comments.Length - 1].IsKind(SyntaxKind.SingleLineCommentTrivia);

	/// <summary>Whether anything but whitespace follows the position on its line.</summary>
	private static bool IsFollowedOnItsLine(SourceText sourceText, int position)
	{
		TextLine line = sourceText.Lines.GetLineFromPosition(position);
		return !StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(position, line.End));
	}

	/// <summary>Reads the statement that a chain of labels stands before.</summary>
	public static StatementSyntax SkipLabels(StatementSyntax statement)
	{
		while (statement is LabeledStatementSyntax labeled)
			statement = labeled.Statement;

		return statement;
	}

	/// <summary>Reads the indent step between two indentations, or nothing when the inner one is no deeper.</summary>
	public static string? FindStep(string outerIndentation, string innerIndentation)
	{
		return innerIndentation.Length > outerIndentation.Length && innerIndentation.StartsWith(outerIndentation, StringComparison.Ordinal)
			? innerIndentation.Substring(outerIndentation.Length)
			: null;
	}

	/// <summary>Builds the changes that take one indent step off each line after the first of a span.</summary>
	/// <remarks>
	/// It declines when a token spans lines, as a string would change, or when a line is not indented by that step.
	/// </remarks>
	public static bool TryCreateDedent(
		SyntaxNode node,
		SourceText sourceText,
		TextSpan span,
		string outerIndentation,
		string step,
		out ImmutableArray<TextChange> changes
	)
	{
		changes = [];

		foreach (SyntaxToken token in node.DescendantTokens(span))
		{
			if (token.Text.IndexOfAny(['\r', '\n']) >= 0)
				return false;
		}

		string prefix = outerIndentation + step;
		ImmutableArray<TextChange>.Builder removals = ImmutableArray.CreateBuilder<TextChange>();

		int firstLineIndex = sourceText.Lines.GetLineFromPosition(span.Start).LineNumber;
		int lastLineIndex = sourceText.Lines.GetLineFromPosition(span.End).LineNumber;

		for (int lineIndex = firstLineIndex + 1; lineIndex <= lastLineIndex; lineIndex++)
		{
			TextLine line = sourceText.Lines[lineIndex];

			if (StanzaText.IsWhitespaceOnly(sourceText, line.Span))
				continue;

			if (line.Span.Length < prefix.Length || !StanzaText.IsExactly(sourceText, new TextSpan(line.Start, prefix.Length), prefix))
				return false;

			removals.Add(new TextChange(new TextSpan(line.Start + outerIndentation.Length, step.Length), string.Empty));
		}

		changes = removals.ToImmutable();
		return true;
	}

	/// <summary>Collects the names a statement declares in its block, which a move would join to another.</summary>
	/// <remarks>
	/// A variable of an expression reaches the block from an expression statement, a jump, an if, a switch or a lock.
	/// </remarks>
	public static void CollectLeakedNames(StatementSyntax statement, HashSet<string> names)
	{
		switch (statement)
		{
			case LocalDeclarationStatementSyntax declaration:
				foreach (VariableDeclaratorSyntax declarator in declaration.Declaration.Variables)
					names.Add(declarator.Identifier.ValueText);

				CollectDesignations(declaration, names);
				break;

			case LocalFunctionStatementSyntax function:
				names.Add(function.Identifier.ValueText);
				break;

			case LabeledStatementSyntax labeled:
				names.Add(labeled.Identifier.ValueText);
				CollectLeakedNames(labeled.Statement, names);
				break;

			case ExpressionStatementSyntax
			or ReturnStatementSyntax
			or ThrowStatementSyntax
			or YieldStatementSyntax
			or GotoStatementSyntax:
				CollectDesignations(statement, names);
				break;

			case IfStatementSyntax ifStatement:
				CollectDesignations(ifStatement.Condition, names);
				break;

			case SwitchStatementSyntax switchStatement:
				CollectDesignations(switchStatement.Expression, names);
				break;

			case LockStatementSyntax lockStatement:
				CollectDesignations(lockStatement.Expression, names);
				break;
		}
	}

	/// <summary>Collects the variables that out arguments, patterns and deconstructions declare.</summary>
	/// <remarks>
	/// A lambda or a local function has a scope of its own, so the search leaves it.
	/// </remarks>
	public static void CollectDesignations(SyntaxNode node, HashSet<string> names)
	{
		foreach (
			SyntaxNode descendant in node.DescendantNodes(static child =>
				child is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
			)
		)
		{
			if (descendant is SingleVariableDesignationSyntax designation)
				names.Add(designation.Identifier.ValueText);
		}
	}

	/// <summary>Whether a token or disabled text of the scope reads a name, outside the spans that may.</summary>
	/// <remarks>
	/// The identifiers of a scope are read once and kept, so a chain of statements in one block costs one read of it.
	/// </remarks>
	public static bool ReusesName(SyntaxNode scope, HashSet<string> names, params TextSpan[] allowed)
	{
		Dictionary<string, List<TextSpan>> identifiers = s_identifiers.GetOrCompute(scope, static scope => ReadIdentifiers(scope));

		foreach (string name in names)
		{
			if (identifiers.TryGetValue(name, out List<TextSpan>? spans) && spans.Any(span => !allowed.Any(range => range.Contains(span))))
				return true;
		}

		return false;
	}

	private static Dictionary<string, List<TextSpan>> ReadIdentifiers(SyntaxNode scope)
	{
		var identifiers = new Dictionary<string, List<TextSpan>>(StringComparer.Ordinal);

		foreach (SyntaxToken token in scope.DescendantTokens())
		{
			if (token.IsKind(SyntaxKind.IdentifierToken))
				AddIdentifier(identifiers, token.ValueText, token.Span);

			// The text of a view that is off holds the same names, and another view binds them.
			foreach (SyntaxTrivia trivia in token.LeadingTrivia)
			{
				if (trivia.IsKind(SyntaxKind.DisabledTextTrivia))
					ReadWords(identifiers, trivia);
			}
		}

		return identifiers;
	}

	private static void ReadWords(Dictionary<string, List<TextSpan>> identifiers, SyntaxTrivia disabledText)
	{
		string text = disabledText.ToString();
		int start = 0;

		for (int offset = 0; offset <= text.Length; offset++)
		{
			if (offset < text.Length && (char.IsLetterOrDigit(text[offset]) || text[offset] == '_'))
				continue;

			if (offset > start)
				AddIdentifier(identifiers, text.Substring(start, offset - start), new TextSpan(disabledText.SpanStart + start, offset - start));

			start = offset + 1;
		}
	}

	private static void AddIdentifier(Dictionary<string, List<TextSpan>> identifiers, string name, TextSpan span)
	{
		if (!identifiers.TryGetValue(name, out List<TextSpan>? spans))
		{
			spans = [];
			identifiers.Add(name, spans);
		}

		spans.Add(span);
	}

	private static readonly WeakCache<SyntaxNode, Dictionary<string, List<TextSpan>>> s_identifiers = new();
}
