using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Finds the logical expressions that hold more operators than a limit allows.</summary>
internal static class LogicalConditions
{
	/// <summary>Collects the outermost expression of each condition that holds more operators than the limit.</summary>
	/// <remarks>
	/// A lambda, an anonymous method and an argument start a condition of their own.
	/// </remarks>
	public static ImmutableArray<ExpressionSyntax> CollectLongConditions(SyntaxNode root, int limit, CancellationToken cancellationToken)
	{
		List<Condition> conditions = [];
		Stack<(SyntaxNode Node, Condition? Condition)> pending = new();

		pending.Push((root, null));

		// The walk keeps its own stack, so a deep condition cannot overflow the call stack, and it visits each node once.
		while (pending.Count > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();

			(SyntaxNode node, Condition? condition) = pending.Pop();

			if (IsSkipped(node))
				continue;

			if (IsBoundary(node))
			{
				condition = null;
			}
			else if (node is BinaryExpressionSyntax binary && binary.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression)
			{
				if (condition is null)
				{
					condition = new Condition(binary);
					conditions.Add(condition);
				}

				condition.Count++;
			}

			foreach (SyntaxNode child in node.ChildNodes())
				pending.Push((child, condition));
		}

		ImmutableArray<ExpressionSyntax>.Builder longConditions = ImmutableArray.CreateBuilder<ExpressionSyntax>();

		foreach (Condition condition in conditions)
		{
			if (condition.Count > limit)
				longConditions.Add(condition.Root);
		}

		return longConditions.ToImmutable();
	}

	/// <summary>Counts the logical operators that an expression adds to the condition around it.</summary>
	/// <remarks>
	/// A lambda, an anonymous method and an argument start a condition of their own, so they stay out of the count.
	/// </remarks>
	public static int CountOperators(SyntaxNode expression)
	{
		int count = 0;
		Stack<SyntaxNode> pending = new();

		pending.Push(expression);

		while (pending.Count > 0)
		{
			SyntaxNode node = pending.Pop();

			if (IsBoundary(node))
				continue;

			if (node is BinaryExpressionSyntax binary && binary.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression)
				count++;

			foreach (SyntaxNode child in node.ChildNodes())
				pending.Push(child);
		}

		return count;
	}

	/// <summary>Whether the node stands in an <c>Equals</c> method or a comparison operator, both skipped.</summary>
	public static bool IsInSkippedMember(SyntaxNode node)
	{
		foreach (SyntaxNode ancestor in node.Ancestors())
		{
			if (IsSkipped(ancestor))
				return true;
		}

		return false;
	}

	private static bool IsBoundary(SyntaxNode node) => node is AnonymousFunctionExpressionSyntax or ArgumentSyntax;

	/// <summary>Whether the node is a member that may write a long comparison, which reads fine.</summary>
	private static bool IsSkipped(SyntaxNode node)
	{
		return node switch
		{
			MethodDeclarationSyntax method => method.Identifier.ValueText == "Equals",
			OperatorDeclarationSyntax op => op.OperatorToken.Kind() is SyntaxKind.EqualsEqualsToken or SyntaxKind.ExclamationEqualsToken,
			_ => false,
		};
	}

	private sealed class Condition(ExpressionSyntax root)
	{
		public ExpressionSyntax Root { get; } = root;
		public int Count { get; set; }
	}
}
