using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.DocComments;

namespace Nickelony.Stanza.Analyzers.Documentation;

/// <summary>Creates the edits to a documentation comment that its rules report and its fixes apply.</summary>
internal static class DocCommentEdits
{
	/// <summary>Lays the comment out, or returns <see langword="null"/> when it stays as written.</summary>
	public static List<string>? WriteLines(DocumentationCommentTriviaSyntax comment, DocLayoutOptions layout, bool markParagraphs)
	{
		if (layout.ContentWidth <= 0 || DocCommentSyntax.IsDamaged(comment))
			return null;

		return DocCommentSyntax.BuildBody(comment.Content, depth: 0) is { } body ? DocWalk.Write(body, layout, markParagraphs) : null;
	}

	/// <summary>Writes the comment in the layout, or returns <see langword="null"/> when it stands so.</summary>
	public static TextChange? CreateLayout(
		SourceText sourceText,
		SyntaxTrivia trivia,
		DocumentationCommentTriviaSyntax comment,
		DocLayoutOptions layout,
		bool markParagraphs
	)
	{
		TextSpan prefix = DocCommentLines.GetLinePrefix(sourceText, trivia);
		string current = trivia.ToFullString();

		string? wanted = BuildCommentText(
			comment,
			frame: DocCommentFrame.Read(sourceText, prefix, comment),
			layout,
			trailing: DocCommentLines.GetTrailingLineBreak(current),
			markParagraphs
		);

		return wanted is null || string.Equals(wanted, current, StringComparison.Ordinal) ? null : new TextChange(trivia.FullSpan, wanted);
	}

	public static string? BuildCommentText(
		DocumentationCommentTriviaSyntax comment,
		DocCommentFrame frame,
		DocLayoutOptions layout,
		string trailing,
		bool markParagraphs
	)
	{
		if (WriteLines(comment, layout, markParagraphs) is not { } lines)
			return null;

		StringBuilder builder = new();
		DocCommentDelimiters delimiters = frame.Delimiters;

		if (delimiters.IsBlock)
		{
			builder.Append(delimiters.Opening);

			// Each block marker owns a line, and a blank one holds the star alone, without a trailing space.
			foreach (string line in lines)
				builder.Append(frame.LineEnding).Append(frame.Indentation).Append(DocCommentDelimiters.BuildLine(delimiters.Marker, line));

			builder.Append(frame.LineEnding).Append(frame.Indentation).Append(delimiters.Closing);
			builder.Append(trailing);

			return builder.ToString();
		}

		for (int index = 0; index < lines.Count; index++)
		{
			if (index > 0)
				builder.Append(frame.LineEnding).Append(frame.Indentation);

			// The indentation before the first line sits outside the comment,
			// so the first line is written without it and every line after it repeats it.
			builder.Append(DocCommentDelimiters.BuildLine(delimiters.Marker, lines[index]));
		}

		builder.Append(trailing);

		return builder.ToString();
	}

	/// <summary>Builds each blank line that separates nothing, as the removal and the line it removes.</summary>
	public static List<DocCommentEdit> CreatePaddingEdits(DocumentationCommentTriviaSyntax comment) =>
		BuildEdits(FindCleanupChanges(comment, DocCleanupKind.Padding), static change => ToTextSpan(change.Span));

	/// <summary>Finds the changes of one clean-up, each standing against the comment as written.</summary>
	private static List<MarkupChange> FindCleanupChanges(DocumentationCommentTriviaSyntax comment, DocCleanupKind kind, bool spacedSlash = false)
	{
		// The clean-up judges the prose around a code sample and leaves the sample as written, so the body it reads keeps it.
		// The slash of a tag stands apart from the markup around it, so a comment, a CDATA section or an instruction is skipped for it alone.
		bool skipsMarkup = kind == DocCleanupKind.SlashShape;

		if (
			DocCommentSyntax.IsDamaged(comment)
			|| DocCommentSyntax.BuildBody(nodes: comment.Content, depth: 0, keepsOpaque: true, skipsMarkup) is not { } body
		)
		{
			return [];
		}

		return DocCleanup.FindChanges(body, kind, spacedSlash);
	}

	private static List<DocCommentEdit> BuildEdits(List<MarkupChange> changes, Func<MarkupChange, TextSpan> findAnchor)
	{
		List<DocCommentEdit> edits = [];

		foreach (MarkupChange change in changes)
			edits.Add(new DocCommentEdit(new TextChange(ToTextSpan(change.Span), change.NewText), findAnchor(change)));

		return edits;
	}

	private static TextSpan ToTextSpan(MarkupSpan span) => new(span.Start, span.Length);

	public static TextSpan? FindDeclarationGap(SyntaxTrivia trivia, SourceText sourceText, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (
			trivia.GetStructure() is not DocumentationCommentTriviaSyntax
			|| DocCommentSyntax.FindDocumentedDeclaration(trivia) is not { } declaration
			|| trivia.Token != declaration.GetFirstToken()
		)
		{
			return null;
		}

		int commentLine = DocCommentLines.GetLastLineIndex(sourceText, trivia.Span);
		int tokenLine = sourceText.Lines.GetLinePosition(trivia.Token.SpanStart).Line;

		if (tokenLine - commentLine < 2)
			return null;

		var gap = TextSpan.FromBounds(sourceText.Lines[commentLine].EndIncludingLineBreak, sourceText.Lines[tokenLine].Start);
		return StanzaText.IsWhitespaceOnly(sourceText, gap) ? gap : null;
	}

	/// <summary>Removes the gap below the token's last documentation comment, or <see langword="null"/>.</summary>
	public static TextChange? CreateDeclarationGapRemoval(SyntaxToken token, SourceText sourceText, CancellationToken cancellationToken)
	{
		return FindDocumentedComment(token) is { } comment && FindDeclarationGap(comment, sourceText, cancellationToken) is { } gap
			? new TextChange(gap, string.Empty)
			: null;
	}

	private static SyntaxTrivia? FindDocumentedComment(SyntaxToken token)
	{
		SyntaxTrivia? last = null;

		foreach (SyntaxTrivia trivia in token.LeadingTrivia)
		{
			if (trivia.GetStructure() is DocumentationCommentTriviaSyntax)
				last = trivia;
		}

		return last;
	}

	/// <summary>Builds the <see langword="null"/> keywords of the prose, with the element each becomes.</summary>
	public static List<DocCommentEdit> CreateNullKeywordEdits(DocumentationCommentTriviaSyntax comment, bool spacedSlash) =>
		BuildEdits(FindCleanupChanges(comment, DocCleanupKind.NullKeyword, spacedSlash), static change => ToTextSpan(change.Span));

	/// <summary>Builds each period the text lacks, as the insertion and the character it follows.</summary>
	public static List<DocCommentEdit> CreateMissingPeriodEdits(DocumentationCommentTriviaSyntax comment, SourceText sourceText)
	{
		return BuildEdits(
			FindCleanupChanges(comment, DocCleanupKind.Period),

			change =>
			{
				int end = change.Span.Start;
				int start = end - (end >= 2 && char.IsLowSurrogate(sourceText[end - 1]) ? 2 : 1);

				return new TextSpan(start, end - start);
			}
		);
	}

	/// <summary>Builds each gap before a slash that the shape asks to change, as its new text and the slash.</summary>
	public static List<DocCommentEdit> CreateSlashGapEdits(DocumentationCommentTriviaSyntax comment, bool spaced)
	{
		// Most comments hold no self-closing tag, so they skip the body the clean-up reads.
		if (!comment.DescendantNodes().Any(static node => node is XmlEmptyElementSyntax))
			return [];

		return BuildEdits(FindCleanupChanges(comment, DocCleanupKind.SlashShape, spaced), static change => new TextSpan(change.Span.End, 2));
	}

	public static ImmutableArray<TextChange> CreateEmptyElement(
		DocumentationCommentTriviaSyntax comment,
		bool spaced,
		CancellationToken cancellationToken
	)
	{
		ImmutableArray<TextChange>.Builder changes = ImmutableArray.CreateBuilder<TextChange>();
		string text = comment.ToFullString();

		foreach (MarkupChange change in FindCleanupChanges(comment, DocCleanupKind.EmptyElement, spaced))
		{
			cancellationToken.ThrowIfCancellationRequested();

			// A start tag written across lines holds line structure, which the layout rule owns.
			if (!StartTagSpansALine(text, start: change.Span.Start - comment.FullSpan.Start))
				changes.Add(new TextChange(ToTextSpan(change.Span), change.NewText));
		}

		return changes.ToImmutable();
	}

	private static bool StartTagSpansALine(string text, int start)
	{
		for (int offset = start; offset >= 0 && offset < text.Length && text[offset] != '>'; offset++)
		{
			if (text[offset] is '\n' or '\r')
				return true;
		}

		return false;
	}

	/// <summary>Writes one space over the tag whitespace run at the position, or <see langword="null"/>.</summary>
	public static TextChange? CreateTagSpace(
		DocumentationCommentTriviaSyntax comment,
		SourceText sourceText,
		int position,
		CancellationToken cancellationToken
	)
	{
		foreach (TextSpan run in FindTagWhitespaceRuns(comment, sourceText, cancellationToken))
		{
			if (run.Start == position)
				return new TextChange(run, " ");
		}

		return null;
	}

	public static List<TextSpan> FindTagWhitespaceRuns(
		DocumentationCommentTriviaSyntax comment,
		SourceText sourceText,
		CancellationToken cancellationToken
	)
	{
		if (DocCommentSyntax.IsDamaged(comment))
			return [];

		List<TextSpan> runs = [];

		foreach (SyntaxNode node in comment.DescendantNodes())
		{
			cancellationToken.ThrowIfCancellationRequested();

			SyntaxNode? tag = node switch
			{
				XmlElementStartTagSyntax start => start,
				XmlElementEndTagSyntax end => end,
				XmlEmptyElementSyntax empty => empty,
				_ => null,
			};

			if (tag is null || DocCommentSyntax.IsWithinUntouchedMarkup(tag))
				continue;

			foreach (SyntaxToken token in tag.DescendantTokens())
			{
				// The slash of a self-closing tag carries the gap its pair owns,
				// so the scan skips that token.
				// Other gaps inside a tag are runs of spaces or tabs before a token.
				if (token.IsKind(SyntaxKind.SlashGreaterThanToken))
					continue;

				// A gap that spans a line is line structure, which the layout rule owns,
				// so the scan skips it and measures only the runs of spaces or tabs.
				SyntaxToken previous = token.GetPreviousToken();

				string gap = sourceText.ToString(TextSpan.FromBounds(previous.Span.End, token.SpanStart));

				if (gap.IndexOf('\n') >= 0 || gap.IndexOf('\r') >= 0)
					continue;

				foreach (SyntaxTrivia piece in token.LeadingTrivia)
				{
					// The marker of a comment without stars is the indentation of its line, not space inside a tag.
					if (piece.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia) || !IsHorizontalWhitespaceRun(piece.ToString()))
						continue;

					runs.Add(piece.Span);
				}
			}
		}

		return runs;
	}

	private static bool IsHorizontalWhitespaceRun(string text)
	{
		if (text.Length < 2)
			return false;

		foreach (char character in text)
		{
			if (character is not (' ' or '\t'))
				return false;
		}

		return true;
	}
}
