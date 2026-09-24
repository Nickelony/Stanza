using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nickelony.Stanza.Analyzers;

internal static class StanzaTrivia
{
	public static bool IsComment(SyntaxTrivia trivia)
	{
		return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
			|| trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
			|| trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
			|| trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
	}

	public static bool IsDocumentationComment(SyntaxTrivia trivia)
	{
		if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
			return true;

		// A build that writes no documentation file parses `///` and `/**` as plain comments,
		// so in that mode the text tells them apart, by the compiler's own test.
		if (trivia.SyntaxTree?.Options.DocumentationMode != DocumentationMode.None)
			return false;

		string text = trivia.ToString();

		// `///` opens a documentation comment and `////` does not; `/**` does, and `/***` and `/**/` do not.
		return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
			? text.StartsWith("///", StringComparison.Ordinal) && !text.StartsWith("////", StringComparison.Ordinal)
			: trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
				&& text.StartsWith("/**", StringComparison.Ordinal)
				&& !text.StartsWith("/***", StringComparison.Ordinal)
				&& !text.StartsWith("/**/", StringComparison.Ordinal);
	}
}
