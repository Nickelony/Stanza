using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Analyzers.Documentation;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BlankLineCodeFixProvider)), Shared]
internal sealed class BlankLineCodeFixProvider : StanzaCodeFixProvider
{
	private const string AddBlankLine = "Add the blank line";
	private const string RemoveBlankLine = "Remove the blank line";
	private const string RemoveExtraBlankLines = "Remove the extra blank lines";

	private static readonly FixTable s_fixes = new([
		.. StanzaRules.All.Where(static rule => rule.Fix != GapFix.None).Select(static rule => GetRow(rule)),
	]);

	protected override FixTable Fixes => s_fixes;

	/// <summary>Gives each gap fix its title and builder in one switch, so a fix with no arm fails.</summary>
	/// <remarks>
	/// The switch runs once, when the table is built, because the registry names one fix per rule.
	/// </remarks>
	private static FixRow GetRow(StanzaRule rule)
	{
		return rule.Fix switch
		{
			GapFix.SiblingInsertion => FixRow.For(
				descriptor: rule.Descriptor,
				title: AddBlankLine,
				create: FromSync(static (document, diagnostic) => CreateSiblingInsertion(document.Root, document.Text, diagnostic)),
				createSingle: FromSync(static (document, diagnostic) => CreateSingleSiblingInsertion(document.Root, document.Text, diagnostic))
			),

			GapFix.MarkupInsertion => FixRow.For(
				descriptor: rule.Descriptor,
				title: AddBlankLine,

				create: FromSync(
					static (document, diagnostic) =>
						MarkupLiteralEdits.CreateBlankLineInsertion(document.Text, diagnostic.Location.SourceSpan.Start) is { } insertion
							? [insertion]
							: []
				)
			),

			GapFix.TokenInsertion => FixRow.For(
				descriptor: rule.Descriptor,
				title: AddBlankLine,
				create: FromSync(static (document, diagnostic) => CreateTokenInsertion(document.Root, document.Text, diagnostic))
			),

			GapFix.AdjacentRemoval => FixRow.For(
				descriptor: rule.Descriptor,
				title: RemoveBlankLine,

				create: FromSync(
					static (document, diagnostic) => CreateTokenRemoval(document.Root, document.Text, diagnostic) is { } removal ? [removal] : []
				)
			),

			GapFix.RunRemoval => FixRow.For(
				descriptor: rule.Descriptor,
				title: "Remove the blank lines of the run",
				create: FromSync(static (document, diagnostic) => CreateRunRemoval(document.Root, document.Text, diagnostic))
			),

			GapFix.DeclarationGapRemoval => FixRow.For(
				descriptor: rule.Descriptor,
				title: RemoveBlankLine,

				create: FromSync(
					static (document, diagnostic, cancellationToken) =>
						CreateDeclarationGapRemoval(root: document.Root, sourceText: document.Text, diagnostic, cancellationToken)
				)
			),

			// A tight rule removes every blank line of its gap, so a diagnostic with no count still has one to remove;
			// the surplus removal that cuts a gap back to a cap has no count to fall back on.
			GapFix.MarkupSurplusRemoval => FixRow.For(
				descriptor: rule.Descriptor,
				title: RemoveExtraBlankLines,
				create: FromSync(static (document, diagnostic) => CreateMarkupCapRemoval(document.Text, diagnostic, keptWithoutProperty: null))
			),

			GapFix.MarkupCapRemoval => FixRow.For(
				descriptor: rule.Descriptor,
				title: RemoveBlankLine,
				create: FromSync(static (document, diagnostic) => CreateMarkupCapRemoval(document.Text, diagnostic, keptWithoutProperty: 0))
			),

			// The rule reports the span it removes.
			GapFix.TriviaRunRemoval => FixRow.For(
				descriptor: rule.Descriptor,
				title: RemoveExtraBlankLines,

				create: FromSync(
					static (document, diagnostic) =>
						BlankLineSurplus.CreateSurplusRemoval(document.Text, diagnostic.Location.SourceSpan) is { } removal ? [removal] : []
				)
			),

			_ => throw new ArgumentOutOfRangeException(nameof(rule), rule.Fix, message: null),
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

	private static ImmutableArray<TextChange> CreateSiblingInsertion(SyntaxNode root, SourceText sourceText, Diagnostic diagnostic)
	{
		return
			StanzaDiagnosticLookup.FindNeighborUnits(root, diagnostic) is { } neighbors
			&& SiblingGaps.CreateSiblingInsertion(
				sourceText,
				previous: neighbors.Previous,
				next: neighbors.Next,
				indentStepColumns: FixOptions.Parse(diagnostic).IndentStepColumns
			)
				is { } insertion
			? [insertion]
			: [];
	}

	/// <summary>The sibling insertion, with the gap above the lone unit where the diagnostic marks one.</summary>
	/// <remarks>
	/// A blank line below that unit ends the ask for the gap above it, while a Fix All has that gap's own diagnostic.
	/// </remarks>
	private static ImmutableArray<TextChange> CreateSingleSiblingInsertion(SyntaxNode root, SourceText sourceText, Diagnostic diagnostic)
	{
		ImmutableArray<TextChange> lower = CreateSiblingInsertion(root, sourceText, diagnostic);

		if (lower.IsEmpty || !FixOptions.Parse(diagnostic).WritesGapAbove)
			return lower;

		return
			StanzaDiagnosticLookup.FindNeighborUnits(root, diagnostic) is { } neighbors
			&& StanzaSyntax.FindContainer(neighbors.Next) is { } container
			&& StanzaSyntax.ReadSiblings(container) is var siblings
			&& siblings.IndexOf(neighbors.Previous is GlobalStatementSyntax globalStatement ? globalStatement.Statement : neighbors.Previous)
				is > 0
					and var index
			&& SiblingGaps.CreateSiblingInsertion(
				sourceText,
				previous: siblings[index - 1],
				next: siblings[index],
				indentStepColumns: FixOptions.Parse(diagnostic).IndentStepColumns
			)
				is { } upper
			? [upper, .. lower]
			: lower;
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
			FixOptions.Parse(diagnostic) is { RunStart: { } runStart, RunEnd: { } runEnd } fixOptions
			&& StanzaDiagnosticLookup.FindNeighborUnits(root, diagnostic) is { } neighbors
			&& StanzaSyntax.FindContainer(neighbors.Next) is { } container
			? BlankLineLooseRuns.CreateRemoval(
				sourceText,
				siblings: StanzaSyntax.ReadSiblings(container),
				runStart,
				runEnd,
				ruleId: diagnostic.Id,
				boundaries: fixOptions.Boundaries,
				indentStepColumns: fixOptions.IndentStepColumns
			)
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
