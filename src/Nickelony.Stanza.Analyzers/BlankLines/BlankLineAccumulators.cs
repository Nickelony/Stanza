using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads the seeds above a loop that it builds, which ZA0063 keeps with the loop as one group.</summary>
/// <remarks>
/// A group is the single-line seeds directly above the loop, with only whitespace in their gaps.
/// </remarks>
internal static class BlankLineAccumulators
{
	/// <summary>The methods that add to a collection or a builder, so a call of one builds its receiver.</summary>
	private static readonly ImmutableHashSet<string> s_builders = ImmutableHashSet.Create(
		StringComparer.Ordinal,
		"Add",
		"AddRange",
		"Append",
		"AppendLine",
		"AppendFormat",
		"Insert",
		"TryAdd",
		"Enqueue",
		"Push"
	);

	/// <summary>Reads the gaps inside each group, in one walk up from each loop.</summary>
	public static ImmutableHashSet<int> ReadGaps(ContainerContext container)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		SemanticModel semanticModel = container.Context.SemanticModel;
		ImmutableHashSet<int>.Builder gaps = ImmutableHashSet.CreateBuilder<int>();

		for (int loop = 1; loop < siblings.Length; loop++)
		{
			container.CancellationToken.ThrowIfCancellationRequested();

			if (ReadLoopBody(siblings[loop]) is not { } body)
				continue;

			HashSet<ISymbol>? built = null;

			// A seed ends the walk where the loop builds nothing it seeds, so each seed is read for one loop at most.
			for (int seed = loop - 1; seed >= 0; seed--)
			{
				if (
					StanzaSyntax.IsMultiLine(siblings[seed])
					|| !BlankLineKeptPairs.IsWhitespaceGap(siblings[seed], siblings[seed + 1], container.CancellationToken)
					|| ReadSeededSymbol(siblings[seed], semanticModel) is not { } symbol
					|| !(built ??= CollectBuiltSymbols(body, semanticModel)).Contains(symbol)
				)
				{
					break;
				}

				gaps.Add(seed + 1);
			}
		}

		return gaps.ToImmutable();
	}

	private static StatementSyntax? ReadLoopBody(SyntaxNode unit)
	{
		return unit switch
		{
			ForStatementSyntax loop => loop.Statement,
			CommonForEachStatementSyntax loop => loop.Statement,
			WhileStatementSyntax loop => loop.Statement,
			DoStatementSyntax loop => loop.Statement,
			_ => null,
		};
	}

	/// <summary>
	/// Reads the binding a statement seeds with a value that reads no variable, or <see langword="null"/>.
	/// </summary>
	private static ISymbol? ReadSeededSymbol(SyntaxNode statement, SemanticModel semanticModel)
	{
		(SyntaxNode Target, ExpressionSyntax Value)? seed = statement switch
		{
			LocalDeclarationStatementSyntax { Declaration.Variables: { Count: 1 } variables }
				when variables[0] is { Initializer: { } initializer } declarator => (declarator, initializer.Value),

			ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
				when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
					&& BlankLineExpressions.UnwrapParenthesesAndSuppression(assignment.Left) is IdentifierNameSyntax target => (
				target,
				assignment.Right
			),

			_ => null,
		};

		if (seed is not { } found || found.Value.DescendantNodesAndSelf().Any(static node => node is ThisExpressionSyntax or BaseExpressionSyntax))
			return null;

		if (found.Value.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().Any(name => ReadsVariable(name, semanticModel)))
			return null;

		return found.Target is VariableDeclaratorSyntax variable
			? BlankLineKeptPairs.FindDeclaredLocal(semanticModel, variable, cancellationToken: default)
			: semanticModel.GetSymbolInfo(found.Target).Symbol;
	}

	/// <summary>
	/// Whether a name in a seed's value reads a variable, rather than naming a type, a method or a static member.
	/// </summary>
	private static bool ReadsVariable(SimpleNameSyntax name, SemanticModel semanticModel)
	{
		if (SyntaxFacts.IsInTypeOnlyContext(name) || name.Parent is QualifiedNameSyntax or NameColonSyntax or NameEqualsSyntax)
			return false;

		// The member name after a dot reads what its receiver reads, and the receiver is judged on its own.
		if (name.Parent is MemberAccessExpressionSyntax access && access.Name == name)
			return false;

		SymbolInfo info = semanticModel.GetSymbolInfo(name);

		if ((info.Symbol ?? info.CandidateSymbols.FirstOrDefault()) is { } symbol)
		{
			return symbol switch
			{
				ITypeSymbol or INamespaceSymbol or IMethodSymbol => false,
				IFieldSymbol { IsConst: true } => false,
				_ => true,
			};
		}

		// A name that binds to nothing is read by its place: a callee or a receiver is a type, anything else a variable.
		return name.Parent switch
		{
			InvocationExpressionSyntax invocation => invocation.Expression != name,
			MemberAccessExpressionSyntax receiver => receiver.Expression != name,
			_ => true,
		};
	}

	/// <summary>Collects the bindings a loop body assigns, steps or calls a builder method on.</summary>
	/// <remarks>
	/// A lambda, a local function and a type declared in the body run elsewhere, so their writes build nothing.
	/// </remarks>
	private static HashSet<ISymbol> CollectBuiltSymbols(StatementSyntax body, SemanticModel semanticModel)
	{
		HashSet<ISymbol> built = new(SymbolEqualityComparer.Default);

		IEnumerable<SyntaxNode> executing = body.DescendantNodesAndSelf(static node =>
			node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or BaseTypeDeclarationSyntax)
		);

		foreach (SyntaxNode node in executing)
		{
			switch (node)
			{
				case AssignmentExpressionSyntax assignment:
					AddAssignedSymbols(assignment.Left, semanticModel, built);
					break;

				case PrefixUnaryExpressionSyntax unary
					when unary.IsKind(SyntaxKind.PreIncrementExpression) || unary.IsKind(SyntaxKind.PreDecrementExpression):
					AddBindingSymbol(unary.Operand, semanticModel, built);
					break;

				case PostfixUnaryExpressionSyntax unary
					when unary.IsKind(SyntaxKind.PostIncrementExpression) || unary.IsKind(SyntaxKind.PostDecrementExpression):
					AddBindingSymbol(unary.Operand, semanticModel, built);
					break;

				case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }
					when access.IsKind(SyntaxKind.SimpleMemberAccessExpression) && s_builders.Contains(access.Name.Identifier.ValueText):
					AddBindingSymbol(access.Expression, semanticModel, built);
					break;
			}
		}

		return built;
	}

	private static void AddAssignedSymbols(ExpressionSyntax target, SemanticModel semanticModel, HashSet<ISymbol> built)
	{
		target = BlankLineExpressions.UnwrapParenthesesAndSuppression(target);

		if (target is not TupleExpressionSyntax tuple)
		{
			AddBindingSymbol(target, semanticModel, built);
			return;
		}

		foreach (ArgumentSyntax argument in tuple.Arguments)
			AddAssignedSymbols(argument.Expression, semanticModel, built);
	}

	private static void AddBindingSymbol(ExpressionSyntax expression, SemanticModel semanticModel, HashSet<ISymbol> built)
	{
		if (
			BlankLineExpressions.UnwrapParenthesesAndSuppression(expression) is IdentifierNameSyntax identifier
			&& semanticModel.GetSymbolInfo(identifier).Symbol is { } symbol
		)
		{
			built.Add(symbol);
		}
	}
}
