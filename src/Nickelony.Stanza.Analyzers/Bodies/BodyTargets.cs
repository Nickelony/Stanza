using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Bodies;

/// <summary>Finds the expression or block body that a clause or a diagnostic names, with its signature.</summary>
internal static class BodyTargets
{
	public static ExpressionBodyTarget? FindExpressionTarget(ArrowExpressionClauseSyntax clause)
	{
		BodyShape? shape = clause.Parent switch
		{
			{ } owner when IsMethodLike(owner) => BodyShape.Statement,
			PropertyDeclarationSyntax or IndexerDeclarationSyntax => BodyShape.Value,
			AccessorDeclarationSyntax => BodyShape.Accessor,
			_ => null,
		};

		if (shape is null || clause.Parent is null || FindSignatureSpan(clause.Parent, clause.ArrowToken) is not { } signatureSpan)
			return null;

		return new ExpressionBodyTarget(
			owner: clause.Parent,
			arrow: clause.ArrowToken,
			expression: clause.Expression,
			signatureSpan,
			shape: shape.Value
		);
	}

	public static ExpressionBodyTarget? FindExpressionTarget(SyntaxNode root, Diagnostic diagnostic) =>
		root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is ArrowExpressionClauseSyntax clause ? FindExpressionTarget(clause) : null;

	public static ExpressionBodyTarget? FindEnclosingExpressionTarget(SyntaxNode root, Diagnostic diagnostic)
	{
		return
			root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is { } node
			&& node.FirstAncestorOrSelf<ArrowExpressionClauseSyntax>() is { } clause
			? FindExpressionTarget(clause)
			: null;
	}

	public static bool IsBodyMultiLine(ExpressionBodyTarget target) => StanzaSyntax.IsMultiLine(target.Expression);

	public static bool IsSignatureMultiLine(ExpressionBodyTarget target) =>
		target.Arrow.SyntaxTree is { } syntaxTree && StanzaSyntax.IsMultiLine(syntaxTree, target.SignatureSpan);

	public static bool IsMultiLine(ExpressionBodyTarget target) => IsSignatureMultiLine(target) || IsBodyMultiLine(target);

	public static BlockBodyTarget? FindBlockTarget(BlockSyntax block)
	{
		return block.Parent switch
		{
			AccessorDeclarationSyntax accessor => FindAccessorTarget(accessor, block),
			{ } owner when IsMethodLike(owner) => FindStatementTarget(owner, block),
			_ => null,
		};
	}

	private static BlockBodyTarget? FindStatementTarget(SyntaxNode owner, BlockSyntax block)
	{
		return FindSignatureSpan(owner, block.OpenBraceToken) is { } signatureSpan
			? BuildTarget(owner, block, shape: BodyShape.Statement, signatureSpan, closeBrace: block.CloseBraceToken, accessorList: null)
			: null;
	}

	private static BlockBodyTarget? FindAccessorTarget(AccessorDeclarationSyntax accessor, BlockSyntax block)
	{
		if (accessor.Parent is not AccessorListSyntax accessorList)
			return null;

		if (IsLoneGetter(accessor, accessorList) && accessorList.Parent is { } member)
		{
			return FindSignatureSpan(member, accessorList.OpenBraceToken) is { } valueSignatureSpan
				? BuildTarget(
					owner: member,
					block,
					shape: BodyShape.Value,
					signatureSpan: valueSignatureSpan,
					closeBrace: accessorList.CloseBraceToken,
					accessorList
				)
				: null;
		}

		return FindSignatureSpan(accessor, block.OpenBraceToken) is { } signatureSpan
			? BuildTarget(owner: accessor, block, shape: BodyShape.Accessor, signatureSpan, closeBrace: block.CloseBraceToken, accessorList: null)
			: null;
	}

	private static bool IsLoneGetter(AccessorDeclarationSyntax accessor, AccessorListSyntax accessorList)
	{
		// An initializer follows the accessor list and an arrow cannot take one, so that property collapses only its getter.
		return accessorList.Accessors.Count == 1
			&& accessor.Keyword.IsKind(SyntaxKind.GetKeyword)
			&& accessorList.Parent is PropertyDeclarationSyntax { Initializer: null } or IndexerDeclarationSyntax;
	}

	private static BlockBodyTarget? BuildTarget(
		SyntaxNode owner,
		BlockSyntax block,
		BodyShape shape,
		TextSpan signatureSpan,
		SyntaxToken closeBrace,
		AccessorListSyntax? accessorList
	)
	{
		if (block.Statements.Count != 1)
			return null;

		StatementSyntax statement = block.Statements[0];

		ExpressionSyntax? expression = statement switch
		{
			ReturnStatementSyntax { Expression: { } returned } => returned,
			ThrowStatementSyntax { Expression: { } thrown } => thrown,
			ExpressionStatementSyntax expressionStatement => expressionStatement.Expression,
			_ => null,
		};

		if (expression is null)
			return null;

		SyntaxToken? keyword = statement is ReturnStatementSyntax or ThrowStatementSyntax ? statement.GetFirstToken() : null;

		return new BlockBodyTarget(owner, block, statement, expression, keyword, signatureSpan, shape, closeBrace, accessorList);
	}

	public static BlockBodyTarget? FindBlockTarget(SyntaxNode root, Diagnostic diagnostic) =>
		root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is BlockSyntax block ? FindBlockTarget(block) : null;

	/// <summary>
	/// Whether the owner is a member or local function whose body is a statement block or an arrow over one.
	/// </summary>
	private static bool IsMethodLike(SyntaxNode owner)
	{
		return owner
			is MethodDeclarationSyntax
				or ConstructorDeclarationSyntax
				or DestructorDeclarationSyntax
				or OperatorDeclarationSyntax
				or ConversionOperatorDeclarationSyntax
				or LocalFunctionStatementSyntax;
	}

	/// <summary>Spans the signature up to the token before <paramref name="openingToken"/>.</summary>
	private static TextSpan? FindSignatureSpan(SyntaxNode owner, SyntaxToken openingToken)
	{
		SyntaxToken signatureStart = GetSignatureStart(owner);
		SyntaxToken signatureEnd = openingToken.GetPreviousToken();

		return signatureEnd.Span.End < signatureStart.SpanStart ? null : TextSpan.FromBounds(signatureStart.SpanStart, signatureEnd.Span.End);
	}

	public static SyntaxList<AttributeListSyntax> GetAttributeLists(SyntaxNode owner)
	{
		return owner switch
		{
			MemberDeclarationSyntax member => member.AttributeLists,
			LocalFunctionStatementSyntax localFunction => localFunction.AttributeLists,
			AccessorDeclarationSyntax accessor => accessor.AttributeLists,
			_ => [],
		};
	}

	private static SyntaxToken GetSignatureStart(SyntaxNode owner)
	{
		SyntaxList<AttributeListSyntax> attributes = GetAttributeLists(owner);
		return attributes.Count == 0 ? owner.GetFirstToken() : attributes[attributes.Count - 1].GetLastToken().GetNextToken();
	}
}
