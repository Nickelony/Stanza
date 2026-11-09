using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

/// <summary>Builds the text changes that add, remove or join the braces of a block.</summary>
internal static class BraceEdits
{
	/// <summary>A diagnostic carries one bit for each joined unit, and an int holds this many.</summary>
	private const int MaxJoinedUnits = 30;

	/// <summary>Adds the braces around a body on their own lines, leaving every existing token unchanged.</summary>
	public static ImmutableArray<TextChange> CreateBraceInsertions(SourceText sourceText, TextSpan headerSpan, StatementSyntax body)
	{
		TextLine bodyLine = sourceText.Lines.GetLineFromPosition(body.SpanStart);

		if (!StanzaText.StartsItsLine(sourceText, body.SpanStart))
			return [];

		if (body.ContainsDirectives)
			return [];

		// Braces close an if at the end of the body, so an else that another view joins to it would be left without one.
		if (BraceTargets.CanSwallowElse(body) && BraceTargets.IsFollowedByElseOfAnotherView(body))
			return [];

		// The closing brace opens a line of its own,
		// so a body is not wrapped when a following statement shares its last line:
		// the brace and that statement would end up together.
		SyntaxToken bodyEnd = body.GetLastToken();

		if (HasATokenAfterItOnItsLine(sourceText, bodyEnd))
			return [];

		int headerLastLineIndex = sourceText.Lines.GetLineFromPosition(headerSpan.End).LineNumber;

		// A comment above the body travels in with the unit it belongs to.
		StanzaSyntax.LeadingUnit leadingUnit = StanzaSyntax.GetLeadingUnit(body);

		int insertPosition =
			leadingUnit.StartLineIndex < bodyLine.LineNumber && leadingUnit.StartLineIndex > headerLastLineIndex
				? sourceText.Lines[leadingUnit.StartLineIndex].Start
				: bodyLine.Start;

		// The opening brace takes the line breaks between the header and the insert position away,
		// so the gap may hold only whitespace:
		// a comment there that the leading unit does not carry would be deleted.
		if (!StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(sourceText.Lines[headerLastLineIndex].End, insertPosition)))
			return [];

		string lineEnding = StanzaText.ReadPreferredLineEnding(sourceText, bodyLine.LineNumber);
		string indentation = StanzaText.GetIndentation(sourceText, headerSpan.Start);
		string openBraceText = indentation + "{" + lineEnding;
		string closeBraceText = lineEnding + indentation + "}";

		return
		[
			new TextChange(TextSpan.FromBounds(sourceText.Lines[headerLastLineIndex].End, insertPosition), lineEnding + openBraceText),
			new TextChange(new TextSpan(StanzaBraces.GetClosingBracePosition(bodyEnd), 0), closeBraceText),
		];
	}

	/// <summary>Finds the bodies of a chain that the braces rule would wrap, which needs a multi-line branch.</summary>
	public static ImmutableArray<BraceTarget> FindBodiesToWrap(SourceText sourceText, ImmutableArray<BraceTarget> targets)
	{
		if (!BraceTargets.HasMultiLineTarget(targets))
			return [];

		ImmutableArray<BraceTarget>.Builder bodies = ImmutableArray.CreateBuilder<BraceTarget>();

		foreach (BraceTarget target in targets)
		{
			if (target.Block is null && !IsStacked(target.Body) && !CreateBraceInsertions(sourceText, target.HeaderSpan, target.Body).IsEmpty)
				bodies.Add(target);
		}

		return bodies.ToImmutable();
	}

	/// <summary>Whether a statement that stacks holds a body of its own kind.</summary>
	private static bool IsStacked(StatementSyntax body)
	{
		return body.Parent switch
		{
			UsingStatementSyntax => body is UsingStatementSyntax,
			LockStatementSyntax => body is LockStatementSyntax,
			FixedStatementSyntax => body is FixedStatementSyntax,
			_ => false,
		};
	}

	/// <summary>Whether the braces rule would wrap a body of the chain that the statement belongs to.</summary>
	public static bool HasBodiesToWrap(SourceText sourceText, StanzaOptions options, IfStatementSyntax ifStatement)
	{
		return options.PreferBraces is not false
			&& !FindBodiesToWrap(sourceText, BraceTargets.GetTargets(BraceTargets.GetChainHead(ifStatement))).IsEmpty;
	}

	/// <summary>Whether the rule waits, as the braces rule is on and would wrap a body of the chain first.</summary>
	public static bool IsWaitingForBraces(SyntaxNodeAnalysisContext context, StanzaRule rule, SourceText sourceText, IfStatementSyntax ifStatement)
	{
		var options = StanzaOptions.From(context.Options, ifStatement.SyntaxTree);

		return StanzaRuleState.IsRequiredDeferralOnAt(
				scope: context,
				options,
				rule,
				target: DiagnosticDescriptors.UseBracesConsistently,
				position: ifStatement.SpanStart
			) && HasBodiesToWrap(sourceText, options, ifStatement);
	}

	/// <summary>Whether only empty blocks the collapse rule joins make the chain multi-line.</summary>
	/// <remarks>
	/// Braces written first would outlive the line breaks that asked for them, so the rule waits.
	/// </remarks>
	public static bool IsWaitingForCollapse(SyntaxNodeAnalysisContext context, StanzaRule rule, StanzaOptions options, SourceText sourceText)
	{
		if (!options.PreserveSingleLineBlocks)
			return false;

		SyntaxNode statement = context.Node;
		ImmutableArray<TextChange>.Builder collapses = ImmutableArray.CreateBuilder<TextChange>();
		int removed = 0;

		foreach (SyntaxNode node in statement.DescendantNodes())
		{
			if (!StanzaKinds.EmptyBlockKinds.Contains(node.Kind()) || StanzaBraces.FindBracePair(node) is not { } braces)
				continue;

			if (CreateEmptyBlockCollapse(sourceText, braces.Previous, braces.Next) is not { } collapse)
				continue;

			DiagnosticDescriptor target = DiagnosticDescriptors.CollapseEmptyBlocks;

			if (!StanzaRuleState.IsRequiredDeferralOnAt(scope: context, options, rule, target, position: braces.Next.SpanStart))
				continue;

			collapses.Add(collapse);
			removed += collapse.Span.Length - collapse.NewText!.Length;
		}

		if (collapses.Count == 0)
			return false;

		// The statement is read again as the collapses leave it, and its chain judged as this rule would judge it then.
		SourceText collapsed = sourceText.WithChanges(collapses);
		string text = collapsed.ToString(TextSpan.FromBounds(statement.SpanStart, statement.Span.End - removed));
		StatementSyntax reparsed = SyntaxFactory.ParseStatement(text, options: statement.SyntaxTree.Options);

		return !BraceTargets.HasMultiLineTarget(BraceTargets.GetTargets(reparsed));
	}

	/// <summary>Whether the multi-line rule is on, so a fix keeps the blank lines it asks around a statement.</summary>
	public static bool IsSeparatingNeighbors(SyntaxNodeAnalysisContext context, StanzaRule rule, SyntaxNode unit)
	{
		var options = StanzaOptions.From(context.Options, unit.SyntaxTree);
		StanzaRule separating = rule.GetDeferral(DiagnosticDescriptors.SeparateMultiLineConstructs);

		return SiblingGaps.IsSeparatingNeighbors(scope: context, options, separating, unit);
	}

	private static bool HasATokenAfterItOnItsLine(SourceText sourceText, SyntaxToken token)
	{
		SyntaxToken next = token.GetNextToken();
		return sourceText.Lines.GetLineFromPosition(next.SpanStart).LineNumber == sourceText.Lines.GetLineFromPosition(token.Span.End).LineNumber;
	}

	public static ImmutableArray<TextChange> CreateSafeBraceRemoval(SourceText sourceText, BraceTarget target, int tabColumns)
	{
		if (target.Block is not { } block)
			return [];

		ImmutableArray<TextChange> removal = CreateBraceRemoval(sourceText, block, tabColumns);
		bool canRemove = !removal.IsEmpty && (!BraceTargets.CanSwallowElse(block.Statements[0]) || !IsFollowedByElseOrDirective(block));

		return canRemove ? removal : [];
	}

	/// <summary>Whether an <see langword="else"/> or a directive follows the block.</summary>
	/// <remarks>
	/// An <see langword="else"/> in an inactive <c>#if</c> is disabled text, not a token.
	/// </remarks>
	private static bool IsFollowedByElseOrDirective(BlockSyntax block)
	{
		SyntaxToken next = block.CloseBraceToken.GetNextToken();

		// A Fix All removes any set of the reported blocks, each judged on the text as it stands.
		// An enclosing block of a loop or an if whose one statement cannot take an else may lose its braces too,
		// and then the else after it follows this block, so the walk passes its closing brace.
		while (!StanzaSyntax.HasDirectiveOrDisabledText(next.LeadingTrivia) && ClosesABlockThatMayLoseItsBraces(next))
			next = next.GetNextToken();

		return next.IsKind(SyntaxKind.ElseKeyword) || StanzaSyntax.HasDirectiveOrDisabledText(next.LeadingTrivia);
	}

	/// <summary>Whether the token closes a judged block whose one statement takes no else.</summary>
	private static bool ClosesABlockThatMayLoseItsBraces(SyntaxToken token)
	{
		if (token.Parent is not BlockSyntax { Statements.Count: 1 } enclosing || enclosing.CloseBraceToken != token)
			return false;

		// A method body, a try block and the like are no target, so they keep their braces.
		return !BraceTargets.CanSwallowElse(enclosing.Statements[0]) && BraceTargets.FindTarget(enclosing) is not null;
	}

	/// <summary>Refuses a removal the compiler needs, or one that makes an <c>else if</c> from a block.</summary>
	/// <remarks>
	/// It also refuses a layout that is not a plain one-statement block.
	/// </remarks>
	private static ImmutableArray<TextChange> CreateBraceRemoval(SourceText sourceText, BlockSyntax block, int tabColumns)
	{
		if (block.Statements.Count != 1)
			return [];

		if (block.Statements[0] is EmptyStatementSyntax)
			return [];

		if (block.Statements[0] is LocalDeclarationStatementSyntax or LabeledStatementSyntax or LocalFunctionStatementSyntax)
			return [];

		if (BraceTargets.IsElseHoldingAnIf(block))
			return [];

		TextLine openLine = sourceText.Lines.GetLineFromPosition(block.OpenBraceToken.SpanStart);
		TextLine closeLine = sourceText.Lines.GetLineFromPosition(block.CloseBraceToken.SpanStart);

		TextLine bodyStartLine = sourceText.Lines.GetLineFromPosition(block.Statements[0].SpanStart);
		TextLine bodyEndLine = sourceText.Lines.GetLineFromPosition(block.Statements[0].Span.End);

		ImmutableArray<TextChange> removal = [];

		bool bodyFillsTheLinesBetween = bodyStartLine.LineNumber == openLine.LineNumber + 1 && bodyEndLine.LineNumber == closeLine.LineNumber - 1;
		bool bracesStandAlone = IsLineAlone(sourceText, openLine, '{') && IsLineAlone(sourceText, closeLine, '}');

		if (
			bodyFillsTheLinesBetween
			&& bracesStandAlone
			&& IsIndentedDeeper(sourceText, inner: bodyStartLine, outer: openLine, tabColumns)
			&& !block.ContainsDirectives
		)
		{
			removal =
			[
				new TextChange(GetLineWithBreak(sourceText, openLine.LineNumber), string.Empty),
				new TextChange(GetClosingBraceLineWithBlankLines(sourceText, block, closeLine), string.Empty),
			];
		}

		return removal;
	}

	/// <summary>Spans the closing brace line and the blank lines above the clause keyword that follows it.</summary>
	private static TextSpan GetClosingBraceLineWithBlankLines(SourceText sourceText, BlockSyntax block, TextLine closeLine)
	{
		TextSpan span = GetLineWithBreak(sourceText, closeLine.LineNumber);
		SyntaxToken next = block.CloseBraceToken.GetNextToken();

		bool continuesStatement =
			next.IsKind(SyntaxKind.ElseKeyword)
			|| (next.IsKind(SyntaxKind.WhileKeyword) && next.Parent is DoStatementSyntax doStatement && doStatement.Statement == block);

		if (!continuesStatement || StanzaSyntax.HasDirectiveOrDisabledText(next.LeadingTrivia))
			return span;

		// A comment in the gap stays where it is, and the blank lines above it go, as the clause rule that removes them does.
		int lineIndex = closeLine.LineNumber + 1;

		while (StanzaText.IsWhitespaceOnly(sourceText, sourceText.Lines[lineIndex].Span))
			lineIndex++;

		return TextSpan.FromBounds(span.Start, sourceText.Lines[lineIndex].Start);
	}

	private static bool IsLineAlone(SourceText sourceText, TextLine line, char expected)
	{
		int position = StanzaText.SkipWhitespace(sourceText, line.Start, line.End);
		bool opensWithExpected = position < line.End && sourceText[position] == expected;

		return opensWithExpected && StanzaText.SkipWhitespace(sourceText, position + 1, line.End) == line.End;
	}

	private static bool IsIndentedDeeper(SourceText sourceText, TextLine inner, TextLine outer, int tabColumns) =>
		StanzaText.GetIndentationColumns(sourceText, inner, tabColumns) > StanzaText.GetIndentationColumns(sourceText, outer, tabColumns);

	private static TextSpan GetLineWithBreak(SourceText sourceText, int lineIndex)
	{
		TextLine line = sourceText.Lines[lineIndex];

		if (line.EndIncludingLineBreak > line.End)
			return TextSpan.FromBounds(line.Start, line.EndIncludingLineBreak);

		// The last line has no break of its own, so the break before it is taken instead;
		// the first line has none before it either, so it is deleted as it is.
		return lineIndex == 0 ? line.Span : TextSpan.FromBounds(sourceText.Lines[lineIndex - 1].End, line.End);
	}

	public static TextChange? CreateEmptyBlockCollapse(SourceText sourceText, SyntaxToken openBrace, SyntaxToken closeBrace)
	{
		if (!openBrace.IsKind(SyntaxKind.OpenBraceToken) || !closeBrace.IsKind(SyntaxKind.CloseBraceToken))
			return null;

		var gap = TextSpan.FromBounds(openBrace.Span.End, closeBrace.SpanStart);
		return StanzaText.IsWhitespaceOnly(sourceText, gap) && StanzaSyntax.IsMultiLine(sourceText, gap) ? new TextChange(gap, " ") : null;
	}

	/// <summary>Collects the units a collapse can leave on one line, from the innermost out.</summary>
	/// <remarks>
	/// A brace on its own line joins only a unit it opens, and the walk ends at a statement, a member or a section.
	/// </remarks>
	public static ImmutableArray<SyntaxNode> CollectJoinedUnits(SourceText sourceText, SyntaxToken openBrace)
	{
		bool opensItsLine = StanzaText.StartsItsLine(sourceText, openBrace.SpanStart);

		ImmutableArray<SyntaxNode>.Builder units = ImmutableArray.CreateBuilder<SyntaxNode>();

		for (SyntaxNode? node = openBrace.Parent; node is not null && units.Count < MaxJoinedUnits; node = node.Parent)
		{
			if (opensItsLine && node.GetFirstToken() != openBrace)
				break;

			if (IsListUnit(node) || IsSiblingUnit(node))
				units.Add(node);

			if (node is SwitchSectionSyntax || (IsSiblingUnit(node) && node.Parent is not SwitchSectionSyntax))
				break;
		}

		return units.ToImmutable();
	}

	/// <summary>Reads which of the joined units a multi-line rule separates, one bit for each unit.</summary>
	public static int ReadSeparatedUnits(SyntaxNodeAnalysisContext context, StanzaRule rule, ImmutableArray<SyntaxNode> units)
	{
		int separated = 0;

		for (int index = 0; index < units.Length; index++)
		{
			if (IsSeparatedByItsList(context, rule, unit: units[index]))
				separated |= 1 << index;
		}

		return separated;
	}

	/// <summary>Whether the multi-line rule of the unit's container reports the gaps beside the unit.</summary>
	private static bool IsSeparatedByItsList(SyntaxNodeAnalysisContext context, StanzaRule rule, SyntaxNode unit)
	{
		if (StanzaSyntax.FindContainer(unit) is not { } container)
			return false;

		SyntaxKind containerKind = container.Kind();
		var options = StanzaOptions.From(context.Options, unit.SyntaxTree);

		foreach (DiagnosticDescriptor target in rule.DefersTo)
		{
			StanzaRule separating = rule.GetDeferral(target);

			if (separating.Kinds.Contains(containerKind))
				return SiblingGaps.IsSeparatingNeighbors(scope: context, options, separating, unit);
		}

		return false;
	}

	/// <summary>Creates the join, and the blank lines beside the units it can leave on one line.</summary>
	/// <remarks>
	/// The multi-line rules would write them, so the fix order writes one text.
	/// </remarks>
	public static ImmutableArray<TextChange> CreateSeparatedCollapse(
		SourceText sourceText,
		TextChange join,
		SyntaxToken openBrace,
		int separatedUnits,
		int indentStepColumns
	)
	{
		ImmutableArray<SyntaxNode> units = CollectJoinedUnits(sourceText, openBrace);

		IEnumerable<SyntaxNode> separated = units.Where((_, index) => (separatedUnits & (1 << index)) != 0);

		return SiblingGaps.WithSeparations(sourceText, units: separated, changes: [join], indentStepColumns);
	}

	private static bool IsListUnit(SyntaxNode node)
	{
		return node
			is AccessorDeclarationSyntax
				or SwitchSectionSyntax
				or SwitchExpressionArmSyntax
				or ArgumentSyntax
				or AttributeArgumentSyntax
				or VariableDeclaratorSyntax
				or AnonymousObjectMemberDeclaratorSyntax
				or CollectionElementSyntax
				or ExpressionSyntax { Parent: InitializerExpressionSyntax };
	}

	private static bool IsSiblingUnit(SyntaxNode node)
	{
		return node
			is StatementSyntax { Parent: BlockSyntax or SwitchSectionSyntax or GlobalStatementSyntax }
				or (MemberDeclarationSyntax and not GlobalStatementSyntax);
	}
}
