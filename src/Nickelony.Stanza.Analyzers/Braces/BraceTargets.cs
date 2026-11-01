using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

/// <summary>Finds the bodies of a statement that the brace rules judge.</summary>
internal static class BraceTargets
{
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

	/// <summary>Whether the block of an <see langword="else"/> holds an <see langword="if"/> alone.</summary>
	public static bool IsElseHoldingAnIf(BlockSyntax block) =>
		block.Parent is ElseClauseSyntax && block.Statements.Count == 1 && block.Statements[0] is IfStatementSyntax;

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
		// A comment on the statement's own line stays there when the braces go, so only the other lines count.
		bool holdsOnTheBraces =
			HasNonLayoutTrivia(block.OpenBraceToken.TrailingTrivia)
			|| HasNonLayoutTrivia(block.CloseBraceToken.LeadingTrivia)
			|| HasNonLayoutTrivia(block.CloseBraceToken.TrailingTrivia);

		return holdsOnTheBraces || (block.Statements.Count == 1 && HoldsALineAbove(block.Statements[0].GetFirstToken().LeadingTrivia));
	}

	private static bool HasNonLayoutTrivia(SyntaxTriviaList triviaList)
	{
		foreach (SyntaxTrivia trivia in triviaList)
		{
			if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
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

			if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia))
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
