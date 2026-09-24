using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

/// <summary>A raw-string markup literal and the file lines its value spans.</summary>
internal readonly record struct MarkupLiteral(SyntaxToken Token, int FirstValueLine, int LastValueLine)
{
	public string Value => Token.ValueText;

	/// <summary>Reads a markup literal, or <see langword="null"/> for another literal or an empty value.</summary>
	public static MarkupLiteral? Read(SyntaxNode node)
	{
		SyntaxToken token = ((LiteralExpressionSyntax)node).Token;

		if (!StanzaKinds.IsMarkupLiteralToken(token))
			return null;

		// The value runs from the line after the opening quotes to the line before the closing quotes,
		// so the closing-quote line holds no value.
		FileLinePositionSpan lineSpan = node.SyntaxTree.GetLineSpan(token.Span);

		int firstValueLine = lineSpan.StartLinePosition.Line + 1;
		int lastValueLine = lineSpan.EndLinePosition.Line - 1;

		return lastValueLine < firstValueLine ? null : new MarkupLiteral(token, firstValueLine, lastValueLine);
	}
}
