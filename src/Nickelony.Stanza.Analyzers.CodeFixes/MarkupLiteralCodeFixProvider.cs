using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Analyzers.MarkupLiterals;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MarkupLiteralCodeFixProvider)), Shared]
internal sealed class MarkupLiteralCodeFixProvider : StanzaCodeFixProvider
{
	private static readonly FixTable s_fixes = new([
		FixRow.For(
			DiagnosticDescriptors.SeparateMarkupCommentMarkerFromText,
			"Separate the comment marker from its text",
			FromSync(CreateMarkerSpace)
		),

		FixRow.For(DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement, "Use one space before the slash", FromSlashGap(spaced: true)),
		FixRow.For(DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight, "Remove the space before the slash", FromSlashGap(spaced: false)),

		FixRow.For(
			DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag,
			"Write the empty element as a self-closing tag",
			FromSync(CreateEmptyElement)
		),
	]);

	protected override FixTable Fixes => s_fixes;

	private static ImmutableArray<TextChange> CreateMarkerSpace(FixDocument document, Diagnostic diagnostic) =>
		MarkupLiteralEdits.CreateCommentMarkerSpace(document.Text, diagnostic.Location.SourceSpan) is { } change ? [change] : [];

	/// <summary>Fixes the slash gap to the shape the rule of the row keeps, so no diagnostic carries it.</summary>
	private static FixChanges FromSlashGap(bool spaced)
	{
		return FromSync(
			(document, diagnostic) =>
				MarkupLiteralEdits.CreateSlashGap(document.Text, diagnostic.Location.SourceSpan, spaced) is { } change ? [change] : []
		);
	}

	private static ImmutableArray<TextChange> CreateEmptyElement(FixDocument document, Diagnostic diagnostic)
	{
		bool spaced = FixOptions.Parse(diagnostic).SpacedSlash;
		return MarkupLiteralEdits.CreateEmptyElement(document.Text, diagnostic.Location.SourceSpan, spaced) is { } change ? [change] : [];
	}
}
