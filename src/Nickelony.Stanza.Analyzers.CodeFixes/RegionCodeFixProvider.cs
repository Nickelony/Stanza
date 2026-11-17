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
			DiagnosticDescriptors.SeparateRegionMarker,
			"Add the blank line",

			FromCarriedChange(
				static (document, cancellationToken) =>
					RegionEdits.CreateMarkerGapEdits(
						syntaxTree: document.Root.SyntaxTree,
						sourceText: document.Text,
						options: ReadOptions(document),
						cancellationToken
					)
			)
		),

		FixRow.For(
			DiagnosticDescriptors.SeparateConditionalBlock,
			"Add the blank line",

			FromCarriedChange(
				static (document, cancellationToken) =>
					RegionEdits.CreateConditionalBlockGapEdits(
						syntaxTree: document.Root.SyntaxTree,
						sourceText: document.Text,
						options: ReadOptions(document),
						cancellationToken
					)
			)
		),

		FixRow.For(
			DiagnosticDescriptors.SeparateSuppressionBlock,
			"Add the blank line",

			FromCarriedChange(
				static (document, cancellationToken) =>
					RegionEdits.CreateSuppressionBlockGapEdits(
						syntaxTree: document.Root.SyntaxTree,
						sourceText: document.Text,
						options: ReadOptions(document),
						cancellationToken
					)
			)
		),

		FixRow.For(
			DiagnosticDescriptors.RemoveDirectiveBlockPadding,
			"Remove the blank line",

			FromCarriedChange(
				static (document, cancellationToken) =>
					RegionEdits.CreateBlockPaddingEdits(
						syntaxTree: document.Root.SyntaxTree,
						sourceText: document.Text,
						options: ReadOptions(document),
						cancellationToken
					)
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

	/// <summary>Reads the options the project sets for the file, for a diagnostic that carries no change.</summary>
	private static StanzaOptions ReadOptions(FixDocument document) =>
		StanzaOptions.From(document.Document.Project.AnalyzerOptions, document.Root.SyntaxTree);
}
