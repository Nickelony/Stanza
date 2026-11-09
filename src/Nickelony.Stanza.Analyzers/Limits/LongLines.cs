using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Markup.Core;

namespace Nickelony.Stanza.Analyzers.Limits;

/// <summary>Finds the lines that run past a width, apart from the ones that cannot be broken.</summary>
internal static class LongLines
{
	private const string HttpPrefix = "http://";
	private const string HttpsPrefix = "https://";

	/// <summary>What may follow a string that ends a statement, a list, or a sum that goes on below.</summary>
	private const string ClosingPunctuation = ")]};,+";

	/// <summary>Collects the part of each line that runs past the width.</summary>
	/// <remarks>
	/// A documentation comment line is left out, and so is one whose overflow is a closed string or a URL.
	/// </remarks>
	public static ImmutableArray<TextSpan> CollectOverflows(
		SyntaxTree syntaxTree,
		int maxColumns,
		int tabColumns,
		CancellationToken cancellationToken
	)
	{
		ImmutableArray<TextSpan>.Builder overflows = ImmutableArray.CreateBuilder<TextSpan>();
		SourceText sourceText = syntaxTree.GetText(cancellationToken);
		SyntaxNode? root = null;

		foreach (TextLine line in sourceText.Lines)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// A tab takes up to the columns of a tab stop and every other character one, so a short enough line fits unread.
			if ((long)line.Span.Length * Math.Max(tabColumns, 1) <= maxColumns)
				continue;

			string text = sourceText.ToString(line.Span);
			int offset = TextColumns.FindOffsetPastColumn(text, tabColumns, maxColumns);

			if (offset < 0)
				continue;

			// The tree is looked up only for a line that is too long.
			root ??= syntaxTree.GetRoot(cancellationToken);

			if (!IsExcused(root, lineText: text, lineStart: line.Start, offset))
				overflows.Add(TextSpan.FromBounds(line.Start + offset, line.End));
		}

		return overflows.ToImmutable();
	}

	private static bool IsExcused(SyntaxNode root, string lineText, int lineStart, int offset)
	{
		int position = lineStart + offset;

		if (StanzaTrivia.IsDocumentationComment(root.FindTrivia(position)))
			return true;

		SyntaxToken token = root.FindToken(position);

		if (token.Span.Contains(position))
		{
			if (token.SpanStart >= position || !IsUnbreakable(token.Kind()))
				return false;

			// An interpolated string is one piece, so the line has to close it and nothing else after it.
			int end = token.IsKind(SyntaxKind.InterpolatedStringTextToken) ? token.Parent?.Parent?.Span.End ?? token.Span.End : token.Span.End;

			return end >= lineStart + lineText.Length || IsOnlyClosingPunctuation(lineText, from: end - lineStart);
		}

		SyntaxKind trivia = root.FindTrivia(position, findInsideTrivia: true).Kind();

		return trivia is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia && IsInUrl(lineText, offset);
	}

	private static bool IsUnbreakable(SyntaxKind kind)
	{
		return kind
			is SyntaxKind.StringLiteralToken
				or SyntaxKind.SingleLineRawStringLiteralToken
				or SyntaxKind.MultiLineRawStringLiteralToken
				or SyntaxKind.Utf8StringLiteralToken
				or SyntaxKind.Utf8SingleLineRawStringLiteralToken
				or SyntaxKind.Utf8MultiLineRawStringLiteralToken
				or SyntaxKind.InterpolatedStringTextToken;
	}

	/// <summary>Whether a URL opens the run of non-space characters at the offset, and it ends the comment.</summary>
	private static bool IsInUrl(string lineText, int offset)
	{
		int start = offset;

		while (start > 0 && !char.IsWhiteSpace(lineText[start - 1]))
			start--;

		int end = offset;

		while (end < lineText.Length && !char.IsWhiteSpace(lineText[end]))
			end++;

		return start < offset
			&& (HasPrefixAt(lineText, start, HttpPrefix) || HasPrefixAt(lineText, start, HttpsPrefix))
			&& lineText.Substring(end).Trim() is "" or "*/";
	}

	/// <summary>Whether only whitespace and closing punctuation follow the offset.</summary>
	private static bool IsOnlyClosingPunctuation(string lineText, int from)
	{
		for (int index = Math.Max(from, 0); index < lineText.Length; index++)
		{
			if (!char.IsWhiteSpace(lineText[index]) && ClosingPunctuation.IndexOf(lineText[index]) < 0)
				return false;
		}

		return true;
	}

	private static bool HasPrefixAt(string text, int start, string prefix) =>
		string.CompareOrdinal(strA: text, indexA: start, strB: prefix, indexB: 0, length: prefix.Length) == 0;
}
