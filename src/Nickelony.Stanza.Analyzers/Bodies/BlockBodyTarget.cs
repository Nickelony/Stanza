using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Bodies;

/// <summary>A block body and each part a conversion to an expression body reads.</summary>
/// <remarks>
/// The constructor takes one parameter for each part, since the one job is to hold them together.
/// </remarks>
internal readonly struct BlockBodyTarget(
	SyntaxNode owner,
	BlockSyntax block,
	StatementSyntax statement,
	ExpressionSyntax expression,
	SyntaxToken? keyword,
	TextSpan signatureSpan,
	BodyShape shape,
	SyntaxToken closeBrace,
	AccessorListSyntax? accessorList
)
{
	public AccessorListSyntax? AccessorList { get; } = accessorList;
	public BlockSyntax Block { get; } = block;
	public SyntaxToken CloseBrace { get; } = closeBrace;
	public ExpressionSyntax Expression { get; } = expression;
	public SyntaxToken? Keyword { get; } = keyword;
	public SyntaxNode Owner { get; } = owner;
	public BodyShape Shape { get; } = shape;
	public TextSpan SignatureSpan { get; } = signatureSpan;
	public StatementSyntax Statement { get; } = statement;

	/// <summary>
	/// The version an expression body needs: C# 7.0 for a constructor, destructor,
	/// accessor or <see langword="throw"/> body, else C# 6.0.
	/// </summary>
	public LanguageVersion ExpressionBodyFloor =>
		Owner is ConstructorDeclarationSyntax or DestructorDeclarationSyntax or AccessorDeclarationSyntax || Statement is ThrowStatementSyntax
			? LanguageVersion.CSharp7
			: LanguageVersion.CSharp6;
}
