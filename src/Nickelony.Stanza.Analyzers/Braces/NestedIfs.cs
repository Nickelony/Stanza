using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

/// <summary>Finds the ifs that merge into the condition of the one around them, and builds the merge.</summary>
internal static class NestedIfs
{
	/// <summary>Finds the ifs inside the statement that merge into its condition, outermost first, or none.</summary>
	/// <remarks>
	/// A limit of zero lets a condition grow without bound, as the long condition rule does in an <c>Equals</c> method.
	/// </remarks>
	public static ImmutableArray<IfStatementSyntax> FindMerges(IfStatementSyntax outer, int limit, int maxLevels = int.MaxValue)
	{
		if (outer.Else is not null || outer.ContainsDiagnostics || StanzaSyntax.HasDirectiveNear(outer))
			return [];

		ImmutableArray<IfStatementSyntax>.Builder merged = ImmutableArray.CreateBuilder<IfStatementSyntax>();
		bool limited = limit > 0 && !LogicalConditions.IsInSkippedMember(outer);
		int operators = LogicalConditions.CountOperators(outer.Condition);
		IfStatementSyntax current = outer;

		while (merged.Count < maxLevels && FindInner(current) is { } inner)
		{
			operators += LogicalConditions.CountOperators(inner.Condition) + 1;

			if ((limited && operators > limit) || !CanMerge(outer, current, inner))
				break;

			merged.Add(inner);
			current = inner;
		}

		return merged.ToImmutable();
	}

	/// <summary>Finds the if whose only statement is the one given, or nothing.</summary>
	public static IfStatementSyntax? FindMergingParent(IfStatementSyntax ifStatement)
	{
		return ifStatement.Parent switch
		{
			IfStatementSyntax parent when parent.Statement == ifStatement => parent,
			BlockSyntax { Parent: IfStatementSyntax parent } block when block.Statements.Count == 1 && parent.Statement == block => parent,
			_ => null,
		};
	}

	/// <summary>Creates the changes that merge the given number of ifs into the outer condition, or none.</summary>
	public static ImmutableArray<TextChange> CreateMerge(
		SourceText sourceText,
		IfStatementSyntax outer,
		int count,
		bool separatesNeighbors = false,
		int indentStepColumns = StanzaText.DefaultIndentStepColumns
	)
	{
		ImmutableArray<IfStatementSyntax> merged = FindMerges(outer, limit: 0, maxLevels: count);

		if (count < 1 || merged.Length != count)
			return [];

		IfStatementSyntax innermost = merged[merged.Length - 1];

		SyntaxToken first = innermost.Statement.GetFirstToken();
		SyntaxToken last = innermost.Statement.GetLastToken();

		// The braces of each if but the innermost go, and the last of them closes the text that the tail covers.
		TextSpan? tail = FindTail(outer, merged, last);
		ImmutableArray<SyntaxTrivia> tailComments = [];

		if (tail is { } tailSpan && !MovedLines.TryReadComments(outer, tailSpan, out tailComments))
			return [];

		string lineEnding = StanzaText.ReadPreferredLineEnding(
			sourceText,
			sourceText.Lines.GetLineFromPosition(outer.IfKeyword.SpanStart).LineNumber
		);

		string outerIndentation = StanzaText.GetIndentation(sourceText, outer.IfKeyword.SpanStart);

		// The innermost body stands one step for each if deeper than the outer one, so it comes out by those steps when every line can take them.
		string? step = StanzaText.StartsItsLine(sourceText, innermost.IfKeyword.SpanStart)
			? MovedLines.FindStep(outerIndentation, StanzaText.GetIndentation(sourceText, innermost.IfKeyword.SpanStart))
			: null;

		bool bodyStartsItsLine = StanzaText.StartsItsLine(sourceText, first.SpanStart);
		string bodyIndentation = bodyStartsItsLine ? StanzaText.GetIndentation(sourceText, first.SpanStart) : string.Empty;
		ImmutableArray<TextChange> dedent = [];

		bool dedented =
			step is not null
			&& (!bodyStartsItsLine || bodyIndentation.StartsWith(outerIndentation + step, StringComparison.Ordinal))
			&& MovedLines.TryCreateDedent(
				node: outer,
				sourceText,
				span: TextSpan.FromBounds(first.SpanStart, last.Span.End),
				outerIndentation,
				step,
				changes: out dedent
			);

		ImmutableArray<TextChange>.Builder changes = ImmutableArray.CreateBuilder<TextChange>();
		changes.AddRange(CreateConditionJoin(sourceText, outer, merged, outerIndentation));

		// A body on a line of its own keeps its line, and one that follows the head on its line keeps what lay between.
		string gap = bodyStartsItsLine
			? lineEnding + (dedented ? outerIndentation + bodyIndentation.Substring(outerIndentation.Length + step!.Length) : bodyIndentation)
			: sourceText.ToString(TextSpan.FromBounds(innermost.CloseParenToken.Span.End, first.SpanStart));

		changes.Add(new TextChange(TextSpan.FromBounds(outer.CloseParenToken.Span.End, first.SpanStart), gap));
		changes.AddRange(dedent);

		if (tail is { } closed)
		{
			changes.Add(
				MovedLines.CreateTail(
					sourceText,
					tail: closed,
					comments: tailComments,
					anchorLineIndex: sourceText.Lines.GetLineFromPosition(last.Span.End).LineNumber,
					placement: new CommentPlacement(lineEnding, Indentation: outerIndentation, DedentStep: dedented ? step : null)
				)
			);
		}

		// The statement stood tight against its siblings as a multi-line construct, which the multi-line rule would have separated.
		return separatesNeighbors ? SiblingGaps.WithSeparations(sourceText, units: [outer], changes, indentStepColumns) : changes.ToImmutable();
	}

	/// <summary>Creates the changes that join the merged conditions to the outer one with <c>&amp;&amp;</c>.</summary>
	private static ImmutableArray<TextChange> CreateConditionJoin(
		SourceText sourceText,
		IfStatementSyntax outer,
		ImmutableArray<IfStatementSyntax> merged,
		string outerIndentation
	)
	{
		var operands = new StringBuilder();

		foreach (IfStatementSyntax level in merged)
		{
			string operand = ReadOperand(sourceText, level, outerIndentation);
			operands.Append(" && ").Append(NeedsParentheses(level.Condition) ? "(" + operand + ")" : operand);
		}

		ExpressionSyntax condition = outer.Condition;

		return NeedsParentheses(condition)
			? [new TextChange(new TextSpan(condition.SpanStart, 0), "("), new TextChange(new TextSpan(condition.Span.End, 0), ")" + operands)]
			: [new TextChange(new TextSpan(condition.Span.End, 0), operands.ToString())];
	}

	/// <summary>Finds the if inside the one given that stands alone in its body and has no else, or nothing.</summary>
	private static IfStatementSyntax? FindInner(IfStatementSyntax current)
	{
		StatementSyntax body = current.Statement is BlockSyntax { Statements.Count: 1 } block ? block.Statements[0] : current.Statement;
		return body is IfStatementSyntax { Else: null } inner ? inner : null;
	}

	private static bool CanMerge(IfStatementSyntax outer, IfStatementSyntax current, IfStatementSyntax inner)
	{
		// Between the two heads there is nothing but layout, apart from the condition that moves, so no comment loses its line.
		var gap = TextSpan.FromBounds(current.CloseParenToken.Span.End, inner.Statement.GetFirstToken().SpanStart);

		foreach (SyntaxTrivia trivia in current.DescendantTrivia(gap))
		{
			bool inside = trivia.SpanStart >= gap.Start && trivia.Span.End <= gap.End;

			if (inside && !inner.Condition.Span.Contains(trivia.Span) && !StanzaTrivia.IsLayout(trivia))
				return false;
		}

		foreach (SyntaxToken token in inner.Condition.DescendantTokens())
		{
			if (token.Text.IndexOfAny(['\r', '\n']) >= 0)
				return false;
		}

		// A variable the condition declares joins the scope around the outer if, where any other use of its name binds to it.
		HashSet<string> declared = [];
		MovedLines.CollectDesignations(inner.Condition, declared);

		return declared.Count == 0 || !MovedLines.ReusesName(GetVariableScope(outer), declared, inner.Condition.Span, inner.Statement.Span);
	}

	private static SyntaxNode GetVariableScope(IfStatementSyntax outer)
	{
		// A label does not open a scope, so the block around it holds the names.
		SyntaxNode statement = outer;

		while (statement.Parent is LabeledStatementSyntax labeled)
			statement = labeled;

		return statement.Parent switch
		{
			BlockSyntax block => block,
			SwitchSectionSyntax { Parent: { } switchStatement } => switchStatement,
			GlobalStatementSyntax { Parent: { } unit } => unit,
			_ => statement,
		};
	}

	private static TextSpan? FindTail(IfStatementSyntax outer, ImmutableArray<IfStatementSyntax> merged, SyntaxToken last)
	{
		SyntaxToken? lastBrace = null;

		for (int index = -1; index < merged.Length - 1; index++)
		{
			IfStatementSyntax head = index < 0 ? outer : merged[index];

			if (head.Statement is BlockSyntax block && (lastBrace is null || block.CloseBraceToken.SpanStart > lastBrace.Value.SpanStart))
				lastBrace = block.CloseBraceToken;
		}

		return lastBrace is { } brace ? TextSpan.FromBounds(last.Span.End, MovedLines.FindCommentEnd(brace)) : null;
	}

	/// <summary>Reads the condition as written, with each line after the first moved out as its if is.</summary>
	private static string ReadOperand(SourceText sourceText, IfStatementSyntax level, string outerIndentation)
	{
		string text = sourceText.ToString(level.Condition.Span);

		string? step = StanzaText.StartsItsLine(sourceText, level.IfKeyword.SpanStart)
			? MovedLines.FindStep(outerIndentation, StanzaText.GetIndentation(sourceText, level.IfKeyword.SpanStart))
			: null;

		return MovedLines.ReadDedented(text, outerIndentation, step);
	}

	/// <summary>Whether <c>&amp;&amp;</c> binds tighter than the operand, so the merge must parenthesize it.</summary>
	private static bool NeedsParentheses(ExpressionSyntax operand)
	{
		return operand.Kind()
				is SyntaxKind.LogicalOrExpression
					or SyntaxKind.CoalesceExpression
					or SyntaxKind.ConditionalExpression
					or SyntaxKind.ThrowExpression
			|| operand is AssignmentExpressionSyntax or AnonymousFunctionExpressionSyntax or QueryExpressionSyntax;
	}
}
