using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads the call or assignment a statement's expression makes, through the wrappers around it.</summary>
internal static class BlankLineExpressions
{
	/// <summary>Whether a statement is an await, or a discard of one such as <c>_ = await Load();</c>.</summary>
	public static bool IsAwaitStatement(ExpressionSyntax expression)
	{
		expression = UnwrapParentheses(UnwrapDiscard(UnwrapParentheses(expression)));
		return expression is AwaitExpressionSyntax;
	}

	/// <summary>Strips the parentheses of an expression, and leaves an await in place.</summary>
	public static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax parenthesized)
			expression = parenthesized.Expression;

		return expression;
	}

	/// <summary>Strips the awaits, the parentheses and the null-forgiving <c>!</c> of an expression.</summary>
	public static ExpressionSyntax UnwrapAwaitParenthesesAndSuppression(ExpressionSyntax expression)
	{
		while (true)
		{
			switch (expression)
			{
				case AwaitExpressionSyntax awaitExpression:
					expression = awaitExpression.Expression;
					continue;

				case ParenthesizedExpressionSyntax parenthesizedExpression:
					expression = parenthesizedExpression.Expression;
					continue;

				case PostfixUnaryExpressionSyntax postfixUnaryExpression
					when postfixUnaryExpression.IsKind(SyntaxKind.SuppressNullableWarningExpression):
					expression = postfixUnaryExpression.Operand;
					continue;

				default:
					return expression;
			}
		}
	}

	/// <summary>Reads the value a discard such as <c>_ = Read();</c> assigns, or the expression as it is.</summary>
	public static ExpressionSyntax UnwrapDiscard(ExpressionSyntax expression)
	{
		return
			expression
				is AssignmentExpressionSyntax
				{
					RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
					Left: IdentifierNameSyntax { Identifier.ValueText: "_" },
				} discard
			? discard.Right
			: expression;
	}

	/// <summary>Reads what a <c>?.</c> guards, as in <c>button?.Click += Handler;</c>, or the expression.</summary>
	public static ExpressionSyntax UnwrapConditionalAccess(ExpressionSyntax expression)
	{
		while (expression is ConditionalAccessExpressionSyntax conditionalAccess)
			expression = UnwrapAwaitParenthesesAndSuppression(conditionalAccess.WhenNotNull);

		return expression;
	}

	/// <summary>Finds the assignment, compound assignment or step that a statement's expression makes.</summary>
	public static ExpressionSyntax? FindAssignment(ExpressionSyntax expression)
	{
		expression = UnwrapConditionalAccess(UnwrapAwaitParenthesesAndSuppression(expression));

		bool isAssignment =
			expression is AssignmentExpressionSyntax
			|| expression.IsKind(SyntaxKind.PreIncrementExpression)
			|| expression.IsKind(SyntaxKind.PreDecrementExpression)
			|| expression.IsKind(SyntaxKind.PostIncrementExpression)
			|| expression.IsKind(SyntaxKind.PostDecrementExpression);

		return isAssignment ? expression : null;
	}

	/// <summary>Finds the call a statement makes, through a discard and a <c>?.</c> that guards it.</summary>
	public static InvocationExpressionSyntax? FindInvocation(ExpressionSyntax expression)
	{
		expression = UnwrapDiscard(UnwrapAwaitParenthesesAndSuppression(expression));
		expression = UnwrapConditionalAccess(UnwrapAwaitParenthesesAndSuppression(expression));

		return expression as InvocationExpressionSyntax;
	}

	/// <summary>Gets the call a chain such as <c>Load().Where().ToList()</c> starts from.</summary>
	public static InvocationExpressionSyntax GetRootInvocation(InvocationExpressionSyntax invocation)
	{
		InvocationExpressionSyntax current = invocation;

		while (
			UnwrapAwaitParenthesesAndSuppression(current.Expression) is MemberAccessExpressionSyntax memberAccess
			&& UnwrapAwaitParenthesesAndSuppression(memberAccess.Expression) is InvocationExpressionSyntax nestedInvocation
		)
		{
			current = nestedInvocation;
		}

		return current;
	}

	/// <summary>Gets the expression a chain of calls and member, element and <c>?.</c> accesses starts from.</summary>
	public static ExpressionSyntax GetChainStart(ExpressionSyntax expression)
	{
		while (true)
		{
			expression = UnwrapAwaitParenthesesAndSuppression(expression);

			switch (expression)
			{
				case InvocationExpressionSyntax invocation:
					expression = invocation.Expression;
					continue;

				case MemberAccessExpressionSyntax memberAccess:
					expression = memberAccess.Expression;
					continue;

				case ElementAccessExpressionSyntax elementAccess:
					expression = elementAccess.Expression;
					continue;

				case ConditionalAccessExpressionSyntax conditionalAccess:
					expression = conditionalAccess.Expression;
					continue;

				default:
					return expression;
			}
		}
	}

	/// <summary>Finds the name a callee calls.</summary>
	public static string? FindName(ExpressionSyntax expression)
	{
		expression = UnwrapAwaitParenthesesAndSuppression(expression);

		return expression switch
		{
			SimpleNameSyntax simpleName => simpleName.Identifier.ValueText,
			MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
			MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
			_ => null,
		};
	}

	/// <summary>Finds the name in the chain of a callee that the roots list, the one nearest the call first.</summary>
	public static string? FindRoot(ExpressionSyntax expression, ImmutableHashSet<string> roots)
	{
		if (roots.Count == 0)
			return null;

		expression = UnwrapAwaitParenthesesAndSuppression(expression);

		// A receiver that is a single name has no chain to walk, so none is built for it.
		if (expression is SimpleNameSyntax singleName)
		{
			string single = singleName.Identifier.ValueText;
			return roots.Contains(single) ? single : null;
		}

		// The chain is walked from the called name back to its receiver, so the matching name nearest the call wins.
		while (expression is MemberAccessExpressionSyntax memberAccess)
		{
			string name = memberAccess.Name.Identifier.ValueText;

			if (roots.Contains(name))
				return name;

			expression = UnwrapAwaitParenthesesAndSuppression(memberAccess.Expression);
		}

		return expression is SimpleNameSyntax leaf && roots.Contains(leaf.Identifier.ValueText) ? leaf.Identifier.ValueText : null;
	}
}
