using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Nickelony.Stanza.Analyzers.Arguments;

/// <summary>Pairs call arguments with their parameters, and tells whether a node is in an expression tree.</summary>
internal static class ArgumentBinding
{
	/// <summary>Writes a parameter named after a keyword with <c>@</c>, so the name reads as an identifier.</summary>
	public static string EscapeName(string name) => SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : $"@{name}";

	/// <summary>Whether a named argument may precede a positional one, which C# 7.2 first allows.</summary>
	public static bool AllowsNamesBeforePositions(SyntaxTree syntaxTree) => StanzaSyntax.SupportsLanguage(syntaxTree, LanguageVersion.CSharp7_2);

	/// <summary>
	/// Whether an enclosing lambda or query clause at any depth makes the node part of an expression tree.
	/// </summary>
	public static bool IsInsideExpressionTree(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken)
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

	/// <summary>
	/// Pairs by span containment, not position, as the bindings also hold entries the call writes no argument for.
	/// </summary>
	public static IArgumentOperation?[] PairArgumentsWithBindings(BaseArgumentListSyntax argumentList, ImmutableArray<IArgumentOperation> bound)
	{
		SeparatedSyntaxList<ArgumentSyntax> arguments = argumentList.Arguments;
		var pairing = new IArgumentOperation?[arguments.Count];

		// An omitted optional parameter or an empty params array has a binding but no written argument.
		foreach (IArgumentOperation candidate in bound)
		{
			if (candidate.Syntax is not { } syntax)
				continue;

			// Arguments are disjoint and ascending:
			// the carrier is the last one starting at or before the syntax.
			int low = 0;
			int high = arguments.Count - 1;
			int containing = -1;

			while (low <= high)
			{
				int middle = low + ((high - low) / 2);

				if (arguments[middle].SpanStart <= syntax.SpanStart)
				{
					containing = middle;
					low = middle + 1;
				}
				else
				{
					high = middle - 1;
				}
			}

			if (containing >= 0 && syntax.Span.End <= arguments[containing].Span.End && pairing[containing] is null)
				pairing[containing] = candidate;
		}

		return pairing;
	}

	public static int CountLeadingImplicitArguments(ImmutableArray<IArgumentOperation> bound, BaseArgumentListSyntax argumentList)
	{
		int count = 0;

		foreach (IArgumentOperation candidate in bound)
		{
			if (candidate.Syntax is { } syntax && IsWritten(argumentList, syntax))
				break;

			count++;
		}

		return count;
	}

	private static bool IsWritten(BaseArgumentListSyntax argumentList, SyntaxNode syntax)
	{
		foreach (ArgumentSyntax argument in argumentList.Arguments)
		{
			if (IsInside(argument, syntax))
				return true;
		}

		return false;
	}

	private static bool IsInside(ArgumentSyntax argument, SyntaxNode syntax) =>
		syntax.Span.Start >= argument.SpanStart && syntax.Span.End <= argument.Span.End;

	public static BaseArgumentListSyntax? FindArgumentList(SyntaxNode node)
	{
		return node switch
		{
			BaseObjectCreationExpressionSyntax creation => creation.ArgumentList,
			ConstructorInitializerSyntax initializer => initializer.ArgumentList,
			ElementAccessExpressionSyntax elementAccess => elementAccess.ArgumentList,
			InvocationExpressionSyntax invocation => invocation.ArgumentList,
			PrimaryConstructorBaseTypeSyntax primary => primary.ArgumentList,
			_ => null,
		};
	}

	public static ImmutableArray<IArgumentOperation> FindBoundArguments(
		SemanticModel semanticModel,
		SyntaxNode node,
		CancellationToken cancellationToken
	)
	{
		return semanticModel.GetOperation(node, cancellationToken) switch
		{
			IInvocationOperation invocation when HasAuthoredParameters(invocation.TargetMethod) => invocation.Arguments,
			IObjectCreationOperation creation => creation.Arguments,
			IPropertyReferenceOperation property when property.Property.IsIndexer => property.Arguments,
			_ => default,
		};
	}

	private static bool HasAuthoredParameters(IMethodSymbol method) => method.MethodKind != MethodKind.DelegateInvoke;
}
