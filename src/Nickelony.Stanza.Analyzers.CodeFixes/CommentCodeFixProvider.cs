using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Analyzers.Comments;
using Nickelony.Stanza.Analyzers.Documentation;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CommentCodeFixProvider)), Shared]
internal sealed class CommentCodeFixProvider : StanzaCodeFixProvider
{
	private const string MarkerSpace = "Separate the comment marker from its text";

	private static readonly FixTable s_fixes = new([
		FixRow.For(DiagnosticDescriptors.LayOutDocumentationComment, "Lay out the documentation comment", FromSync(CreateCommentLayout)),
		FixRow.For(DiagnosticDescriptors.MarkDocumentationParagraphs, "Mark the paragraph break with <para>", FromSync(CreateParagraphLayout)),
		FixRow.For(DiagnosticDescriptors.RemoveDocumentationPadding, "Remove the blank line in the comment", FromSync(CreatePaddingRemoval)),
		FixRow.For(DiagnosticDescriptors.MarkNullKeyword, "Write the keyword as a <see langword> element", FromSync(CreateKeyword)),
		FixRow.For(DiagnosticDescriptors.SeparateSlashOfSelfClosingTag, "Use one space before the slash", FromSync(CreateSlashGap)),
		FixRow.For(DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight, "Remove the space before the slash", FromSync(CreateSlashGap)),
		FixRow.For(DiagnosticDescriptors.SeparateSingleLineCommentMarkerFromText, MarkerSpace, FromSync(CreateSingleLineMarkerSpace)),
		FixRow.For(DiagnosticDescriptors.EndDocumentationTextWithPeriod, "Add the period", FromSync(CreatePeriod)),
		FixRow.For(DiagnosticDescriptors.CollapseWhitespaceInsideTag, "Use one space inside the tag", FromSync(CreateTagSpace)),

		FixRow.For(
			DiagnosticDescriptors.OrderElementsOfDocumentationComment,
			"Order the elements of the documentation comment",
			FromSync(CreateOrder)
		),

		FixRow.For(DiagnosticDescriptors.LayOutItemsOfDocumentationList, "Lay out the items of the documentation list", FromSync(CreateListLayout)),
		FixRow.For(DiagnosticDescriptors.OrderItemsOfDocumentationList, "Order the items of the documentation list", FromSync(CreateListOrder)),

		FixRow.For(
			DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag,
			"Write the empty element as a self-closing tag",
			FromSync(CreateEmptyElement)
		),

		FixRow.For(DiagnosticDescriptors.SeparateDelimitedCommentMarkerFromText, MarkerSpace, FromSync(CreateMarkerSpace)),
		FixRow.For(DiagnosticDescriptors.SeparateMarkupCommentMarkerFromText, MarkerSpace, FromSync(CreateMarkupMarkerSpace)),

		FixRow.For(
			DiagnosticDescriptors.SeparateTrailingCommentFromCode,
			"Separate the trailing comment from the code",
			FromSync(CreateTrailingSpace)
		),
	]);

	protected override FixTable Fixes => s_fixes;

	private static ImmutableArray<TextChange> CreateLayout(FixDocument document, Diagnostic diagnostic, bool markParagraphs)
	{
		if (FixOptions.Parse(diagnostic).DocLayout is not { } layout)
			return [];

		SyntaxTrivia trivia = CommentEdits.FindCommentTrivia(document.Root, GetCommentPosition(diagnostic));

		if (trivia.GetStructure() is not DocumentationCommentTriviaSyntax comment)
			return [];

		return DocCommentEdits.CreateLayout(sourceText: document.Text, trivia, comment, layout, markParagraphs) is { } change ? [change] : [];
	}

	private static ImmutableArray<TextChange> CreatePaddingRemoval(
		FixDocument document,
		Diagnostic diagnostic,
		CancellationToken cancellationToken
	) => CreateAtAnchor(document, diagnostic, create: DocCommentEdits.CreatePaddingEdits);

	private static ImmutableArray<TextChange> CreateKeyword(FixDocument document, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		bool spaced = FixOptions.Parse(diagnostic).SpacedSlash;
		return CreateAtAnchor(document, diagnostic, create: comment => DocCommentEdits.CreateNullKeywordEdits(comment, spaced));
	}

	/// <summary>Offers the change of the edit the diagnostic reports at, with the text the engine wrote.</summary>
	private static ImmutableArray<TextChange> CreateAtAnchor(
		FixDocument document,
		Diagnostic diagnostic,
		Func<DocumentationCommentTriviaSyntax, List<DocCommentEdit>> create
	)
	{
		SyntaxTrivia trivia = CommentEdits.FindCommentTrivia(document.Root, GetCommentPosition(diagnostic));

		return trivia.GetStructure() is not DocumentationCommentTriviaSyntax comment
			? []
			: StanzaFixes.FindChangeAt(create(comment), diagnostic.Location.SourceSpan);
	}

	private static ImmutableArray<TextChange> CreateSingleLineMarkerSpace(
		FixDocument document,
		Diagnostic diagnostic,
		CancellationToken cancellationToken
	)
	{
		SyntaxTrivia trivia = CommentEdits.FindCommentTrivia(document.Root, GetCommentPosition(diagnostic), documentationOnly: false);
		return CommentEdits.CreateSingleLineMarkerSpace(trivia, diagnostic.Location.SourceSpan) is { } change ? [change] : [];
	}

	private static ImmutableArray<TextChange> CreateTrailingSpace(FixDocument document, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		SyntaxTrivia trivia = CommentEdits.FindCommentTrivia(document.Root, GetCommentPosition(diagnostic), documentationOnly: false);
		return CommentEdits.CreateTrailingSpace(trivia, diagnostic.Location.SourceSpan.Start) is { } change ? [change] : [];
	}

	private static ImmutableArray<TextChange> CreateMarkerSpace(FixDocument document, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		SyntaxTrivia trivia = CommentEdits.FindCommentTrivia(document.Root, GetCommentPosition(diagnostic), documentationOnly: false);
		return CommentEdits.CreateDelimitedMarkerSpace(trivia, diagnostic.Location.SourceSpan) is { } change ? [change] : [];
	}

	private static ImmutableArray<TextChange> CreateMarkupMarkerSpace(
		FixDocument document,
		Diagnostic diagnostic,
		CancellationToken cancellationToken
	) => MarkupLiteralEdits.CreateCommentMarkerSpace(document.Text, diagnostic.Location.SourceSpan) is { } change ? [change] : [];

	private static ImmutableArray<TextChange> CreateTagSpace(FixDocument document, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		SyntaxTrivia trivia = CommentEdits.FindCommentTrivia(document.Root, GetCommentPosition(diagnostic));

		return
			trivia.GetStructure() is DocumentationCommentTriviaSyntax comment
			&& DocCommentEdits.CreateTagSpace(comment, sourceText: document.Text, position: diagnostic.Location.SourceSpan.Start, cancellationToken)
				is { } change
			? [change]
			: [];
	}

	private static ImmutableArray<TextChange> CreateOrder(FixDocument document, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		SyntaxTrivia trivia = CommentEdits.FindCommentTrivia(document.Root, GetCommentPosition(diagnostic));

		if (
			trivia.GetStructure() is not DocumentationCommentTriviaSyntax comment
			|| DocCommentOrder.CreateElementOrderEdits(comment, document.Text, DocCommentSyntax.FindDocumentedDeclaration(trivia))
				is not { Count: > 0 } edits
		)
		{
			return [];
		}

		return ToChanges(edits);
	}

	private static ImmutableArray<TextChange> CreateListLayout(FixDocument document, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		if (FixOptions.Parse(diagnostic).DocLayout is not { } layout)
			return [];

		SyntaxTrivia trivia = CommentEdits.FindCommentTrivia(document.Root, GetCommentPosition(diagnostic));

		if (
			trivia.GetStructure() is not DocumentationCommentTriviaSyntax comment
			|| DocCommentLists.CreateListLayoutEdits(trivia, comment, sourceText: document.Text, layout, cancellationToken)
				is not { Count: > 0 } edits
		)
		{
			return [];
		}

		return StanzaFixes.FindChangeAt(edits, diagnostic.Location.SourceSpan);
	}

	private static ImmutableArray<TextChange> CreateListOrder(FixDocument document, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		SyntaxTrivia trivia = CommentEdits.FindCommentTrivia(document.Root, GetCommentPosition(diagnostic));

		if (
			trivia.GetStructure() is not DocumentationCommentTriviaSyntax comment
			|| DocCommentLists.CreateListItemOrderEdits(trivia, comment, sourceText: document.Text, cancellationToken) is not { Count: > 0 } edits
		)
		{
			return [];
		}

		return StanzaFixes.FindChangeAt(edits, diagnostic.Location.SourceSpan);
	}

	private static ImmutableArray<TextChange> CreateEmptyElement(FixDocument document, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		bool spaced = FixOptions.Parse(diagnostic).SpacedSlash;

		int position = diagnostic.Location.SourceSpan.Start;
		SyntaxTrivia trivia = CommentEdits.FindCommentTrivia(document.Root, GetCommentPosition(diagnostic));

		return trivia.GetStructure() is not DocumentationCommentTriviaSyntax comment
			? []
			: StanzaFixes.FindChangeAt(DocCommentEdits.CreateEmptyElement(comment, spaced, cancellationToken), position);
	}

	private static ImmutableArray<TextChange> ToChanges(List<DocCommentEdit> edits) => [.. edits.Select(static edit => edit.Change)];

	private static int GetCommentPosition(Diagnostic diagnostic) =>
		diagnostic.AdditionalLocations.Count > 0 ? diagnostic.AdditionalLocations[0].SourceSpan.Start : diagnostic.Location.SourceSpan.Start;

	private static ImmutableArray<TextChange> CreateCommentLayout(FixDocument document, Diagnostic diagnostic, CancellationToken cancellationToken) =>
		CreateLayout(document, diagnostic, markParagraphs: false);

	private static ImmutableArray<TextChange> CreateParagraphLayout(
		FixDocument document,
		Diagnostic diagnostic,
		CancellationToken cancellationToken
	) => CreateLayout(document, diagnostic, markParagraphs: true);

	private static ImmutableArray<TextChange> CreatePeriod(FixDocument document, Diagnostic diagnostic, CancellationToken cancellationToken) =>
		CreateAtAnchor(document, diagnostic, create: comment => DocCommentEdits.CreateMissingPeriodEdits(comment, document.Text));

	private static ImmutableArray<TextChange> CreateSlashGap(FixDocument document, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		bool spaced = diagnostic.Id == DiagnosticDescriptors.SeparateSlashOfSelfClosingTag.Id;
		return CreateAtAnchor(document, diagnostic, create: comment => DocCommentEdits.CreateSlashGapEdits(comment, spaced));
	}
}
