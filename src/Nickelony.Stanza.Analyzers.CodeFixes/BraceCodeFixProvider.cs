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
			FromSync(static (document, diagnostic) => CreateCollapse(document.Root, document.Text, diagnostic))
		),

		FixRow.For(
			DiagnosticDescriptors.UseBracesConsistently,
			"Add the braces",
			FromSync(static (document, diagnostic) => CreateInsertions(document.Root, document.Text, diagnostic))
		),

		FixRow.For(
			DiagnosticDescriptors.RemoveRedundantBraces,
			"Remove the braces",
			FromSync(static (document, diagnostic) => CreateRemovals(document.Root, document.Text, diagnostic))
		),

		FixRow.For(
			DiagnosticDescriptors.RemoveElseAfterJump,
			"Remove the else",
			FromSync(static (document, diagnostic) => CreateElseRemoval(document.Root, document.Text, diagnostic))
		),

		FixRow.For(
			DiagnosticDescriptors.MergeNestedIfStatements,
			"Merge the if statements",
			FromSync(static (document, diagnostic) => CreateIfMerge(document.Root, document.Text, diagnostic))
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
		return
			root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: false) is BlockSyntax block
			&& BraceTargets.FindTarget(block) is { } target
			? BraceEdits.CreateSafeBraceRemoval(sourceText, target, FixOptions.Parse(diagnostic).TabColumns)
			: [];
	}

	private static ImmutableArray<TextChange> CreateElseRemoval(SyntaxNode root, SourceText sourceText, Diagnostic diagnostic)
	{
		var options = FixOptions.Parse(diagnostic);

		return root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is ElseClauseSyntax { Parent: IfStatementSyntax ifStatement }
			? ElseAfterJump.CreateRemoval(
				sourceText,
				ifStatement,
				separatesNeighbors: options.SeparatesNeighbors,
				indentStepColumns: options.IndentStepColumns
			)
			: [];
	}

	private static ImmutableArray<TextChange> CreateIfMerge(SyntaxNode root, SourceText sourceText, Diagnostic diagnostic)
	{
		var options = FixOptions.Parse(diagnostic);

		return root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is IfStatementSyntax outer && options.MergedIfs is { } count
			? NestedIfs.CreateMerge(
				sourceText,
				outer,
				count,
				separatesNeighbors: options.SeparatesNeighbors,
				indentStepColumns: options.IndentStepColumns
			)
			: [];
	}

	private static ImmutableArray<TextChange> CreateCollapse(SyntaxNode root, SourceText sourceText, Diagnostic diagnostic)
	{
		if (
			StanzaBraces.FindBracePair(root, diagnostic) is not { } braces
			|| BraceEdits.CreateEmptyBlockCollapse(sourceText, braces.Previous, braces.Next) is not { } join
		)
		{
			return [];
		}

		var options = FixOptions.Parse(diagnostic);

		return BraceEdits.CreateSeparatedCollapse(
			sourceText,
			join,
			openBrace: braces.Previous,
			separatedUnits: options.SeparatedUnits,
			indentStepColumns: options.IndentStepColumns
		);
	}
}
