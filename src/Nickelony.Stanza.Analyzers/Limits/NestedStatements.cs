using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.Limits;

/// <summary>Finds the statements that nest deeper than a limit allows.</summary>
internal static class NestedStatements
{
	/// <summary>Collects the deepest statement of each member or function whose nesting passes the limit.</summary>
	/// <remarks>
	/// A lambda, an anonymous method and a local function start a count of their own.
	/// </remarks>
	public static ImmutableArray<StatementSyntax> CollectDeepest(SyntaxNode root, int limit, CancellationToken cancellationToken)
	{
		Scope rootScope = new();
		List<Scope> scopes = [rootScope];
		Stack<(SyntaxNode Node, Scope Scope, int Depth)> pending = new();

		pending.Push((root, rootScope, 0));

		// The walk keeps its own stack, so a deep nest cannot overflow the call stack, and it visits each node once.
		while (pending.Count > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();

			(SyntaxNode node, Scope scope, int depth) = pending.Pop();

			if (IsBoundary(node))
			{
				scope = new Scope();
				scopes.Add(scope);
				depth = 0;
			}
			else if (node is StatementSyntax statement && AddsLevel(statement))
			{
				depth++;
				scope.Add(statement, depth);
			}

			foreach (SyntaxNode child in node.ChildNodes())
				pending.Push((child, scope, depth));
		}

		ImmutableArray<StatementSyntax>.Builder deepest = ImmutableArray.CreateBuilder<StatementSyntax>();

		foreach (Scope scope in scopes)
		{
			if (scope.Deepest is { } statement && scope.Depth > limit)
				deepest.Add(statement);
		}

		return deepest.ToImmutable();
	}

	private static bool IsBoundary(SyntaxNode node) =>
		node is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax;

	/// <summary>Whether the statement nests its body one level deeper, which an <c>else if</c> does not.</summary>
	private static bool AddsLevel(StatementSyntax statement)
	{
		return statement.Kind() switch
		{
			SyntaxKind.IfStatement => statement.Parent is not ElseClauseSyntax,

			SyntaxKind.ForStatement
			or SyntaxKind.ForEachStatement
			or SyntaxKind.ForEachVariableStatement
			or SyntaxKind.WhileStatement
			or SyntaxKind.DoStatement
			or SyntaxKind.SwitchStatement
			or SyntaxKind.TryStatement
			or SyntaxKind.LockStatement
			or SyntaxKind.UsingStatement => true,

			_ => false,
		};
	}

	private sealed class Scope
	{
		public StatementSyntax? Deepest { get; private set; }
		public int Depth { get; private set; }

		public void Add(StatementSyntax statement, int depth)
		{
			if (Deepest is null || depth > Depth || (depth == Depth && statement.SpanStart < Deepest.SpanStart))
			{
				Deepest = statement;
				Depth = depth;
			}
		}
	}
}
