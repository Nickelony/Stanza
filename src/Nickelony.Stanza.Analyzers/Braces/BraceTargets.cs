using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

/// <summary>Finds the bodies of a statement that the brace rules judge.</summary>
internal static class BraceTargets
{
	/// <summary>The nesting at which the walk down a condition stops, as the walk is recursive.</summary>
	private const int MaxConstantDepth = 32;

	public static ImmutableArray<BraceTarget> GetTargets(SyntaxNode statement)
	{
		return statement switch
		{
			// An "else if" belongs to the outer chain, so it is never judged on its own.
			IfStatementSyntax { Parent: ElseClauseSyntax } => [],
			IfStatementSyntax ifStatement => GetChainTargets(ifStatement),
			_ when FindEmbeddedStatement(statement) is { } body && BuildTarget(statement.GetFirstToken(), body) is { } target => [target],
			_ => [],
		};
	}

	/// <summary>Climbs from an else if to the if that heads its chain.</summary>
	public static IfStatementSyntax GetChainHead(IfStatementSyntax ifStatement)
	{
		while (ifStatement.Parent is ElseClauseSyntax { Parent: IfStatementSyntax head })
			ifStatement = head;

		return ifStatement;
	}

	/// <summary>Whether the block of an <see langword="else"/> holds an <see langword="if"/> alone.</summary>
	public static bool IsElseHoldingAnIf(BlockSyntax block) =>
		block.Parent is ElseClauseSyntax && block.Statements.Count == 1 && block.Statements[0] is IfStatementSyntax;

	/// <summary>Whether a conditional or a disabled <see langword="else"/> can change the branches.</summary>
	public static bool HoldsAConditionalBranch(SyntaxNode statement)
	{
		if (HasConditionalDirectiveAmongBranches(statement))
			return true;

		// Disabled text above the statement that ends in an else makes it the branch of a chain in another view.
		if (HasLiveDisabledElse(statement.GetFirstToken().LeadingTrivia, startsWithElse: false))
			return true;

		return CanTakeAnElse(statement) && IsFollowedByElseOfAnotherView(statement);
	}

	/// <summary>Whether a conditional directive stands among the branches of the statement.</summary>
	private static bool HasConditionalDirectiveAmongBranches(SyntaxNode statement)
	{
		if (!statement.ContainsDirectives)
			return false;

		// The while of a do loop follows its only branch, so a directive there changes none.
		// A directive above the statement is its first token's trivia, outside the span, so it is not judged here.
		int end = statement is DoStatementSyntax doStatement ? doStatement.Statement.Span.End : statement.Span.End;

		foreach (SyntaxTrivia trivia in statement.DescendantTrivia(statement.Span))
		{
			if (StanzaSyntax.IsConditionalDirective(trivia) && trivia.SpanStart < end)
				return true;
		}

		return false;
	}

	/// <summary>Whether another view joins an <see langword="else"/> to the statement.</summary>
	/// <remarks>
	/// The <see langword="else"/> is disabled text in an inactive <c>#if</c>, or a token after a conditional region.
	/// </remarks>
	public static bool IsFollowedByElseOfAnotherView(SyntaxNode statement)
	{
		// The walk stops once the regions after the statement close, and can reach the end of the file.
		int depth = 0;
		SyntaxToken token = statement.GetLastToken().GetNextToken(includeZeroWidth: true);

		while (!token.IsKind(SyntaxKind.None))
		{
			SyntaxTriviaList leading = token.LeadingTrivia;
			bool closesARegion = false;

			foreach (SyntaxTrivia trivia in leading)
			{
				if (trivia.IsKind(SyntaxKind.IfDirectiveTrivia))
				{
					depth++;
				}
				else if (trivia.IsKind(SyntaxKind.EndIfDirectiveTrivia))
				{
					depth--;
					closesARegion |= depth == 0;
				}
			}

			if (HasLiveDisabledElse(leading, startsWithElse: true) || (closesARegion && token.IsKind(SyntaxKind.ElseKeyword)))
				return true;

			if (depth <= 0)
				return false;

			token = token.GetNextToken(includeZeroWidth: true);
		}

		return false;
	}

	/// <summary>Whether live disabled text in the trivia starts or ends in an <see langword="else"/>.</summary>
	private static bool HasLiveDisabledElse(SyntaxTriviaList triviaList, bool startsWithElse)
	{
		for (int index = 0; index < triviaList.Count; index++)
		{
			SyntaxTrivia trivia = triviaList[index];

			if (trivia.IsKind(SyntaxKind.DisabledTextTrivia) && HasElseAtEdge(trivia, startsWithElse) && !IsNeverEnabled(triviaList, index))
				return true;
		}

		return false;
	}

	/// <summary>Whether the first or the last token of the disabled text is an <see langword="else"/>.</summary>
	private static bool HasElseAtEdge(SyntaxTrivia disabledText, bool startsWithElse)
	{
		string text = disabledText.ToString();

		if (!text.Contains("else", StringComparison.Ordinal))
			return false;

		IEnumerable<SyntaxToken> tokens = SyntaxFactory.ParseTokens(text).Where(static token => !token.IsKind(SyntaxKind.EndOfFileToken));
		return (startsWithElse ? tokens.FirstOrDefault() : tokens.LastOrDefault()).IsKind(SyntaxKind.ElseKeyword);
	}

	/// <summary>Whether the directive above the disabled text opens an arm that no set of symbols enables.</summary>
	private static bool IsNeverEnabled(SyntaxTriviaList triviaList, int index)
	{
		if (index == 0 || triviaList[index - 1].GetStructure() is not DirectiveTriviaSyntax directive)
			return false;

		foreach (DirectiveTriviaSyntax related in directive.GetRelatedDirectives())
		{
			bool? condition = related switch
			{
				IfDirectiveTriviaSyntax ifDirective => ReadConstant(ifDirective.Condition, depth: 0),
				ElifDirectiveTriviaSyntax elifDirective => ReadConstant(elifDirective.Condition, depth: 0),
				_ => null,
			};

			// An arm after one that is always taken is never reached either.
			if (related.SpanStart == directive.SpanStart)
				return condition is false;

			if (condition is true)
				return true;
		}

		return false;
	}

	/// <summary>Reads a condition no symbol changes, or <see langword="null"/> when the symbols decide it.</summary>
	private static bool? ReadConstant(ExpressionSyntax expression, int depth)
	{
		// A long chain of operators is left undecided, as the walk down it is recursive.
		if (depth > MaxConstantDepth)
			return null;

		if (expression.IsKind(SyntaxKind.TrueLiteralExpression))
			return true;

		if (expression.IsKind(SyntaxKind.FalseLiteralExpression))
			return false;

		if (expression is ParenthesizedExpressionSyntax parenthesized)
			return ReadConstant(parenthesized.Expression, depth + 1);

		if (expression is PrefixUnaryExpressionSyntax unary && unary.IsKind(SyntaxKind.LogicalNotExpression))
			return !ReadConstant(unary.Operand, depth + 1);

		if (
			expression is not BinaryExpressionSyntax binary
			|| binary.Kind() is not (SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression)
		)
		{
			return null;
		}

		// The operand that settles the join alone is false for an and, and true for an or.
		bool? left = ReadConstant(binary.Left, depth + 1);
		bool? right = ReadConstant(binary.Right, depth + 1);

		bool decisive = binary.IsKind(SyntaxKind.LogicalOrExpression);

		if (left == decisive || right == decisive)
			return decisive;

		return left is null || right is null ? null : !decisive;
	}

	/// <summary>Whether the statement is a chain with no bare <see langword="else"/> at its end.</summary>
	private static bool CanTakeAnElse(SyntaxNode statement)
	{
		while (statement is IfStatementSyntax ifStatement)
		{
			if (ifStatement.Else is not { } elseClause)
				return true;

			statement = elseClause.Statement;
		}

		return false;
	}

	public static bool HasMultiLineTarget(ImmutableArray<BraceTarget> targets)
	{
		foreach (BraceTarget target in targets)
		{
			if (target.IsMultiLine)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Reads the embedded statement off its owner, as a search would allocate an enumerator for every statement.
	/// </summary>
	private static StatementSyntax? FindEmbeddedStatement(SyntaxNode statement)
	{
		return statement switch
		{
			DoStatementSyntax doStatement => doStatement.Statement,
			FixedStatementSyntax fixedStatement => fixedStatement.Statement,
			CommonForEachStatementSyntax forEachStatement => forEachStatement.Statement,
			ForStatementSyntax forStatement => forStatement.Statement,
			LockStatementSyntax lockStatement => lockStatement.Statement,
			UsingStatementSyntax usingStatement => usingStatement.Statement,
			WhileStatementSyntax whileStatement => whileStatement.Statement,
			_ => null,
		};
	}

	public static BraceTarget? FindTarget(SyntaxNode anchor)
	{
		for (SyntaxNode? owner = anchor.Parent; owner is not null; owner = owner.Parent)
		{
			foreach (BraceTarget candidate in GetTargets(owner))
			{
				if (candidate.Body.Span == anchor.Span)
					return candidate;
			}
		}

		return null;
	}

	public static bool CanSwallowElse(StatementSyntax statement)
	{
		while (true)
		{
			if (statement is IfStatementSyntax { Else: null })
				return true;

			// A `do` body ends before its `while`, so an else after the statement cannot reach into it.
			StatementSyntax? inner = statement switch
			{
				IfStatementSyntax ifStatement => ifStatement.Else!.Statement,
				LabeledStatementSyntax labeledStatement => labeledStatement.Statement,
				DoStatementSyntax => null,
				_ => FindEmbeddedStatement(statement),
			};

			// A block closes with its brace, so an else after it never reaches the statements inside.
			if (inner is null)
				return false;

			statement = inner;
		}
	}

	private static BraceTarget? BuildTarget(SyntaxToken keyword, StatementSyntax body)
	{
		SyntaxToken bodyStart = body.GetFirstToken();

		// An incomplete tree has a placeholder body that carries no token at all,
		// so there is no header to measure.
		if (bodyStart.IsKind(SyntaxKind.None))
			return null;

		SyntaxToken headerEnd = bodyStart.GetPreviousToken();

		if (headerEnd.Span.End < keyword.SpanStart)
			return null;

		var headerSpan = TextSpan.FromBounds(keyword.SpanStart, headerEnd.Span.End);
		bool isMultiLine = StanzaSyntax.IsMultiLine(body.SyntaxTree, headerSpan) || IsMultiLineBody(body);

		return new BraceTarget(headerSpan, body, isMultiLine);
	}

	private static ImmutableArray<BraceTarget> GetChainTargets(IfStatementSyntax ifStatement)
	{
		ImmutableArray<BraceTarget>.Builder targets = ImmutableArray.CreateBuilder<BraceTarget>();
		IfStatementSyntax current = ifStatement;

		while (true)
		{
			if (BuildTarget(current.IfKeyword, current.Statement) is { } target)
				targets.Add(target);

			if (current.Else is not { } elseClause)
				return targets.ToImmutable();

			// An "else if" continues the chain, a bare "else" ends it.
			if (elseClause.Statement is IfStatementSyntax nested)
			{
				current = nested;
				continue;
			}

			if (BuildTarget(elseClause.ElseKeyword, elseClause.Statement) is { } elseTarget)
				targets.Add(elseTarget);

			return targets.ToImmutable();
		}
	}

	private static bool IsMultiLineBody(StatementSyntax body)
	{
		if (body is not BlockSyntax block)
			return StanzaSyntax.IsMultiLine(body);

		int statementCount = block.Statements.Count;
		return statementCount > 1 || (statementCount == 1 && StanzaSyntax.IsMultiLine(block.Statements[0])) || HoldsALineBesidesItsStatement(block);
	}

	/// <summary>Whether a line of the block holds a comment or a directive besides the statement.</summary>
	private static bool HoldsALineBesidesItsStatement(BlockSyntax block)
	{
		SyntaxToken open = block.OpenBraceToken;
		SyntaxToken close = block.CloseBraceToken;

		if (block.Statements.Count != 1)
			return HasNonLayoutTrivia(open.TrailingTrivia) || HasNonLayoutTrivia(close.LeadingTrivia) || HasNonLayoutTrivia(close.TrailingTrivia);

		StatementSyntax statement = block.Statements[0];
		SyntaxTree syntaxTree = block.SyntaxTree;

		// A comment on the statement's own line stays there when the braces go, so only the other lines count.
		bool openHoldsALine =
			HasNonLayoutTrivia(open.TrailingTrivia) && StanzaSyntax.IsMultiLine(syntaxTree, TextSpan.FromBounds(open.SpanStart, statement.SpanStart));

		bool closeHoldsALine =
			HasNonLayoutTrivia(close.TrailingTrivia)
			&& StanzaSyntax.IsMultiLine(syntaxTree, TextSpan.FromBounds(statement.Span.End, close.SpanStart));

		return openHoldsALine
			|| HasNonLayoutTrivia(close.LeadingTrivia)
			|| closeHoldsALine
			|| HoldsALineAbove(statement.GetFirstToken().LeadingTrivia)
			|| HasMultiLineComment(statement.GetLastToken().TrailingTrivia);
	}

	/// <summary>Whether a comment in the trivia spans lines, so its last line holds no statement.</summary>
	private static bool HasMultiLineComment(SyntaxTriviaList triviaList)
	{
		foreach (SyntaxTrivia trivia in triviaList)
		{
			if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) && StanzaSyntax.IsMultiLine(trivia.SyntaxTree, trivia.Span))
				return true;
		}

		return false;
	}

	private static bool HasNonLayoutTrivia(SyntaxTriviaList triviaList)
	{
		foreach (SyntaxTrivia trivia in triviaList)
		{
			if (!StanzaTrivia.IsLayout(trivia))
				return true;
		}

		return false;
	}

	/// <summary>Whether the trivia before a statement holds a directive, or a comment on a line above it.</summary>
	private static bool HoldsALineAbove(SyntaxTriviaList triviaList)
	{
		for (int index = 0; index < triviaList.Count; index++)
		{
			SyntaxTrivia trivia = triviaList[index];

			if (StanzaTrivia.IsLayout(trivia))
				continue;

			if (trivia.IsDirective || EndsLineBeforeStatement(triviaList, index))
				return true;
		}

		return false;
	}

	private static bool EndsLineBeforeStatement(SyntaxTriviaList triviaList, int index)
	{
		if (StanzaSyntax.IsMultiLine(triviaList[index].SyntaxTree, triviaList[index].Span))
			return true;

		for (int next = index + 1; next < triviaList.Count; next++)
		{
			if (triviaList[next].IsKind(SyntaxKind.EndOfLineTrivia))
				return true;
		}

		return false;
	}
}
