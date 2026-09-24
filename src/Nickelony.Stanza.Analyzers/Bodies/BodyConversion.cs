using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Bodies;

/// <summary>Converts a member body between block and expression forms, and reports a multi-line one.</summary>
internal static class BodyConversion
{
	private static bool MatchesTrigger(MultiLineTrigger trigger, bool signature, bool body)
	{
		return trigger switch
		{
			MultiLineTrigger.Body => body,
			MultiLineTrigger.Signature => signature,
			MultiLineTrigger.Both => signature && body,
			_ => signature || body,
		};
	}

	private static bool CanConvert(SourceText sourceText, ExpressionBodyTarget target, int indentStepColumns) =>
		HasConvertibleShape(sourceText, target) && HasReadableBodyIndentation(sourceText, target, indentStepColumns);

	private static bool HasConvertibleShape(SourceText sourceText, ExpressionBodyTarget target)
	{
		// The fix replaces the arrow and the gaps on both sides of it, so they may hold only whitespace, as a leading arrow's do.
		return !target.Expression.ContainsDirectives
			&& target.Expression.GetLastToken().GetNextToken().IsKind(SyntaxKind.SemicolonToken)
			&& StartsItsLine(sourceText, target)
			&& BodyArrows.CanMoveArrow(sourceText, target, ArrowPlacement.Leading);
	}

	/// <summary>
	/// Whether only the member's own attributes, if any, stand before the signature on its line, so a block written
	/// under it does not land mid-line.
	/// </summary>
	private static bool StartsItsLine(SourceText sourceText, ExpressionBodyTarget target)
	{
		TextLine line = sourceText.Lines.GetLineFromPosition(target.SignatureSpan.Start);
		int lineStart = target.SignatureSpan.Start;

		// The block lands under the line the attributes start, so they may stand before the signature,
		// but a signature such as `get` in `int Value { get => Build(` does not start its line.
		foreach (AttributeListSyntax attributes in BodyTargets.GetAttributeLists(target.Owner))
		{
			if (attributes.SpanStart >= line.Start)
			{
				lineStart = attributes.SpanStart;
				break;
			}
		}

		return StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(line.Start, lineStart));
	}

	private static bool HasReadableBodyIndentation(SourceText sourceText, ExpressionBodyTarget target, int indentStepColumns)
	{
		string memberIndentation = StanzaText.GetIndentation(sourceText, target.SignatureSpan.Start);
		return TryFindBodyIndentation(sourceText, target, memberIndentation, indentStepColumns, bodyIndentation: out _);
	}

	private static ImmutableArray<Location> GetCulpritLocations(ExpressionBodyTarget target, MultiLineTrigger trigger, bool bodyMultiLine)
	{
		var signatureLocation = Location.Create(target.Expression.SyntaxTree, target.SignatureSpan);
		Location bodyLocation = target.Expression.GetLocation();

		return trigger switch
		{
			MultiLineTrigger.Body => [bodyLocation],
			MultiLineTrigger.Signature => [signatureLocation],
			MultiLineTrigger.Both => [signatureLocation, bodyLocation],
			_ => [bodyMultiLine ? bodyLocation : signatureLocation],
		};
	}

	public static void ReportMultiLineBody(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		DiagnosticDescriptor descriptor,
		bool valueShaped
	)
	{
		if (context.Node is not ArrowExpressionClauseSyntax clause)
			return;

		if (BodyTargets.FindExpressionTarget(clause) is not { } target)
			return;

		if (target.Shape.IsValueShaped() != valueShaped)
			return;

		bool signature = BodyTargets.IsSignatureMultiLine(target);
		bool body = BodyTargets.IsBodyMultiLine(target);

		if (!MatchesTrigger(options.MultiLineTrigger, signature, body))
			return;

		SourceText sourceText = context.Node.SyntaxTree.GetText(context.CancellationToken);

		// A body that cannot convert produces no change, so no diagnostic is offered without a fix.
		if (!CanConvert(sourceText, target, options.IndentStepColumns))
			return;

		context.ReportDiagnostic(
			Diagnostic.Create(
				descriptor,
				location: target.Arrow.GetLocation(),
				additionalLocations: GetCulpritLocations(target, options.MultiLineTrigger, body),
				properties: new FixOptions(IndentStepColumns: options.IndentStepColumns).ToProperties()
			)
		);
	}

	public static ImmutableArray<TextChange> CreateBlockBody(
		SourceText sourceText,
		ExpressionBodyTarget target,
		bool returnsValue,
		int indentStepColumns
	)
	{
		if (!HasConvertibleShape(sourceText, target))
			return [];

		string memberIndentation = StanzaText.GetIndentation(sourceText, target.SignatureSpan.Start);

		if (!TryFindBodyIndentation(sourceText, target, memberIndentation, indentStepColumns, out string bodyIndentation))
			return [];

		// No ref keyword is added: a ref expression spells its own, and a second would not compile.
		// A throw expression spells its own keyword too, so the block writes no return before it.
		bool writesReturn = returnsValue && target.Expression is not ThrowExpressionSyntax;
		string keyword = writesReturn ? "return " : string.Empty;

		int lineIndex = sourceText.Lines.GetLineFromPosition(target.Expression.SpanStart).LineNumber;
		string lineEnding = StanzaText.ReadPreferredLineEnding(sourceText, lineIndex);

		SyntaxToken statementEnd = target.Expression.GetLastToken().GetNextToken();

		var arrowSpan = TextSpan.FromBounds(target.Arrow.GetPreviousToken().Span.End, target.Expression.SpanStart);

		// The closing brace goes after a trailing comment on the statement,
		// so that comment stays inside the block.
		var closingSpan = new TextSpan(StanzaBraces.GetClosingBracePosition(statementEnd), 0);

		// The accessor shape and the member shape write the same two changes;
		// only the value shape wraps the block in a get accessor.
		if (target.Shape == BodyShape.Value)
		{
			string indentStep = bodyIndentation.Substring(memberIndentation.Length);

			return
			[
				new TextChange(
					arrowSpan,

					lineEnding
						+ memberIndentation
						+ "{"
						+ lineEnding
						+ bodyIndentation
						+ "get"
						+ lineEnding
						+ bodyIndentation
						+ "{"
						+ lineEnding
						+ bodyIndentation
						+ indentStep
						+ keyword
				),

				new TextChange(closingSpan, lineEnding + bodyIndentation + "}" + lineEnding + memberIndentation + "}"),
			];
		}

		return
		[
			new TextChange(arrowSpan, lineEnding + memberIndentation + "{" + lineEnding + bodyIndentation + keyword),
			new TextChange(closingSpan, lineEnding + memberIndentation + "}"),
		];
	}

	private static bool CanCollapse(SourceText sourceText, BlockBodyTarget target) =>
		!StanzaSyntax.IsMultiLine(target.Owner.SyntaxTree, target.SignatureSpan) && IsCollapsibleStatement(sourceText, target);

	private static bool IsCollapsibleStatement(SourceText sourceText, BlockBodyTarget target)
	{
		if (StanzaSyntax.IsMultiLine(target.Statement))
			return false;

		SyntaxToken semicolon = target.Statement.GetLastToken();

		bool signatureIsClean = target.AccessorList is { } accessorList
			? IsWhitespaceBetween(sourceText, target.SignatureSpan.End, accessorList.OpenBraceToken.SpanStart)
				&& IsWhitespaceBetween(sourceText, accessorList.OpenBraceToken.Span.End, accessorList.Accessors[0].Keyword.SpanStart)
				&& IsWhitespaceBetween(sourceText, accessorList.Accessors[0].Keyword.Span.End, target.Block.OpenBraceToken.SpanStart)
			: IsWhitespaceBetween(sourceText, target.SignatureSpan.End, target.Block.OpenBraceToken.SpanStart);

		bool closeIsClean =
			target.AccessorList is null || IsWhitespaceBetween(sourceText, target.Block.CloseBraceToken.Span.End, target.CloseBrace.SpanStart);

		// Source that does not compile can place the semicolon before the expression it ends,
		// which leaves no gap to measure; the comparison tells that shape apart from a clean statement.
		return semicolon.SpanStart >= target.Expression.Span.End
			&& signatureIsClean
			&& IsWhitespaceBetween(sourceText, target.Block.OpenBraceToken.Span.End, target.Statement.SpanStart)
			&& IsWhitespaceBetween(sourceText, target.Expression.Span.End, semicolon.SpanStart)
			&& IsWhitespaceBetween(sourceText, semicolon.Span.End, target.Block.CloseBraceToken.SpanStart)
			&& closeIsClean
			&& (target.Keyword is not { } keyword || IsWhitespaceBetween(sourceText, keyword.Span.End, target.Expression.Span.Start));
	}

	private static bool IsWhitespaceBetween(SourceText sourceText, int start, int end) => StanzaText.SkipWhitespace(sourceText, start, end) == end;

	public static void ReportSingleLineBody(SyntaxNodeAnalysisContext context, StanzaOptions options, StanzaRule rule, bool valueShaped)
	{
		// A max line length below one gives these rules no width to measure against,
		// so a member whose shape only the width decides keeps the shape the file wrote.
		if (options.MaxLineLength <= 0)
			return;

		if (context.Node is not BlockSyntax block || BodyTargets.FindBlockTarget(block) is not { } target)
			return;

		if (target.Shape.IsValueShaped() != valueShaped)
			return;

		if (!StanzaSyntax.SupportsLanguage(context.Node.SyntaxTree, target.ExpressionBodyFloor))
			return;

		SourceText sourceText = context.Node.SyntaxTree.GetText(context.CancellationToken);

		if (!CanCollapse(sourceText, target))
			return;

		string bodyText = BuildStatementText(target);
		ArrowPlacement placement = BodyArrows.GetPlacement(scope: context, options, rule, position: block.SpanStart);
		bool openOwnLine = !BodyWidth.FitsOneLine(sourceText, signatureSpan: target.SignatureSpan, bodyText, options);

		if (openOwnLine && !BodyWidth.FitsTwoLines(sourceText, signatureSpan: target.SignatureSpan, bodyText, options, placement))
			return;

		// After a broken arrow, CSharpier puts a `with` initializer and each clause of a query on lines of their own,
		// so where the trigger counts the body, the block-body rule would ask for the block back.
		// CanCollapse leaves only a signature that stands on one line.
		if (
			openOwnLine
			&& target.Expression is WithExpressionSyntax or QueryExpressionSyntax
			&& MatchesTrigger(options.MultiLineTrigger, signature: false, body: true)
		)
		{
			return;
		}

		context.ReportDiagnostic(
			Diagnostic.Create(
				rule.Descriptor,
				target.Block.OpenBraceToken.GetLocation(),

				properties: new FixOptions(
					IndentStepColumns: options.IndentStepColumns,
					Placement: placement,
					OpensOwnLine: openOwnLine
				).ToProperties()
			)
		);
	}

	public static ImmutableArray<TextChange> CreateExpressionBody(
		SourceText sourceText,
		BlockBodyTarget target,
		ArrowPlacement placement,
		bool openOwnLine,
		int indentStepColumns
	)
	{
		if (!CanCollapse(sourceText, target))
			return [];

		return
		[
			new TextChange(
				TextSpan.FromBounds(target.SignatureSpan.End, target.CloseBrace.Span.End),
				BuildExpressionText(sourceText, target, placement, openOwnLine, indentStepColumns)
			),
		];
	}

	/// <summary>
	/// Writes the shape the file's placement asks for; width alone decides whether the expression moves.
	/// </summary>
	private static string BuildExpressionText(
		SourceText sourceText,
		BlockBodyTarget target,
		ArrowPlacement placement,
		bool openOwnLine,
		int indentStepColumns
	)
	{
		string statement = BuildStatementText(target);

		if (!openOwnLine)
			return BodyArrows.ArrowGap + BodyArrows.Arrow + statement;

		string lineGap = BodyArrows.GetArrowGapText(sourceText, signatureStart: target.SignatureSpan.Start, indentStepColumns);

		return placement == ArrowPlacement.Leading
			? lineGap + BodyArrows.Arrow + statement
			: BodyArrows.ArrowGap + BodyArrows.ArrowMark + lineGap + statement;
	}

	private static string BuildStatementText(BlockBodyTarget target)
	{
		return (target.Statement is ThrowStatementSyntax && target.Keyword is { } keyword ? keyword.Text + " " : string.Empty)
			+ target.Expression
			+ ";";
	}

	private static bool TryFindBodyIndentation(
		SourceText sourceText,
		ExpressionBodyTarget target,
		string memberIndentation,
		int indentStepColumns,
		out string bodyIndentation
	)
	{
		// A line the member's own indentation prefixes is deeper exactly when it is longer, so no tab width is needed.
		TextLine bodyLine = sourceText.Lines.GetLineFromPosition(target.Expression.SpanStart);

		if (bodyLine.Start >= target.SignatureSpan.End)
		{
			bodyIndentation = StanzaText.GetIndentation(sourceText, target.Expression.SpanStart);
			return bodyIndentation.Length > memberIndentation.Length && bodyIndentation.StartsWith(memberIndentation, StringComparison.Ordinal);
		}

		int firstLineIndex = sourceText.Lines.GetLineFromPosition(target.SignatureSpan.Start).LineNumber;
		int lastLineIndex = sourceText.Lines.GetLineFromPosition(target.SignatureSpan.End).LineNumber;

		for (int index = firstLineIndex; index <= lastLineIndex; index++)
		{
			string indentation = StanzaText.GetIndentation(sourceText, sourceText.Lines[index].Start);

			// Only a line one step deeper than the member is one a block can be written under.
			// A line a formatter indented another way, or aligned under an opening parenthesis, is stepped over,
			// even when the alignment lands on a whole number of steps.
			if (
				indentation.StartsWith(memberIndentation, StringComparison.Ordinal)
				&& IsOneStep(indentation.Substring(memberIndentation.Length), indentStepColumns)
			)
			{
				bodyIndentation = indentation;
				return true;
			}
		}

		// The body starts on a signature line, and no signature line the member's indentation prefixes is indented deeper,
		// so the body belongs one step in, the level a body gets anywhere else.
		bodyIndentation = StanzaText.WithOneIndentStep(sourceText, position: target.SignatureSpan.Start, memberIndentation, indentStepColumns);
		return true;
	}

	private static bool IsOneStep(string indentation, int indentStepColumns) =>
		indentation == "\t" || (indentation.Length == indentStepColumns && indentation.All(static character => character == ' '));
}
