using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Nickelony.Stanza.Analyzers.Regions;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RegionCodeFixProvider)), Shared]
internal sealed class RegionCodeFixProvider : StanzaCodeFixProvider
{
	private static readonly FixTable s_fixes = new([
		FixRow.For(
			DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors,
			"Add the blank line",

			FromCarriedChange(
				static (document, cancellationToken) => RegionEdits.CreateMarkerGapEdits(document.Root.SyntaxTree, document.Text, cancellationToken)
			)
		),

		FixRow.For(
			DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors,
			"Add the blank line",

			FromCarriedChange(
				static (document, cancellationToken) =>
					RegionEdits.CreateConditionalBlockGapEdits(document.Root.SyntaxTree, document.Text, cancellationToken)
			)
		),

		FixRow.For(
			DiagnosticDescriptors.NameClosingRegionDirective,
			"Write the region name",

			FromCarriedChange(
				static (document, cancellationToken) => RegionEdits.CreateEndRegionNameEdits(document.Root.SyntaxTree, cancellationToken)
			)
		),

		FixRow.For(
			DiagnosticDescriptors.NameClosingConditionalDirective,
			"Write the condition of the block",

			FromCarriedChange(
				// A diagnostic that carries no change was reported for a block that passed the threshold, so every block is a candidate.
				static (document, cancellationToken) =>
					RegionEdits.CreateEndIfNameEdits(syntaxTree: document.Root.SyntaxTree, sourceText: document.Text, minLines: 0, cancellationToken)
			)
		),

		FixRow.For(
			DiagnosticDescriptors.RemoveEmptyRegions,
			"Remove the empty region",

			FromCarriedChange(
				static (document, cancellationToken) => RegionEdits.CreateEmptyRegionEdits(document.Root.SyntaxTree, document.Text, cancellationToken)
			)
		),
	]);

	protected override FixTable Fixes => s_fixes;
}
