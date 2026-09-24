using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Finds the brace pair of a container, and where an inserted closing brace goes.</summary>
internal static class StanzaBraces
{
	public static StanzaDiagnosticLookup.TokenPair? FindBracePair(SyntaxNode container)
	{
		(SyntaxToken openBrace, SyntaxToken closeBrace) = container switch
		{
			AnonymousObjectCreationExpressionSyntax anonymousObject => (anonymousObject.OpenBraceToken, anonymousObject.CloseBraceToken),
			AccessorListSyntax accessorList => (accessorList.OpenBraceToken, accessorList.CloseBraceToken),
			BlockSyntax block => (block.OpenBraceToken, block.CloseBraceToken),
			InitializerExpressionSyntax initializer => (initializer.OpenBraceToken, initializer.CloseBraceToken),
			TypeDeclarationSyntax typeDeclaration => (typeDeclaration.OpenBraceToken, typeDeclaration.CloseBraceToken),
			EnumDeclarationSyntax enumDeclaration => (enumDeclaration.OpenBraceToken, enumDeclaration.CloseBraceToken),
			_ => (default, default),
		};

		// A parser-invented token is never measured:
		// a missing token sits at the position it would be inserted at rather than where the author wrote it.
		return !openBrace.IsKind(SyntaxKind.None) && !openBrace.IsMissing && !closeBrace.IsKind(SyntaxKind.None) && !closeBrace.IsMissing
			? new StanzaDiagnosticLookup.TokenPair(openBrace, closeBrace)
			: null;
	}

	public static StanzaDiagnosticLookup.TokenPair? FindBracePair(SyntaxNode root, Diagnostic diagnostic)
	{
		StanzaDiagnosticLookup.TokenPair? pair = StanzaDiagnosticLookup.FindTokenPair(root, diagnostic);

		if (pair is null && root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is { } container)
			pair = FindBracePair(container);

		return pair;
	}

	/// <summary>Returns where an inserted closing brace goes: after the statement's own trailing comment.</summary>
	public static int GetClosingBracePosition(SyntaxToken statementEnd)
	{
		int position = statementEnd.Span.End;
		bool hasComment = false;

		foreach (SyntaxTrivia trivia in statementEnd.TrailingTrivia)
		{
			if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
				return hasComment ? trivia.SpanStart : position;

			if (StanzaTrivia.IsComment(trivia))
			{
				hasComment = true;
				position = trivia.Span.End;
			}
		}

		return position;
	}
}
