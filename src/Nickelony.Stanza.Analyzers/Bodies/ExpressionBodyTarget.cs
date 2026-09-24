using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Bodies;

internal readonly struct ExpressionBodyTarget(
	SyntaxNode owner,
	SyntaxToken arrow,
	ExpressionSyntax expression,
	TextSpan signatureSpan,
	BodyShape shape
)
{
	public SyntaxToken Arrow { get; } = arrow;
	public ExpressionSyntax Expression { get; } = expression;
	public SyntaxNode Owner { get; } = owner;
	public BodyShape Shape { get; } = shape;
	public TextSpan SignatureSpan { get; } = signatureSpan;
}
