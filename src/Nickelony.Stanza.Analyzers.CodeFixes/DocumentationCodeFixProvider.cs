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

namespace Nickelony.Stanza.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DocumentationCodeFixProvider)), Shared]
internal sealed class DocumentationCodeFixProvider : StanzaCodeFixProvider
{
	private static readonly FixTable s_fixes = new([
		FixRow.For(DiagnosticDescriptors.LayOutDocumentationComment, "Lay out the documentation comment", FromSync(CreateCommentLayout)),
		FixRow.For(DiagnosticDescriptors.MarkDocumentationParagraphs, "Mark the paragraph break with <para>", FromSync(CreateParagraphLayout)),
		FixRow.For(DiagnosticDescriptors.RemoveDocumentationPadding, "Remove the blank line in the comment", FromSync(CreatePaddingRemoval)),
		FixRow.For(DiagnosticDescriptors.MarkLanguageKeywords, "Write the keyword as a <see langword> element", FromSync(CreateKeyword)),
		FixRow.For(DiagnosticDescriptors.SeparateSlashOfSelfClosingTag, "Use one space before the slash", FromSlashGap(spaced: true)),
		FixRow.For(DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight, "Remove the space before the slash", FromSlashGap(spaced: false)),
		FixRow.For(DiagnosticDescriptors.CapitalizeDocumentationText, "Capitalize the first letter", FromSync(CreateCapital)),
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
	]);

	protected override FixTable Fixes => s_fixes;

	private static ImmutableArray<TextChange> CreateLayout(FixDocument document, Diagnostic diagnostic, bool markParagraphs)
	{
		if (FixOptions.Parse(diagnostic).DocLayout is not { } layout)
			return [];

		if (!TryReadDocComment(document, diagnostic, out SyntaxTrivia trivia, out DocumentationCommentTriviaSyntax comment))
			return [];

		return DocCommentEdits.CreateLayout(sourceText: document.Text, trivia, comment, layout, markParagraphs) is { } change ? [change] : [];
	}

	private static ImmutableArray<TextChange> CreatePaddingRemoval(FixDocument document, Diagnostic diagnostic) =>
		CreateAtAnchor(document, diagnostic, create: DocCommentEdits.CreatePaddingEdits);

	private static ImmutableArray<TextChange> CreateKeyword(FixDocument document, Diagnostic diagnostic)
	{
		var options = FixOptions.Parse(diagnostic);

		return CreateAtAnchor(
			document,
			diagnostic,
			create: comment => DocCommentEdits.CreateKeywordEdits(comment, options.SpacedSlash, options.Keywords)
		);
	}

	/// <summary>Offers the change of the edit the diagnostic reports at, with the text the engine wrote.</summary>
	private static ImmutableArray<TextChange> CreateAtAnchor(
		FixDocument document,
		Diagnostic diagnostic,
		Func<DocumentationCommentTriviaSyntax, List<DocCommentEdit>> create
	)
	{
		return TryReadDocComment(document, diagnostic, trivia: out _, out DocumentationCommentTriviaSyntax comment)
			? StanzaFixes.FindChangeAt(create(comment), diagnostic.Location.SourceSpan)
			: [];
	}

	private static ImmutableArray<TextChange> CreateTagSpace(FixDocument document, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		return
			TryReadDocComment(document, diagnostic, trivia: out _, out DocumentationCommentTriviaSyntax comment)
			&& DocCommentEdits.CreateTagSpace(comment, sourceText: document.Text, position: diagnostic.Location.SourceSpan.Start, cancellationToken)
				is { } change
			? [change]
			: [];
	}

	private static ImmutableArray<TextChange> CreateOrder(FixDocument document, Diagnostic diagnostic)
	{
		if (
			!TryReadDocComment(document, diagnostic, out SyntaxTrivia trivia, out DocumentationCommentTriviaSyntax comment)
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

		if (
			!TryReadDocComment(document, diagnostic, out SyntaxTrivia trivia, out DocumentationCommentTriviaSyntax comment)
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
		if (
			!TryReadDocComment(document, diagnostic, out SyntaxTrivia trivia, out DocumentationCommentTriviaSyntax comment)
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

		return TryReadDocComment(document, diagnostic, trivia: out _, out DocumentationCommentTriviaSyntax comment)
			? FindChangeAt(DocCommentEdits.CreateEmptyElement(comment, spaced, cancellationToken), position)
			: [];
	}

	/// <summary>Reads the documentation comment a diagnostic is about, and the trivia that holds it.</summary>
	private static bool TryReadDocComment(
		FixDocument document,
		Diagnostic diagnostic,
		out SyntaxTrivia trivia,
		out DocumentationCommentTriviaSyntax comment
	)
	{
		trivia = CommentEdits.FindCommentTrivia(document.Root, StanzaFixes.GetCommentPosition(diagnostic));
		comment = (trivia.GetStructure() as DocumentationCommentTriviaSyntax)!;

		return comment is not null;
	}

	/// <summary>Offers a change only when it starts where the diagnostic stands.</summary>
	private static ImmutableArray<TextChange> FindChangeAt(ImmutableArray<TextChange> changes, int position)
	{
		foreach (TextChange change in changes)
		{
			if (change.Span.Start == position)
				return [change];
		}

		return [];
	}

	private static ImmutableArray<TextChange> ToChanges(List<DocCommentEdit> edits) => [.. edits.Select(static edit => edit.Change)];

	private static ImmutableArray<TextChange> CreateCommentLayout(FixDocument document, Diagnostic diagnostic) =>
		CreateLayout(document, diagnostic, markParagraphs: false);

	private static ImmutableArray<TextChange> CreateParagraphLayout(FixDocument document, Diagnostic diagnostic) =>
		CreateLayout(document, diagnostic, markParagraphs: true);

	private static ImmutableArray<TextChange> CreateCapital(FixDocument document, Diagnostic diagnostic)
	{
		var options = FixOptions.Parse(diagnostic);
		return CreateAtAnchor(document, diagnostic, create: comment => DocCommentEdits.CreateCapitalEdits(comment, options.Keywords));
	}

	private static ImmutableArray<TextChange> CreatePeriod(FixDocument document, Diagnostic diagnostic) =>
		CreateAtAnchor(document, diagnostic, create: comment => DocCommentEdits.CreateMissingPeriodEdits(comment, document.Text));

	/// <summary>Fixes the slash gap to the shape the rule of the row keeps, so no diagnostic carries it.</summary>
	private static FixChanges FromSlashGap(bool spaced)
	{
		return FromSync(
			(document, diagnostic) => CreateAtAnchor(document, diagnostic, create: comment => DocCommentEdits.CreateSlashGapEdits(comment, spaced))
		);
	}
}
