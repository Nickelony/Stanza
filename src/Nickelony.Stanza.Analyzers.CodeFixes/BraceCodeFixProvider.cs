using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Analyzers.Braces;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BraceCodeFixProvider)), Shared]
internal sealed class BraceCodeFixProvider : StanzaCodeFixProvider
{
	private static readonly FixTable s_fixes = new([
		FixRow.For(
			DiagnosticDescriptors.CollapseEmptyBlocks,
			"Collapse the braces",
			FromSync(static (document, diagnostic, _) => CreateCollapse(document.Root, document.Text, diagnostic))
		),

		FixRow.For(
			DiagnosticDescriptors.UseBracesConsistently,
			"Add the braces",
			FromSync(static (document, diagnostic, _) => CreateInsertions(document.Root, document.Text, diagnostic))
		),

		FixRow.For(
			DiagnosticDescriptors.RemoveRedundantBraces,
			"Remove the braces",
			FromSync(static (document, diagnostic, _) => CreateRemovals(document.Root, document.Text, diagnostic))
		),
	]);

	protected override FixTable Fixes => s_fixes;

	/// <summary>Two nested braceless statements close at one position, and the inner one closes first.</summary>
	protected override bool NestsInserts => true;

	private static ImmutableArray<TextChange> CreateInsertions(SyntaxNode root, SourceText sourceText, Diagnostic diagnostic)
	{
		TextSpan bodySpan = diagnostic.AdditionalLocations.Count > 0 ? diagnostic.AdditionalLocations[0].SourceSpan : diagnostic.Location.SourceSpan;

		return
			root.FindNode(bodySpan, getInnermostNodeForTie: false).FirstAncestorOrSelf<StatementSyntax>() is { } body
			&& BraceTargets.FindTarget(body) is { } target
			? BraceEdits.CreateBraceInsertions(sourceText, target.HeaderSpan, target.Body)
			: [];
	}

	private static ImmutableArray<TextChange> CreateRemovals(SyntaxNode root, SourceText sourceText, Diagnostic diagnostic)
	{
		return root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: false) is BlockSyntax block
			? BraceEdits.CreateSafeBraceRemoval(sourceText, block, FixOptions.Parse(diagnostic).TabColumns)
			: [];
	}

	private static ImmutableArray<TextChange> CreateCollapse(SyntaxNode root, SourceText sourceText, Diagnostic diagnostic)
	{
		return
			StanzaBraces.FindBracePair(root, diagnostic) is { } braces
			&& BraceEdits.CreateEmptyBlockCollapse(sourceText, braces.Previous, braces.Next) is { } join
			? [join]
			: [];
	}
}
