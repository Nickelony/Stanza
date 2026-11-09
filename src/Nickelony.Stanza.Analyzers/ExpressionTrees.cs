using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Tells whether a node stands in an expression tree, which holds no pattern and no named argument.</summary>
internal static class ExpressionTrees
{
	/// <summary>
	/// Whether an enclosing lambda or query clause at any depth makes the node part of an expression tree.
	/// </summary>
	public static bool IsInside(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken)
	{
		INamedTypeSymbol? expressionType = semanticModel.Compilation.GetTypeByMetadataName("System.Linq.Expressions.Expression");

		if (expressionType is null)
			return false;

		SyntaxNode child = node;

		for (SyntaxNode? ancestor = node.Parent; ancestor is not null; child = ancestor, ancestor = ancestor.Parent)
		{
			// A lambda is an expression tree when it converts to `Expression` or a type derived from it:
			// `Expression<T>`, or `LambdaExpression` and `Expression` itself for a lambda with a natural type.
			// A query clause is one when its method takes one, and an `orderby` clause binds no method of its own:
			// each of its orderings binds one.
			bool isTree = ancestor switch
			{
				// A join takes its inner sequence as an ordinary argument, and only its key selectors as expression trees.
				JoinClauseSyntax join when join.InExpression == child => false,
				LambdaExpressionSyntax lambda => IsExpressionType(semanticModel.GetTypeInfo(lambda, cancellationToken).ConvertedType, expressionType),

				QueryClauseSyntax clause => TakesExpressionTree(
					semanticModel.GetQueryClauseInfo(clause, cancellationToken).OperationInfo.Symbol,
					expressionType
				),

				SelectOrGroupClauseSyntax clause => TakesExpressionTree(
					semanticModel.GetSymbolInfo(clause, cancellationToken).Symbol,
					expressionType
				),

				OrderingSyntax ordering => TakesExpressionTree(semanticModel.GetSymbolInfo(ordering, cancellationToken).Symbol, expressionType),
				_ => false,
			};

			if (isTree)
				return true;
		}

		return false;
	}

	private static bool TakesExpressionTree(ISymbol? symbol, INamedTypeSymbol expressionType)
	{
		if (symbol is not IMethodSymbol method)
			return false;

		foreach (IParameterSymbol parameter in method.Parameters)
		{
			if (IsExpressionType(parameter.Type, expressionType))
				return true;
		}

		return false;
	}

	private static bool IsExpressionType(ITypeSymbol? type, INamedTypeSymbol expressionType)
	{
		for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(current, expressionType))
				return true;
		}

		return false;
	}
}
