using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Analyzers.Comments;

namespace Nickelony.Stanza.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CommentCodeFixProvider)), Shared]
internal sealed class CommentCodeFixProvider : StanzaCodeFixProvider
{
	private const string MarkerSpace = "Separate the comment marker from its text";

	private static readonly FixTable s_fixes = new([
		FixRow.For(DiagnosticDescriptors.SeparateSingleLineCommentMarkerFromText, MarkerSpace, FromSync(CreateSingleLineMarkerSpace)),
		FixRow.For(DiagnosticDescriptors.SeparateDelimitedCommentMarkerFromText, MarkerSpace, FromSync(CreateMarkerSpace)),

		FixRow.For(
			DiagnosticDescriptors.SeparateTrailingCommentFromCode,
			"Separate the trailing comment from the code",
			FromSync(CreateTrailingSpace)
		),
	]);

	protected override FixTable Fixes => s_fixes;

	private static ImmutableArray<TextChange> CreateSingleLineMarkerSpace(FixDocument document, Diagnostic diagnostic)
	{
		SyntaxTrivia trivia = FindComment(document, diagnostic);
		return CommentEdits.CreateSingleLineMarkerSpace(trivia, diagnostic.Location.SourceSpan) is { } change ? [change] : [];
	}

	private static ImmutableArray<TextChange> CreateTrailingSpace(FixDocument document, Diagnostic diagnostic)
	{
		SyntaxTrivia trivia = FindComment(document, diagnostic);
		return CommentEdits.CreateTrailingSpace(trivia, diagnostic.Location.SourceSpan.Start) is { } change ? [change] : [];
	}

	private static ImmutableArray<TextChange> CreateMarkerSpace(FixDocument document, Diagnostic diagnostic)
	{
		SyntaxTrivia trivia = FindComment(document, diagnostic);
		return CommentEdits.CreateDelimitedMarkerSpace(trivia, diagnostic.Location.SourceSpan) is { } change ? [change] : [];
	}

	/// <summary>Finds the comment of any kind a diagnostic is about, or a default trivia.</summary>
	private static SyntaxTrivia FindComment(FixDocument document, Diagnostic diagnostic) =>
		CommentEdits.FindCommentTrivia(document.Root, StanzaFixes.GetCommentPosition(diagnostic), documentationOnly: false);
}
