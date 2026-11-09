using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Expressions;

/// <summary>Writes a test for <see langword="null"/> the other way, from the syntax alone.</summary>
internal static class NullCheckEdits
{
	/// <summary>Finds the operand of a comparison whose other side alone is a bare <see langword="null"/>.</summary>
	public static ExpressionSyntax? FindComparedOperand(BinaryExpressionSyntax comparison)
	{
		bool leftIsNull = comparison.Left.IsKind(SyntaxKind.NullLiteralExpression);
		bool rightIsNull = comparison.Right.IsKind(SyntaxKind.NullLiteralExpression);

		if (leftIsNull == rightIsNull)
			return null;

		return leftIsNull ? comparison.Right : comparison.Left;
	}

	/// <summary>Finds the <see langword="null"/> constant or empty property pattern under one <c>not</c>.</summary>
	public static PatternSyntax? FindNullTest(PatternSyntax pattern)
	{
		if (pattern is UnaryPatternSyntax unary && unary.OperatorToken.IsKind(SyntaxKind.NotKeyword))
			pattern = unary.Pattern;

		bool testsNull = pattern switch
		{
			ConstantPatternSyntax constant => constant.Expression.IsKind(SyntaxKind.NullLiteralExpression),

			RecursivePatternSyntax recursive => recursive
				is { Type: null, PositionalPatternClause: null, PropertyPatternClause.Subpatterns.Count: 0, Designation: null },

			_ => false,
		};

		return testsNull ? pattern : null;
	}

	public static ImmutableArray<TextChange> CreatePattern(SyntaxNode root, TextSpan span) =>
		FindNode<BinaryExpressionSyntax>(root, span) is { } comparison ? CreatePattern(comparison) : [];

	public static ImmutableArray<TextChange> CreateEqualityCheck(SyntaxNode root, TextSpan span) =>
		FindNode<IsPatternExpressionSyntax>(root, span) is { } check ? CreateEqualityCheck(check) : [];

	/// <summary>Creates the change that writes a comparison as <c>is null</c> or <c>is not null</c>.</summary>
	/// <remarks>
	/// The operand comes first, and a comment it would cross keeps the comparison as written.
	/// </remarks>
	public static ImmutableArray<TextChange> CreatePattern(BinaryExpressionSyntax comparison)
	{
		if (FindComparedOperand(comparison) is not { } operand)
			return [];

		string pattern = comparison.IsKind(SyntaxKind.NotEqualsExpression) ? "is not" : "is";

		if (operand == comparison.Left)
		{
			TextChange keyword = new(comparison.OperatorToken.Span, pattern);
			return IsEquality(operand) ? [CreateInsertion(operand.SpanStart, "("), CreateInsertion(operand.Span.End, ")"), keyword] : [keyword];
		}

		if (!IsLayoutUpTo(comparison.Left.GetFirstToken(), operand.SpanStart))
			return [];

		return [new TextChange(comparison.Span, $"{operand} {pattern} null")];
	}

	/// <summary>Creates the change that writes a pattern as <c>== null</c> or <c>!= null</c>.</summary>
	/// <remarks>
	/// A <see langword="null"/> constant stays where it is, and an empty property pattern gives way to the literal.
	/// </remarks>
	public static ImmutableArray<TextChange> CreateEqualityCheck(IsPatternExpressionSyntax check)
	{
		if (FindNullTest(check.Pattern) is not { } inner)
			return [];

		bool negated = check.Pattern is UnaryPatternSyntax;
		int end = inner is ConstantPatternSyntax ? ReadKeywordsEnd(check) : check.Pattern.Span.End;

		if (!IsLayoutUpTo(check.IsKeyword, end))
			return [];

		string comparison = (negated, inner) switch
		{
			(false, ConstantPatternSyntax) => "==",
			(true, ConstantPatternSyntax) => "!=",
			(false, _) => "!= null",
			(true, _) => "== null",
		};

		TextChange change = new(TextSpan.FromBounds(check.IsKeyword.SpanStart, end), comparison);

		return NeedsParentheses(check) ? [CreateInsertion(check.SpanStart, "("), change, CreateInsertion(check.Span.End, ")")] : [change];
	}

	private static int ReadKeywordsEnd(IsPatternExpressionSyntax check) =>
		check.Pattern is UnaryPatternSyntax unary ? unary.OperatorToken.Span.End : check.IsKeyword.Span.End;

	/// <summary>Whether a comparison in place of the pattern would bind looser than its parent.</summary>
	private static bool NeedsParentheses(IsPatternExpressionSyntax check)
	{
		return check.Parent switch
		{
			BinaryExpressionSyntax binary when IsEquality(binary) => binary.Right == check,
			BinaryExpressionSyntax binary => BindsTighterThanEquality(binary),
			IsPatternExpressionSyntax or SwitchExpressionSyntax or RangeExpressionSyntax => true,
			_ => false,
		};
	}

	private static bool BindsTighterThanEquality(BinaryExpressionSyntax binary)
	{
		return binary.Kind() switch
		{
			SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression or SyntaxKind.CoalesceExpression => false,
			SyntaxKind.BitwiseAndExpression or SyntaxKind.BitwiseOrExpression or SyntaxKind.ExclusiveOrExpression => false,
			_ => true,
		};
	}

	private static bool IsEquality(ExpressionSyntax expression) =>
		expression.IsKind(SyntaxKind.EqualsExpression) || expression.IsKind(SyntaxKind.NotEqualsExpression);

	/// <summary>Whether only whitespace follows the token up to the end, so no comment is dropped.</summary>
	private static bool IsLayoutUpTo(SyntaxToken first, int end)
	{
		for (SyntaxToken token = first; !token.IsKind(SyntaxKind.None); token = token.GetNextToken())
		{
			if (token != first && !token.LeadingTrivia.All(StanzaTrivia.IsLayout))
				return false;

			if (token.Span.End >= end)
				return true;

			if (!token.TrailingTrivia.All(StanzaTrivia.IsLayout))
				return false;
		}

		return true;
	}

	private static TextChange CreateInsertion(int position, string text) => new(new TextSpan(position, 0), text);

	private static TNode? FindNode<TNode>(SyntaxNode root, TextSpan span)
		where TNode : SyntaxNode
	{
		if (span.End > root.FullSpan.End)
			return null;

		return root.FindNode(span, getInnermostNodeForTie: true).AncestorsAndSelf().OfType<TNode>().FirstOrDefault(node => node.Span == span);
	}
}
