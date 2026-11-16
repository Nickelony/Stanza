using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads direct receiver operations without evaluating a call or an indexer.</summary>
internal static class BlankLineReceiverCalls
{
	public static bool IsInitializationPair(ContainerContext container, int boundary)
	{
		return IsInitializationCandidate(container, boundary)
			&& (
				boundary == 1
				|| container.ReadsAnchors
				|| !BlankLineKeptPairs
					.ReadRunBoundaries(container with { ReadsRuns = false }, StanzaRules.KeepInitializationWithCalls)
					.Contains(boundary - 1)
			);
	}

	/// <summary>Whether the pair qualifies before a run owner takes its initialization gap.</summary>
	public static bool IsInitializationCandidate(ContainerContext container, int boundary)
	{
		SyntaxNode previous = container.Siblings[boundary - 1];
		SyntaxNode next = container.Siblings[boundary];

		if (previous is not LocalDeclarationStatementSyntax local || next is not ExpressionStatementSyntax call)
			return false;

		bool singleInitialization =
			!StanzaSyntax.IsMultiLine(previous)
			&& !StanzaSyntax.IsMultiLine(next)
			&& local.UsingKeyword.RawKind == 0
			&& local.Declaration.Variables.Count == 1;

		if (!singleInitialization || FindReceiver(call.Expression) is not IdentifierNameSyntax receiver)
			return false;

		VariableDeclaratorSyntax variable = local.Declaration.Variables[0];

		ExpressionSyntax? initializer = variable.Initializer?.Value is { } value
			? BlankLineExpressions.UnwrapAwaitParenthesesAndSuppression(value)
			: null;

		if (
			variable.Identifier.ValueText != receiver.Identifier.ValueText
			|| initializer is not (InvocationExpressionSyntax or ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)
		)
		{
			return false;
		}

		if (boundary == 1)
			return true;

		SyntaxNode above = container.Siblings[boundary - 2];

		return BlankLineKeptPairs.HasStanzaOpening(container, boundary - 1, StanzaRules.KeepInitializationWithCalls)
			&& BlankLineKeptPairs.IsWhitespaceGap(above, previous, container.CancellationToken);
	}

	/// <summary>The statements of each container that share a line with a neighboring statement.</summary>
	/// <remarks>
	/// A token walk to the neighbor scans the whole container, so one pass reads every statement of it at once.
	/// </remarks>
	private static readonly WeakCache<SyntaxNode, HashSet<SyntaxNode>> s_joined = new();

	/// <summary>Whether the statement shares a line with a neighboring statement of its container.</summary>
	public static bool IsJoinedStatement(SyntaxNode statement)
	{
		SyntaxNode? parent = statement.Parent is GlobalStatementSyntax global ? global.Parent : statement.Parent;
		return parent is not null && s_joined.GetOrCompute(parent, ReadJoinedStatements).Contains(statement);
	}

	private static HashSet<SyntaxNode> ReadJoinedStatements(SyntaxNode parent)
	{
		HashSet<SyntaxNode> joined = [];
		SyntaxNode? previous = null;
		int previousEndLine = -1;

		// A brace or another token of the container between two statements keeps them apart, as the token walk did.
		foreach (SyntaxNodeOrToken child in parent.ChildNodesAndTokens())
		{
			if (child.AsNode() is not { } node || node is not (StatementSyntax or GlobalStatementSyntax))
			{
				previous = null;
				continue;
			}

			FileLinePositionSpan lines = node.GetLocation().GetLineSpan();

			if (previous is not null && previousEndLine == lines.StartLinePosition.Line)
				joined.UnionWith([previous, ToStatement(previous), node, ToStatement(node)]);

			previous = node;
			previousEndLine = lines.EndLinePosition.Line;
		}

		return joined;
	}

	private static SyntaxNode ToStatement(SyntaxNode node) => node is GlobalStatementSyntax global ? global.Statement : node;

	public static string? FindConfiguredReceiver(ExpressionSyntax expression, StanzaOptions options)
	{
		ExpressionSyntax? receiver = FindReceiver(expression);
		return receiver is null ? null : FindConfiguredPath(receiver, options);
	}

	/// <summary>Reads the selected owner of a direct call, member write or update.</summary>
	public static string? FindConfiguredOwner(ExpressionSyntax expression, StanzaOptions options)
	{
		expression = BlankLineExpressions.UnwrapParenthesesAndSuppression(expression);
		ExpressionSyntax? receiver = FindReceiver(expression);

		if (receiver is null)
		{
			ExpressionSyntax? target = expression switch
			{
				AssignmentExpressionSyntax assignment => assignment.Left,

				PrefixUnaryExpressionSyntax prefix
					when prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression) => prefix.Operand,

				PostfixUnaryExpressionSyntax postfix
					when postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression) => postfix.Operand,

				_ => null,
			};

			if (
				target is not null
				&& BlankLineExpressions.UnwrapParenthesesAndSuppression(target) is MemberAccessExpressionSyntax member
				&& member.IsKind(SyntaxKind.SimpleMemberAccessExpression)
			)
			{
				receiver = member.Expression;
			}
		}

		return receiver is null ? null : FindConfiguredPath(receiver, options);
	}

	private static string? FindConfiguredPath(ExpressionSyntax receiver, StanzaOptions options)
	{
		if (options.ReceiverRoots.IsEmpty || FindPath(receiver) is not { Length: > 0 } path)
			return null;

		string[] names = path.Split('.');

		return names.Length > 0 && (options.ReceiverRoots.Contains("*") || options.ReceiverRoots.Overlaps(names))
			? options.ComparesReceiverPaths
				? string.Join(".", names)
				: names[0]
			: null;
	}

	public static ExpressionSyntax? FindReceiver(ExpressionSyntax expression)
	{
		expression = BlankLineExpressions.UnwrapParenthesesAndSuppression(expression);

		if (expression is AwaitExpressionSyntax awaited)
			expression = BlankLineExpressions.UnwrapParenthesesAndSuppression(awaited.Expression);

		return
			expression is InvocationExpressionSyntax invocation
			&& BlankLineExpressions.UnwrapParenthesesAndSuppression(invocation.Expression) is MemberAccessExpressionSyntax member
			&& member.IsKind(SyntaxKind.SimpleMemberAccessExpression)
			? BlankLineExpressions.UnwrapParenthesesAndSuppression(member.Expression)
			: null;
	}

	private static string? FindPath(ExpressionSyntax expression)
	{
		List<string> names = [];

		while (true)
		{
			expression = BlankLineExpressions.UnwrapParenthesesAndSuppression(expression);

			if (expression is MemberAccessExpressionSyntax member && member.IsKind(SyntaxKind.SimpleMemberAccessExpression))
			{
				names.Add(member.Name.Identifier.ValueText);
				expression = member.Expression;

				continue;
			}

			if (expression is IdentifierNameSyntax name)
				names.Add(name.Identifier.ValueText);
			else if (expression is not (ThisExpressionSyntax or BaseExpressionSyntax))
				return null;

			names.Reverse();
			return string.Join(".", names);
		}
	}
}
