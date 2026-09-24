using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.DocComments;

namespace Nickelony.Stanza.Analyzers.Documentation;

/// <summary>Reports the documentation rules' diagnostics over the documentation comments of a tree.</summary>
internal static class DocCommentAnalysis
{
	public static void ReportCommentLayout(SyntaxTreeAnalysisContext context, DiagnosticDescriptor descriptor, bool markParagraphs)
	{
		var options = StanzaOptions.From(context.Options, context.Tree);

		if (options.DocLayout is null)
			return;

		SourceText sourceText = context.Tree.GetText(context.CancellationToken);

		ForEachDocComment(
			context,

			(trivia, comment) =>
			{
				// A comment that shares its line with code cannot be re-indented.
				// The full span is what is measured and rewritten: the first `///` sits outside the trivia's own span.
				if (DocCommentLines.FindCommentPrefix(sourceText, trivia) is not { } prefix)
					return;

				if (LayoutDeclines(sourceText, trivia))
					return;

				TextSpan? paragraphBreak = markParagraphs ? DocCommentSyntax.FindParagraphBreak(comment) : null;

				if (markParagraphs && paragraphBreak is null)
					return;

				// The marker and the indentation are not part of the width left for a content line.
				DocLayoutOptions layout = BuildDocLayout(options, sourceText.ToString(prefix), DocCommentSyntax.GetDelimiters(comment));

				// The fix makes the same comparison, so a no-op is never reported.
				if (DocCommentEdits.CreateLayout(sourceText, trivia, comment, layout, markParagraphs) is null)
					return;

				// The layout rides the diagnostic as a property, so the fix lays the comment out with the options it was judged by.
				context.ReportDiagnostic(
					Diagnostic.Create(
						descriptor,
						location: paragraphBreak is { } span ? Location.Create(context.Tree, span) : trivia.Token.GetLocation(),
						additionalLocations: [Location.Create(context.Tree, trivia.FullSpan)],
						properties: new FixOptions(DocLayout: layout).ToProperties()
					)
				);
			}
		);
	}

	public static void ReportPadding(SyntaxTreeAnalysisContext context, CompilationOptions compilationOptions, DiagnosticDescriptor descriptor)
	{
		var options = StanzaOptions.From(context.Options, context.Tree);
		var scope = new RuleScope(compilationOptions, context.Tree, context.CancellationToken);

		StanzaRule rule = StanzaRules.Get(descriptor);

		ForEachDocComment(
			context,

			(trivia, comment) =>
			{
				// The paragraph rule owns a comment that holds a break it would mark, and this rule defers to it.
				// The break is where that rule reports, so its state is read there rather than at the member.
				if (
					DocCommentSyntax.FindParagraphBreak(comment) is { } paragraphBreak
					&& !StanzaRuleState.IsOffAt(
						scope,
						options,
						rule: rule.GetDeferral(DiagnosticDescriptors.MarkDocumentationParagraphs),
						position: paragraphBreak.Start
					)
				)
				{
					return;
				}

				ReportEach(context, descriptor, trivia, spans: [.. DocCommentEdits.CreatePaddingEdits(comment).Select(static edit => edit.Anchor)]);
			}
		);
	}

	public static void ReportNullKeywords(SyntaxTreeAnalysisContext context, CompilationOptions compilationOptions, DiagnosticDescriptor descriptor)
	{
		var options = StanzaOptions.From(context.Options, context.Tree);
		var scope = new RuleScope(compilationOptions, context.Tree, context.CancellationToken);

		StanzaRule rule = StanzaRules.Get(descriptor);

		ForEachDocComment(
			context,

			(trivia, comment) =>
			{
				// The shape of the tag changes the text the keyword becomes, not where it stands.
				if (DocCommentEdits.CreateNullKeywordEdits(comment, spacedSlash: false) is not { Count: > 0 } keywords)
					return;

				// A null keyword takes the self-closing shape of whichever half owns the tag gap here;
				// a pragma over the member can hand that gap to the other half.
				bool spaced = !StanzaRuleState.OwnsTheSharedGap(
					scope,
					options,
					rule: rule.GetDeferral(DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight),
					position: trivia.FullSpan.Start
				);

				ReportEach(
					context,
					descriptor,
					trivia,
					spans: [.. keywords.Select(static edit => edit.Anchor)],
					fixOptions: new FixOptions(SpacedSlash: spaced)
				);
			}
		);
	}

	public static void ReportEmptyElements(SyntaxTreeAnalysisContext context, CompilationOptions compilationOptions, DiagnosticDescriptor descriptor)
	{
		var options = StanzaOptions.From(context.Options, context.Tree);
		var scope = new RuleScope(compilationOptions, context.Tree, context.CancellationToken);

		StanzaRule rule = StanzaRules.Get(descriptor);

		ForEachDocComment(
			context,

			(trivia, comment) =>
			{
				bool spaced = !StanzaRuleState.OwnsTheSharedGap(
					scope,
					options,
					rule: rule.GetDeferral(DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight),
					position: trivia.FullSpan.Start
				);

				ImmutableArray<TextChange> changes = DocCommentEdits.CreateEmptyElement(comment, spaced, context.CancellationToken);

				ReportEach(
					context,
					descriptor,
					trivia,
					spans: [.. changes.Select(static change => change.Span)],
					fixOptions: new FixOptions(SpacedSlash: spaced)
				);
			}
		);
	}

	public static void ReportMissingPeriods(SyntaxTreeAnalysisContext context, DiagnosticDescriptor descriptor)
	{
		SourceText sourceText = context.Tree.GetText(context.CancellationToken);

		ForEachDocComment(
			context,

			(trivia, comment) =>
				ReportEach(
					context,
					descriptor,
					trivia,
					spans: [.. DocCommentEdits.CreateMissingPeriodEdits(comment, sourceText).Select(static edit => edit.Anchor)]
				)
		);
	}

	public static void ReportSelfClosingSlashGaps(
		SyntaxTreeAnalysisContext context,
		CompilationOptions compilationOptions,
		DiagnosticDescriptor descriptor,
		bool spaced
	)
	{
		var options = StanzaOptions.From(context.Options, context.Tree);
		var scope = new RuleScope(compilationOptions, context.Tree, context.CancellationToken);

		StanzaRule rule = StanzaRules.Get(descriptor);

		ForEachDocComment(
			context,

			(trivia, comment) =>
			{
				List<TextSpan> slashes = [];

				foreach (DocCommentEdit edit in DocCommentEdits.CreateSlashGapEdits(comment, spaced))
				{
					context.CancellationToken.ThrowIfCancellationRequested();

					// A gap without a line break that is neither one space nor none changes under both halves, so ownership picks the half that reports it.
					// When the owner is off at this tag, the other half reports the gap, but only when the file enables a half of the pair.
					if (StanzaRuleState.OwnsTheSharedGap(scope, options, rule, position: edit.Change.Span.Start))
						slashes.Add(edit.Anchor);
				}

				ReportEach(context, descriptor, trivia, spans: slashes);
			}
		);
	}

	public static void ReportElementOrder(SyntaxTreeAnalysisContext context, DiagnosticDescriptor descriptor)
	{
		SourceText sourceText = context.Tree.GetText(context.CancellationToken);

		ForEachDocComment(
			context,

			(trivia, comment) =>
			{
				if (DocCommentLines.FindCommentPrefix(sourceText, trivia) is null)
					return;

				SyntaxNode? declaration = DocCommentSyntax.FindDocumentedDeclaration(trivia);

				// One diagnostic names the comment, and its fix orders every run the comment holds.
				if (DocCommentOrder.CreateElementOrderEdits(comment, sourceText, declaration) is { Count: > 0 } edits)
					ReportEach(context, descriptor, trivia, spans: [edits[0].Anchor]);
			}
		);
	}

	public static void ReportListLayout(SyntaxTreeAnalysisContext context, CompilationOptions compilationOptions, DiagnosticDescriptor descriptor)
	{
		SyntaxTree tree = context.Tree;

		var options = StanzaOptions.From(context.Options, tree);
		var scope = new RuleScope(compilationOptions, context.Tree, context.CancellationToken);

		if (options.DocLayout is null)
			return;

		StanzaRule rule = StanzaRules.Get(descriptor);
		SourceText sourceText = tree.GetText(context.CancellationToken);

		ForEachDocComment(
			context,

			(trivia, comment) =>
			{
				if (DocCommentLines.FindCommentPrefix(sourceText, trivia) is not { } prefix)
					return;

				DocLayoutOptions layout = BuildDocLayout(options, sourceText.ToString(prefix), DocCommentSyntax.GetDelimiters(comment));

				// A comment with no list has nothing to report, so the layout that would own it is never run for it.
				if (
					DocCommentLists.CreateListLayoutEdits(trivia, comment, sourceText, layout, cancellationToken: context.CancellationToken)
					is not { Count: > 0 } edits
				)
				{
					return;
				}

				// The layout rule, or the paragraph rule for a comment with a paragraph break, writes a list with its comment.
				// This rule reports a list only where that rule does not lay the comment out.
				if (!LayoutDeclines(sourceText, trivia) && CommentRulesOwnTheComment(scope, options, rule, trivia, comment, layout))
					return;

				ReportEach(
					context,
					descriptor,
					trivia,
					spans: [.. edits.Select(static edit => edit.Anchor)],
					fixOptions: new FixOptions(DocLayout: layout)
				);
			}
		);
	}

	public static void ReportListItemOrder(SyntaxTreeAnalysisContext context, DiagnosticDescriptor descriptor)
	{
		SourceText sourceText = context.Tree.GetText(context.CancellationToken);

		ForEachDocComment(
			context,

			(trivia, comment) =>
			{
				if (
					DocCommentLists.CreateListItemOrderEdits(trivia, comment, sourceText, cancellationToken: context.CancellationToken) is
					{ Count: > 0 } edits
				)
				{
					ReportEach(context, descriptor, trivia, spans: [.. edits.Select(static edit => edit.Anchor)]);
				}
			}
		);
	}

	public static void ReportTagWhitespace(SyntaxTreeAnalysisContext context, DiagnosticDescriptor descriptor)
	{
		SourceText sourceText = context.Tree.GetText(context.CancellationToken);

		ForEachDocComment(
			context,

			(trivia, comment) =>
				ReportEach(context, descriptor, trivia, spans: DocCommentEdits.FindTagWhitespaceRuns(comment, sourceText, context.CancellationToken))
		);
	}

	public static void ReportDeclarationGaps(SyntaxTreeAnalysisContext context, DiagnosticDescriptor descriptor)
	{
		SourceText sourceText = context.Tree.GetText(context.CancellationToken);

		ForEachDocComment(
			context,

			(trivia, _) =>
			{
				if (DocCommentEdits.FindDeclarationGap(trivia, sourceText, context.CancellationToken) is not null)
				{
					context.ReportDiagnostic(
						Diagnostic.Create(
							descriptor,
							trivia.Token.GetLocation(),
							additionalLocations: [Location.Create(context.Tree, trivia.FullSpan)]
						)
					);
				}
			}
		);
	}

	/// <summary>Skips a file with no documentation comment before its trivia is walked.</summary>
	private static void ForEachDocComment(SyntaxTreeAnalysisContext context, Action<SyntaxTrivia, DocumentationCommentTriviaSyntax> visit)
	{
		if (!StanzaCommentCache.HasDocumentationComment(context.Tree, context.CancellationToken))
			return;

		foreach (SyntaxTrivia trivia in StanzaCommentCache.GetDocumentationComments(context.Tree, context.CancellationToken))
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (trivia.GetStructure() is DocumentationCommentTriviaSyntax comment)
				visit(trivia, comment);
		}
	}

	/// <summary>
	/// Reports each span with its whole comment as the second location, so the fix reads the comment it changes.
	/// </summary>
	private static void ReportEach(
		SyntaxTreeAnalysisContext context,
		DiagnosticDescriptor descriptor,
		SyntaxTrivia trivia,
		IReadOnlyList<TextSpan> spans,
		FixOptions? fixOptions = null
	)
	{
		foreach (TextSpan span in spans)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					descriptor,
					location: Location.Create(context.Tree, span),
					additionalLocations: [Location.Create(context.Tree, trivia.FullSpan)],
					properties: fixOptions?.ToProperties()
				)
			);
		}
	}

	/// <summary>Whether a <c>/** */</c> comment has code after its closer or uneven stars, so it stays.</summary>
	private static bool LayoutDeclines(SourceText sourceText, SyntaxTrivia trivia)
	{
		return trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
			&& (!EndsItsLine(sourceText, trivia) || DocCommentLists.HasUnevenStars(sourceText, trivia.Span));
	}

	private static bool EndsItsLine(SourceText sourceText, SyntaxTrivia trivia)
	{
		TextLine line = sourceText.Lines.GetLineFromPosition(trivia.FullSpan.End);
		return trivia.FullSpan.End >= line.End || StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(trivia.FullSpan.End, line.End));
	}

	private static DocLayoutOptions BuildDocLayout(StanzaOptions options, string indentation, DocCommentDelimiters delimiters)
	{
		// The caller has already refused a file whose width key turned the layout off.
		return options.DocLayout!.Value.NarrowBy(MarkupColumns.GetColumns(indentation, options.TabColumns) + delimiters.MarkerWidth);
	}

	/// <summary>Whether the layout rule or the paragraph rule writes the comment.</summary>
	private static bool CommentRulesOwnTheComment(
		RuleScope scope,
		StanzaOptions options,
		StanzaRule rule,
		SyntaxTrivia trivia,
		DocumentationCommentTriviaSyntax comment,
		DocLayoutOptions layout
	)
	{
		TextSpan? paragraphBreak = DocCommentSyntax.FindParagraphBreak(comment);
		bool markParagraphs = paragraphBreak is not null;

		// The owner reports at the paragraph break, or at the member's first token, so its state is read there.
		int position = paragraphBreak?.Start ?? trivia.Token.SpanStart;

		DiagnosticDescriptor owner = markParagraphs
			? DiagnosticDescriptors.MarkDocumentationParagraphs
			: DiagnosticDescriptors.LayOutDocumentationComment;

		return !StanzaRuleState.IsOffAt(scope, options, rule: rule.GetDeferral(owner), position)
			&& DocCommentEdits.WriteLines(comment, layout, markParagraphs) is not null;
	}
}
