using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Bodies;

/// <summary>Places the arrow of an expression body and breaks its expression off the signature line.</summary>
internal static class BodyArrows
{
	public const string Arrow = ArrowMark + " ";
	public const string ArrowMark = "=>";
	public const string ArrowGap = " ";

	private static TextSpan? FindArrowGap(SourceText sourceText, ExpressionBodyTarget target, ArrowPlacement placement)
	{
		SyntaxToken previous = target.Arrow.GetPreviousToken();

		int start = previous.Span.End;
		int expressionStart = target.Expression.SpanStart;

		bool trailing = placement == ArrowPlacement.Trailing;

		bool clean =
			StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(start, target.Arrow.SpanStart))
			&& (trailing || StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(target.Arrow.Span.End, expressionStart)));

		int spanEnd = trailing ? target.Arrow.SpanStart : expressionStart;

		return clean ? TextSpan.FromBounds(start, spanEnd) : null;
	}

	private static bool IsArrowOnOwnLine(SourceText sourceText, ExpressionBodyTarget target)
	{
		SyntaxToken previous = target.Arrow.GetPreviousToken();
		TextLineCollection lines = sourceText.Lines;

		return lines.GetLineFromPosition(previous.Span.End).LineNumber != lines.GetLineFromPosition(target.Arrow.SpanStart).LineNumber;
	}

	/// <summary>
	/// Whether the arrow sits on the side the placement asks for, and, when leading, at or past the member's column.
	/// </summary>
	private static bool MatchesPlacement(SourceText sourceText, ExpressionBodyTarget target, ArrowPlacement placement, int tabColumns)
	{
		return IsArrowOnOwnLine(sourceText, target) == (placement == ArrowPlacement.Leading)
			&& (
				placement == ArrowPlacement.Trailing
				|| StanzaText.GetIndentationColumns(sourceText, target.Arrow.SpanStart, tabColumns)
					>= StanzaText.GetIndentationColumns(sourceText, target.SignatureSpan.Start, tabColumns)
			);
	}

	public static bool CanMoveArrow(SourceText sourceText, ExpressionBodyTarget target, ArrowPlacement placement) =>
		FindArrowGap(sourceText, target, placement) is not null;

	public static ImmutableArray<TextChange> CreateArrowPlacement(
		SourceText sourceText,
		ExpressionBodyTarget target,
		ArrowPlacement placement,
		int indentStepColumns
	)
	{
		if (FindArrowGap(sourceText, target, placement) is not { } gap)
			return [];

		string replacement =
			placement == ArrowPlacement.Leading
				? GetArrowGapText(sourceText, signatureStart: target.SignatureSpan.Start, indentStepColumns) + Arrow
				: ArrowGap;

		return [new TextChange(gap, replacement)];
	}

	public static void ReportArrowPlacement(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		DiagnosticDescriptor descriptor,
		ArrowPlacement placement
	)
	{
		if (context.Node is not ArrowExpressionClauseSyntax clause || BodyTargets.FindExpressionTarget(clause) is not { } target)
			return;

		SourceText sourceText = context.Node.SyntaxTree.GetText(context.CancellationToken);

		// A wrapped expression belongs to the body shape rules,
		// so only an arrow beside a body that stands on one line looks at the placement.
		if (BodyTargets.IsBodyMultiLine(target))
			return;

		// A leading arrow starts the expression's line, so an arrow alone on its line still has the expression to take.
		bool settled =
			MatchesPlacement(sourceText, target, placement, tabColumns: options.TabColumns)
			&& !(placement == ArrowPlacement.Leading && !IsExpressionOnArrowLine(target));

		if (IsOneLineBody(sourceText, target) || settled || !CanMoveArrow(sourceText, target, placement))
			return;

		context.ReportDiagnostic(
			Diagnostic.Create(
				descriptor,
				location: target.Arrow.GetLocation(),
				additionalLocations: [target.Arrow.GetPreviousToken().GetLocation()],
				properties: new FixOptions(IndentStepColumns: options.IndentStepColumns).ToProperties()
			)
		);
	}

	private static bool IsExpressionOnArrowLine(ExpressionBodyTarget target)
	{
		return target.Arrow.SyntaxTree is { } syntaxTree
			&& !StanzaSyntax.IsMultiLine(syntaxTree, TextSpan.FromBounds(target.Arrow.Span.End, target.Expression.SpanStart));
	}

	private static bool IsOneLineBody(SourceText sourceText, ExpressionBodyTarget target) =>
		!BodyTargets.IsMultiLine(target) && !IsArrowOnOwnLine(sourceText, target) && IsExpressionOnArrowLine(target);

	private static bool CanBreakExpression(SourceText sourceText, ExpressionBodyTarget target)
	{
		int start = target.Arrow.Span.End;
		int end = target.Expression.SpanStart;

		return IsExpressionOnArrowLine(target)
			&& !BodyTargets.IsBodyMultiLine(target)
			&& StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(start, end))
			&& target.Expression.GetLastToken().GetNextToken().IsKind(SyntaxKind.SemicolonToken);
	}

	/// <summary>
	/// Whether the expression breaks off: it has no room on the arrow's line but fits on a line of its own.
	/// </summary>
	private static bool FitsOnlyOffSignatureLine(SourceText sourceText, ExpressionBodyTarget target, StanzaOptions options, ArrowPlacement placement)
	{
		if (!CanBreakExpression(sourceText, target))
			return false;

		// A comment between the signature and the arrow stands on the arrow's line, so the signature runs through it.
		int signatureEnd = target.Arrow.SpanStart;

		while (signatureEnd > target.SignatureSpan.End && char.IsWhiteSpace(sourceText[signatureEnd - 1]))
			signatureEnd--;

		var signatureSpan = TextSpan.FromBounds(target.SignatureSpan.Start, Math.Max(signatureEnd, target.SignatureSpan.End));

		// The semicolon that ends the expression is part of the line.
		string bodyText = sourceText.ToString(target.Expression.Span) + ";";

		return !BodyWidth.FitsOneLine(sourceText, signatureSpan, bodyText, options)
			&& BodyWidth.FitsTwoLines(sourceText, signatureSpan, bodyText, options, placement);
	}

	public static void ReportExpressionBreak(SyntaxNodeAnalysisContext context, StanzaOptions options, StanzaRule rule)
	{
		if (context.Node is not ArrowExpressionClauseSyntax clause || BodyTargets.FindExpressionTarget(clause) is not { } target)
			return;

		SourceText sourceText = context.Node.SyntaxTree.GetText(context.CancellationToken);

		if (!IsOneLineBody(sourceText, target))
			return;

		ArrowPlacement placement = GetPlacement(scope: context, options, rule, position: clause.SpanStart);

		if (!FitsOnlyOffSignatureLine(sourceText, target, options, placement))
			return;

		// A gap holding something no fix may move, such as a comment before a leading arrow, produces no change,
		// so no diagnostic is offered without a fix.
		if (CreateExpressionBreak(sourceText, target, placement, indentStepColumns: options.IndentStepColumns).IsEmpty)
			return;

		context.ReportDiagnostic(
			Diagnostic.Create(
				descriptor: rule.Descriptor,
				location: target.Expression.GetLocation(),
				additionalLocations: [target.Arrow.GetLocation()],
				properties: new FixOptions(IndentStepColumns: options.IndentStepColumns, Placement: placement).ToProperties()
			)
		);
	}

	public static ImmutableArray<TextChange> CreateExpressionBreak(
		SourceText sourceText,
		ExpressionBodyTarget target,
		ArrowPlacement placement,
		int indentStepColumns
	)
	{
		if (placement == ArrowPlacement.Leading)
			return CreateArrowPlacement(sourceText, target, placement: ArrowPlacement.Leading, indentStepColumns);

		if (!CanBreakExpression(sourceText, target))
			return [];

		return
		[
			new TextChange(
				TextSpan.FromBounds(target.Arrow.Span.End, target.Expression.SpanStart),
				GetArrowGapText(sourceText, signatureStart: target.SignatureSpan.Start, indentStepColumns)
			),
		];
	}

	public static string GetArrowGapText(SourceText sourceText, int signatureStart, int indentStepColumns)
	{
		return StanzaText.ReadPreferredLineEnding(sourceText, sourceText.Lines.GetLineFromPosition(signatureStart).LineNumber)
			+ GetArrowIndentation(sourceText, signatureStart, indentStepColumns);
	}

	private static string GetArrowIndentation(SourceText sourceText, int signatureStart, int indentStepColumns)
	{
		string memberIndentation = StanzaText.GetIndentation(sourceText, signatureStart);
		return StanzaText.WithOneIndentStep(sourceText, position: signatureStart, memberIndentation, indentStepColumns);
	}

	/// <summary>
	/// Leading where <see cref="DiagnosticDescriptors.PutArrowOnLineAfterSignature"/> reports the gap, else trailing.
	/// </summary>
	public static ArrowPlacement GetPlacement(RuleScope scope, StanzaOptions options, StanzaRule rule, int position)
	{
		StanzaRule leadingRule = rule.GetDeferral(DiagnosticDescriptors.PutArrowOnLineAfterSignature);
		return StanzaRuleState.ReportsTheSharedGap(scope, options, rule: leadingRule, position) ? ArrowPlacement.Leading : ArrowPlacement.Trailing;
	}
}
