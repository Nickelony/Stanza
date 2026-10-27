using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Analyzers.Bodies;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BodyCodeFixProvider)), Shared]
internal sealed class BodyCodeFixProvider : StanzaCodeFixProvider
{
	private const string BlockBody = "Use the block body";
	private const string ExpressionBody = "Use the expression body";

	private static readonly FixChanges s_blockBody = CreateBlockBodyAsync;

	private static readonly FixChanges s_expressionBody = FromSync(
		static (document, diagnostic, _) => CreateExpressionBody(document.Root, document.Text, diagnostic)
	);

	/// <summary>Each arrow rule knows its placement, so a bare diagnostic still moves the arrow.</summary>
	private static readonly FixChanges s_trailingArrow = FromSync(
		static (document, diagnostic, _) =>
			CreateArrowPlacement(root: document.Root, sourceText: document.Text, diagnostic, placement: ArrowPlacement.Trailing)
	);

	private static readonly FixChanges s_leadingArrow = FromSync(
		static (document, diagnostic, _) =>
			CreateArrowPlacement(root: document.Root, sourceText: document.Text, diagnostic, placement: ArrowPlacement.Leading)
	);

	private static readonly FixTable s_fixes = new([
		FixRow.For(DiagnosticDescriptors.UseBlockBodyForMultiLineMember, BlockBody, s_blockBody),
		FixRow.For(DiagnosticDescriptors.UseExpressionBodyForSingleLineMember, ExpressionBody, s_expressionBody),
		FixRow.For(DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember, BlockBody, s_blockBody),
		FixRow.For(DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember, ExpressionBody, s_expressionBody),

		FixRow.For(
			DiagnosticDescriptors.BreakExpressionOffSignatureLine,
			"Move the expression off the signature line",
			FromSync(static (document, diagnostic, _) => CreateExpressionBreak(document.Root, document.Text, diagnostic))
		),

		FixRow.For(DiagnosticDescriptors.KeepArrowOnSignatureLine, "Move the arrow onto the signature line", s_trailingArrow),
		FixRow.For(DiagnosticDescriptors.PutArrowOnLineAfterSignature, "Move the arrow onto the line after the signature", s_leadingArrow),
	]);

	protected override FixTable Fixes => s_fixes;

	private static async Task<ImmutableArray<TextChange>> CreateBlockBodyAsync(
		FixDocument document,
		Diagnostic diagnostic,
		CancellationToken cancellationToken
	)
	{
		SemanticModel? semanticModel = await document.Document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
		return CreateBlockBody(root: document.Root, sourceText: document.Text, diagnostic, semanticModel);
	}

	private static ImmutableArray<TextChange> CreateBlockBody(
		SyntaxNode root,
		SourceText sourceText,
		Diagnostic diagnostic,
		SemanticModel? semanticModel
	)
	{
		return semanticModel is not null && BodyTargets.FindExpressionTarget(root, diagnostic) is { } target
			? BodyConversion.CreateBlockBody(
				sourceText,
				target,
				returnsValue: BodyReturnValues.ReturnsValue(semanticModel, target.Owner),
				indentStepColumns: FixOptions.Parse(diagnostic).IndentStepColumns
			)
			: [];
	}

	private static ImmutableArray<TextChange> CreateExpressionBody(SyntaxNode root, SourceText sourceText, Diagnostic diagnostic)
	{
		var options = FixOptions.Parse(diagnostic);

		return BodyTargets.FindBlockTarget(root, diagnostic) is { } target
			? BodyConversion.CreateExpressionBody(
				sourceText,
				target,
				placement: options.Placement,
				openOwnLine: options.OpensOwnLine,
				indentStepColumns: options.IndentStepColumns
			)
			: [];
	}

	private static ImmutableArray<TextChange> CreateArrowPlacement(
		SyntaxNode root,
		SourceText sourceText,
		Diagnostic diagnostic,
		ArrowPlacement placement
	)
	{
		var options = FixOptions.Parse(diagnostic);

		return BodyTargets.FindExpressionTarget(root, diagnostic) is { } target
			? BodyArrows.CreateArrowPlacement(
				sourceText,
				target,
				placement,
				openOwnLine: options.OpensOwnLine,
				indentStepColumns: options.IndentStepColumns
			)
			: [];
	}

	private static ImmutableArray<TextChange> CreateExpressionBreak(SyntaxNode root, SourceText sourceText, Diagnostic diagnostic)
	{
		var options = FixOptions.Parse(diagnostic);

		return BodyTargets.FindEnclosingExpressionTarget(root, diagnostic) is { } target
			? BodyArrows.CreateExpressionBreak(sourceText, target, placement: options.Placement, indentStepColumns: options.IndentStepColumns)
			: [];
	}
}
