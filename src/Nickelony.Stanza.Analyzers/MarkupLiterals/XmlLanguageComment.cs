using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

/// <summary>Reads the <c>// language=xml</c> comment that marks a literal as XML, as Rider does.</summary>
internal static class XmlLanguageComment
{
	private const string Xml = "xml";

	/// <summary>The names the comment takes, the longer first, since <c>lang</c> starts <c>language</c>.</summary>
	private static readonly string[] s_names = ["language", "lang"];

	/// <summary>Whether the comment leads the literal, its statement or, outside one, its member.</summary>
	public static bool Covers(LiteralExpressionSyntax literal)
	{
		SyntaxToken token = literal.Token;

		// A comment that ends the line before the literal is trivia of the token before it.
		if (HoldsMark(token.LeadingTrivia) || HoldsMark(token.GetPreviousToken().TrailingTrivia))
			return true;

		SyntaxNode? holder = literal.FirstAncestorOrSelf<StatementSyntax>() ?? (SyntaxNode?)literal.FirstAncestorOrSelf<MemberDeclarationSyntax>();

		return holder is not null && HoldsMark(holder.GetFirstToken().LeadingTrivia);
	}

	private static bool HoldsMark(SyntaxTriviaList trivia)
	{
		foreach (SyntaxTrivia comment in trivia)
		{
			if (ReadCommentText(comment) is { } text && NamesXml(text))
				return true;
		}

		return false;
	}

	/// <summary>Reads the text between the comment's markers, or <see langword="null"/> for other trivia.</summary>
	private static string? ReadCommentText(SyntaxTrivia trivia)
	{
		string text = trivia.ToString();

		if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
			return text.Substring(2);

		// A block comment left open at the end of a file has no closing marker to cut.
		if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) && text.Length >= 4 && text.EndsWith("*/", StringComparison.Ordinal))
			return text.Substring(2, text.Length - 4);

		return null;
	}

	/// <summary>Whether the text is <c>language=xml</c> or <c>lang=xml</c>, in any case and spacing.</summary>
	private static bool NamesXml(string text)
	{
		string body = text.Trim();
		int offset = FindNameEnd(body);

		if (offset < 0)
			return false;

		offset = SkipSpaces(body, offset);

		if (offset >= body.Length || body[offset] != '=')
			return false;

		offset = SkipSpaces(body, offset + 1);

		if (!HoldsWordAt(body, offset, Xml))
			return false;

		// Rider reads further options after the language, such as a prefix, so a space may follow it.
		offset += Xml.Length;
		return offset == body.Length || char.IsWhiteSpace(body[offset]);
	}

	private static int FindNameEnd(string body)
	{
		foreach (string name in s_names)
		{
			if (HoldsWordAt(body, offset: 0, name))
				return name.Length;
		}

		return -1;
	}

	private static bool HoldsWordAt(string body, int offset, string word)
	{
		return string.Compare(
				strA: body,
				indexA: offset,
				strB: word,
				indexB: 0,
				length: word.Length,
				comparisonType: StringComparison.OrdinalIgnoreCase
			) == 0;
	}

	private static int SkipSpaces(string body, int offset)
	{
		while (offset < body.Length && char.IsWhiteSpace(body[offset]))
			offset++;

		return offset;
	}
}
