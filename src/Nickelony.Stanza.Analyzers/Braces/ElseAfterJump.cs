using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

/// <summary>Builds the changes that move the body of an else after a branch that ends in a jump.</summary>
internal static class ElseAfterJump
{
	/// <summary>Creates the changes that drop the else of the statement, or none when it must stay.</summary>
	/// <remarks>
	/// The statement must stand in a block, so the body lands among its siblings, and it must be clear of directives.
	/// </remarks>
	public static ImmutableArray<TextChange> CreateRemoval(
		SourceText sourceText,
		IfStatementSyntax ifStatement,
		bool separatesNeighbors = false,
		int indentStepColumns = StanzaText.DefaultIndentStepColumns
	)
	{
		if (ifStatement.Else is not { } elseClause || ifStatement.Parent is not BlockSyntax enclosing || ifStatement.ContainsDiagnostics)
			return [];

		if (!EndsInJump(ifStatement.Statement) || ReadMovedStatements(elseClause.Statement) is not { IsEmpty: false } moved)
			return [];

		if (StanzaSyntax.HasDirectiveNear(ifStatement) || moved.Any(IsUsingDeclarationOrEmpty))
			return [];

		// A name the body declares joins the block around it, where any use of the name binds to the new local.
		HashSet<string> names = [];

		foreach (StatementSyntax statement in moved)
			MovedLines.CollectLeakedNames(statement, names);

		if (names.Count > 0 && MovedLines.ReusesName(enclosing, names, elseClause.Statement.Span))
			return [];

		ImmutableArray<TextChange> move = CreateMove(sourceText, ifStatement, elseClause, moved, separatesNeighbors);

		// The statement stood tight against its siblings as a multi-line construct, which the multi-line rule would have separated.
		return separatesNeighbors && !move.IsEmpty
			? SiblingGaps.WithSeparations(sourceText, units: [ifStatement], changes: move, indentStepColumns)
			: move;
	}

	private static ImmutableArray<TextChange> CreateMove(
		SourceText sourceText,
		IfStatementSyntax ifStatement,
		ElseClauseSyntax elseClause,
		ImmutableArray<StatementSyntax> moved,
		bool separatesNeighbors
	)
	{
		SyntaxToken first = moved[0].GetFirstToken();
		SyntaxToken last = moved[moved.Length - 1].GetLastToken();

		int branchEnd = ifStatement.Statement.GetLastToken().Span.End;

		// The head holds the else, its brace and the gap before the first statement, and the tail the brace that closes the body.
		var head = TextSpan.FromBounds(branchEnd, first.SpanStart);

		TextSpan? tail = elseClause.Statement is BlockSyntax block
			? TextSpan.FromBounds(last.Span.End, MovedLines.FindCommentEnd(block.CloseBraceToken))
			: null;

		if (!MovedLines.TryReadComments(ifStatement, head, out ImmutableArray<SyntaxTrivia> headComments))
			return [];

		ImmutableArray<SyntaxTrivia> tailComments = [];

		if (tail is { } tailSpan && !MovedLines.TryReadComments(ifStatement, tailSpan, out tailComments))
			return [];

		(CommentPlacement placement, string firstIndentation, ImmutableArray<TextChange> dedent) = ReadPlacement(
			sourceText,
			ifStatement,
			elseClause,
			moved,
			movedSpan: TextSpan.FromBounds(first.SpanStart, last.Span.End),
			separatesNeighbors
		);

		ImmutableArray<TextChange>.Builder changes = ImmutableArray.CreateBuilder<TextChange>();

		string headText =
			MovedLines.BuildCommentText(
				sourceText,
				comments: headComments,
				anchorLineIndex: sourceText.Lines.GetLineFromPosition(branchEnd).LineNumber,
				placement
			)
			+ placement.LineEnding
			+ firstIndentation;

		changes.Add(new TextChange(head, headText));
		changes.AddRange(dedent);

		if (tail is { } closing)
		{
			changes.Add(
				MovedLines.CreateTail(
					sourceText,
					tail: closing,
					comments: tailComments,
					anchorLineIndex: sourceText.Lines.GetLineFromPosition(last.Span.End).LineNumber,

					placement: placement with
					{
						SeparatesFromAnchor = false,
					}
				)
			);
		}

		return changes.ToImmutable();
	}

	/// <summary>Reads the layout of the moved statements, the indentation their first takes and the dedent.</summary>
	/// <remarks>
	/// The statements stand one step deeper than the else, so they come out by that step when every line can take it.
	/// </remarks>
	private static (CommentPlacement Placement, string FirstIndentation, ImmutableArray<TextChange> Dedent) ReadPlacement(
		SourceText sourceText,
		IfStatementSyntax ifStatement,
		ElseClauseSyntax elseClause,
		ImmutableArray<StatementSyntax> moved,
		TextSpan movedSpan,
		bool separatesNeighbors
	)
	{
		SyntaxToken elseKeyword = elseClause.ElseKeyword;

		string lineEnding = StanzaText.ReadPreferredLineEnding(sourceText, sourceText.Lines.GetLineFromPosition(elseKeyword.SpanStart).LineNumber);
		string outerIndentation = StanzaText.GetIndentation(sourceText, elseKeyword.SpanStart);

		bool startsItsLine = StanzaText.StartsItsLine(sourceText, movedSpan.Start);
		string writtenIndentation = startsItsLine ? StanzaText.GetIndentation(sourceText, movedSpan.Start) : outerIndentation;

		ImmutableArray<TextChange> dedent = [];

		string? step = startsItsLine ? MovedLines.FindStep(outerIndentation, writtenIndentation) : null;

		bool dedented =
			step is not null
			&& MovedLines.TryCreateDedent(node: ifStatement, sourceText, span: movedSpan, outerIndentation, step, changes: out dedent);

		var placement = new CommentPlacement(
			LineEnding: lineEnding,
			Indentation: outerIndentation,
			DedentStep: dedented ? step : null,
			SeparatesFromAnchor: separatesNeighbors && IsGapBelowBranchAsked(sourceText, ifStatement, first: moved[0])
		);

		return (placement, dedented ? outerIndentation : writtenIndentation, dedent);
	}

	/// <summary>Whether the multi-line rule asks for a blank line above the first freed statement.</summary>
	/// <remarks>
	/// A statement on the line of the branch keeps no gap to write in, as the gaps beside the chain do not.
	/// </remarks>
	private static bool IsGapBelowBranchAsked(SourceText sourceText, IfStatementSyntax ifStatement, StatementSyntax first)
	{
		var branch = TextSpan.FromBounds(ifStatement.SpanStart, ifStatement.Statement.Span.End);

		bool sharesLine =
			sourceText.Lines.GetLineFromPosition(branch.End).LineNumber == sourceText.Lines.GetLineFromPosition(first.SpanStart).LineNumber;

		return !sharesLine && (StanzaSyntax.IsMultiLine(sourceText, branch) || StanzaSyntax.IsMultiLine(sourceText, first.Span));
	}

	private static bool EndsInJump(StatementSyntax branch)
	{
		StatementSyntax? last = branch is BlockSyntax block ? block.Statements.LastOrDefault() : branch;

		if (last is not null)
			last = MovedLines.SkipLabels(last);

		return last is ReturnStatementSyntax or ThrowStatementSyntax or BreakStatementSyntax or ContinueStatementSyntax or GotoStatementSyntax
			|| (last is YieldStatementSyntax yieldStatement && yieldStatement.IsKind(SyntaxKind.YieldBreakStatement));
	}

	/// <summary>Reads the statements the body of an else holds, or none for an else if.</summary>
	private static ImmutableArray<StatementSyntax> ReadMovedStatements(StatementSyntax body)
	{
		return body switch
		{
			IfStatementSyntax => [],
			BlockSyntax block => [.. block.Statements],
			_ => [body],
		};
	}

	/// <summary>Whether the statement is empty or a using declaration, whose disposal a move would delay.</summary>
	private static bool IsUsingDeclarationOrEmpty(StatementSyntax statement)
	{
		StatementSyntax unlabeled = MovedLines.SkipLabels(statement);

		return unlabeled is EmptyStatementSyntax
			|| (unlabeled is LocalDeclarationStatementSyntax declaration && declaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword));
	}
}
