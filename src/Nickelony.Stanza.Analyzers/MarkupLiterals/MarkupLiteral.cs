using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Nickelony.Stanza.Markup.Core.Options;

namespace Nickelony.Stanza.Analyzers.MarkupLiterals;

/// <summary>A raw-string markup literal and the file lines its value spans.</summary>
internal readonly record struct MarkupLiteral(SyntaxToken Token, int FirstValueLine, int LastValueLine)
{
	public string Value => Token.ValueText;

	/// <summary>Reads a markup literal the reading picks, or <see langword="null"/> for any other literal.</summary>
	public static MarkupLiteral? Read(SyntaxNodeAnalysisContext context, MarkupLiteralReading reading)
	{
		var literal = (LiteralExpressionSyntax)context.Node;
		SyntaxToken token = literal.Token;

		if (!StanzaKinds.IsMarkupLiteralToken(token))
			return null;

		// The value runs from the line after the opening quotes to the line before the closing quotes,
		// so the closing-quote line holds no value.
		FileLinePositionSpan lineSpan = literal.SyntaxTree.GetLineSpan(token.Span);

		int firstValueLine = lineSpan.StartLinePosition.Line + 1;
		int lastValueLine = lineSpan.EndLinePosition.Line - 1;

		if (lastValueLine < firstValueLine)
			return null;

		bool picked = IsPicked(literal, reading, semanticModel: context.SemanticModel, cancellationToken: context.CancellationToken);
		return picked ? new MarkupLiteral(token, firstValueLine, lastValueLine) : null;
	}

	private static bool IsPicked(
		LiteralExpressionSyntax literal,
		MarkupLiteralReading reading,
		SemanticModel semanticModel,
		CancellationToken cancellationToken
	)
	{
		return reading switch
		{
			MarkupLiteralReading.All => true,
			// The comment needs no binding, so it is read before the attribute.
			MarkupLiteralReading.Marked => XmlLanguageComment.Covers(literal) || XmlStringSyntax.Covers(literal, semanticModel, cancellationToken),
			_ => false,
		};
	}
}
