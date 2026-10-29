using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Analyzers.Documentation;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BlankLineCodeFixProvider)), Shared]
internal sealed class BlankLineCodeFixProvider : StanzaCodeFixProvider
{
	private static readonly FixTable s_fixes = new([
		.. StanzaRules
			.All.Where(static rule => rule.Fix != GapFix.None)
			.Select(static rule => FixRow.For(rule.Descriptor, GetTitle(rule.Fix), FromSync(GetChangeBuilder(rule.Fix)))),
	]);

	protected override FixTable Fixes => s_fixes;

	private static string GetTitle(GapFix fix)
	{
		return fix switch
		{
			GapFix.MarkupExtraRemoval => "Remove the extra blank lines",
			GapFix.AdjacentRemoval or GapFix.DeclarationGapRemoval or GapFix.MarkupCapRemoval => "Remove the blank line",
			GapFix.RunRemoval => "Remove the blank lines of the run",
			GapFix.SiblingInsertion or GapFix.MarkupInsertion or GapFix.TokenInsertion => "Add the blank line",
			_ => throw new ArgumentOutOfRangeException(nameof(fix), fix, message: null),
		};
	}

	/// <summary>
	/// Chooses the change builder once, when the table is built, because the registry names one fix per rule.
	/// </summary>
	private static Func<FixDocument, Diagnostic, CancellationToken, ImmutableArray<TextChange>> GetChangeBuilder(GapFix fix)
	{
		return fix switch
		{
			GapFix.SiblingInsertion => static (document, diagnostic, _) => CreateSiblingInsertion(document.Root, document.Text, diagnostic),
			GapFix.MarkupInsertion => static (document, diagnostic, _) => CreateMarkupInsertion(document.Text, diagnostic),
			GapFix.TokenInsertion => static (document, diagnostic, _) => CreateTokenInsertion(document.Root, document.Text, diagnostic),

			GapFix.AdjacentRemoval => static (document, diagnostic, _) =>
				CreateTokenRemoval(document.Root, document.Text, diagnostic) is { } removal ? [removal] : [],

			GapFix.RunRemoval => static (document, diagnostic, _) => CreateRunRemoval(document.Root, document.Text, diagnostic),

			GapFix.DeclarationGapRemoval => static (document, diagnostic, cancellationToken) =>
				CreateDeclarationGapRemoval(root: document.Root, sourceText: document.Text, diagnostic, cancellationToken),

			// A tight rule removes every blank line of its gap, so a diagnostic with no count still has one to remove;
			// the rule that cuts a gap back to a cap has no count to fall back on.
			GapFix.MarkupCapRemoval => static (document, diagnostic, _) => CreateMarkupCapRemoval(document.Text, diagnostic, keptWithoutProperty: 0),

			GapFix.MarkupExtraRemoval => static (document, diagnostic, _) =>
				CreateMarkupCapRemoval(document.Text, diagnostic, keptWithoutProperty: null),

			_ => throw new ArgumentOutOfRangeException(nameof(fix), fix, message: null),
		};
	}

	private static ImmutableArray<TextChange> CreateTokenInsertion(SyntaxNode root, SourceText sourceText, Diagnostic diagnostic)
	{
		return
			StanzaDiagnosticLookup.FindTokenPair(root, diagnostic) is { } tokenPair
			&& BlankLineGaps.CreateTokenInsertion(sourceText, tokenPair.Previous, tokenPair.Next) is { } insertion
			? [insertion]
			: [];
	}

	private static ImmutableArray<TextChange> CreateMarkupInsertion(SourceText sourceText, Diagnostic diagnostic)
	{
		int position = diagnostic.Location.SourceSpan.Start;

		TextLine line = sourceText.Lines.GetLineFromPosition(position);

		// A blank line in front of a blank line would add a second one, so none is offered there.
		return StanzaText.IsBlankLine(sourceText, line.LineNumber) ? [] : [StanzaText.CreateBlankLineAt(sourceText, line.LineNumber)];
	}

	private static ImmutableArray<TextChange> CreateSiblingInsertion(SyntaxNode root, SourceText sourceText, Diagnostic diagnostic)
	{
		return
			StanzaDiagnosticLookup.FindNeighborUnits(root, diagnostic) is { } neighbors
			&& BlankLineGaps.CreateSiblingInsertion(
				sourceText,
				previous: neighbors.Previous,
				next: neighbors.Next,
				indentStepColumns: FixOptions.Parse(diagnostic).IndentStepColumns
			)
				is { } insertion
			? [insertion]
			: [];
	}

	private static ImmutableArray<TextChange> CreateMarkupCapRemoval(SourceText sourceText, Diagnostic diagnostic, int? keptWithoutProperty)
	{
		// A count that is there and is not a number offers no fix, and a diagnostic with no count takes the rule's own.
		int? written = FixOptions.Parse(diagnostic).KeptBlankLines;

		if ((written ?? (FixOptions.CarriesKeptBlankLines(diagnostic) ? null : keptWithoutProperty)) is not { } keptBlankLines)
			return [];

		return MarkupLiteralEdits.CreateCapRemoval(sourceText, diagnostic.Location.SourceSpan.Start, keptBlankLines) is { } removal ? [removal] : [];
	}

	private static TextChange? CreateTokenRemoval(SyntaxNode root, SourceText sourceText, Diagnostic diagnostic)
	{
		if (StanzaDiagnosticLookup.FindTokenPair(root, diagnostic) is { } tokenPair)
			return BlankLineGaps.CreateAdjacentRemoval(sourceText, previous: tokenPair.Previous, next: tokenPair.Next, ruleId: diagnostic.Id);

		return diagnostic.AdditionalLocations.Count == 0
			? BlankLineGaps.CreateAnchorRemoval(sourceText, root.FindToken(diagnostic.Location.SourceSpan.Start), diagnostic.Id)
			: null;
	}

	private static ImmutableArray<TextChange> CreateRunRemoval(SyntaxNode root, SourceText sourceText, Diagnostic diagnostic)
	{
		return
			FixOptions.Parse(diagnostic) is { RunStart: { } runStart, RunEnd: { } runEnd }
			&& StanzaDiagnosticLookup.FindNeighborUnits(root, diagnostic) is { Next.Parent: { } container }
			? BlankLineLooseRuns.CreateRemoval(sourceText, siblings: StanzaSyntax.ReadSiblings(container), runStart, runEnd, ruleId: diagnostic.Id)
			: [];
	}

	private static ImmutableArray<TextChange> CreateDeclarationGapRemoval(
		SyntaxNode root,
		SourceText sourceText,
		Diagnostic diagnostic,
		CancellationToken cancellationToken
	)
	{
		SyntaxToken token = root.FindToken(diagnostic.Location.SourceSpan.Start);
		return DocCommentEdits.CreateDeclarationGapRemoval(token, sourceText, cancellationToken) is { } removal ? [removal] : [];
	}
}
