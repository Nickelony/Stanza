using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Markup.Core;
using Nickelony.Stanza.Markup.Core.DocComments;

namespace Nickelony.Stanza.Analyzers.Documentation;

/// <summary>Reads the documentation XML of a comment into the engine's document tree.</summary>
internal static class DocCommentSyntax
{
	public static DocCommentDelimiters GetDelimiters(DocumentationCommentTriviaSyntax comment) =>
		comment.ParentTrivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia) ? DocCommentDelimiters.Block : DocCommentDelimiters.Lines;

	/// <summary>The first paragraph break inside the element, or <see langword="null"/> when it holds none.</summary>
	public static MarkupSpan? FindParagraphBreak(XmlElementSyntax element) => ReadContent(element)?.FirstParagraphBreak;

	/// <summary>Finds the first paragraph break in the comment, or <see langword="null"/> when it holds none.</summary>
	public static TextSpan? FindParagraphBreak(DocumentationCommentTriviaSyntax comment)
	{
		if (IsDamaged(comment))
			return null;

		foreach (XmlNodeSyntax child in comment.Content)
		{
			if (child is XmlElementSyntax element && FindParagraphBreak(element) is { } paragraphBreak)
				return new TextSpan(paragraphBreak.Start, paragraphBreak.Length);
		}

		return null;
	}

	public static SyntaxNode? FindDocumentedDeclaration(SyntaxTrivia trivia)
	{
		SyntaxNode? declaration = null;
		SyntaxNode? parent = trivia.Token.Parent;

		if (parent is not null)
		{
			foreach (SyntaxNode node in parent.AncestorsAndSelf())
			{
				if (node is MemberDeclarationSyntax or LocalFunctionStatementSyntax)
				{
					declaration = node;
					break;
				}
			}
		}

		return declaration;
	}

	/// <summary>
	/// Whether a node stands in an opaque element, such as a code sample, or in content that preserves space.
	/// </summary>
	public static bool IsWithinUntouchedMarkup(SyntaxNode node)
	{
		// A start or end tag belongs to its element, so only the elements around that element count.
		SyntaxNode? container = node is XmlElementStartTagSyntax or XmlElementEndTagSyntax ? node.Parent?.Parent : node.Parent;

		if (container is XmlNodeSyntax nearest && IsSpacePreserved(nearest))
			return true;

		for (SyntaxNode? current = container; current is XmlNodeSyntax xml; current = current.Parent)
		{
			if (xml is XmlElementSyntax element && DocElementNames.OpaqueElements.Contains(element.StartTag.Name.LocalName.ValueText))
				return true;
		}

		return false;
	}

	private static DocContent? ReadContent(XmlElementSyntax element) =>
		!IsWithinDepth(element) || BuildElement(element, depth: 0) is not { } built ? null : DocWalk.ReadParagraphs(built, depth: 0);

	/// <summary>Whether the node is, or holds, an element that the layout keeps as written.</summary>
	public static bool HoldsKeptElement(XmlNodeSyntax node)
	{
		foreach (SyntaxNode descendant in node.DescendantNodesAndSelf())
		{
			string? name = descendant switch
			{
				XmlElementSyntax element => element.StartTag.Name.LocalName.ValueText,
				XmlEmptyElementSyntax empty => empty.Name.LocalName.ValueText,
				_ => null,
			};

			if (name is not null && DocElementNames.IsKeptAsWritten(name))
				return true;
		}

		return false;
	}

	/// <summary>Reads the comment into a layout body, or <see langword="null"/> when a node is unreadable.</summary>
	/// <remarks>
	/// A top-level element that holds a sample or a pointer is read as the lines it stands on, so the layout keeps it.
	/// </remarks>
	public static List<DocNode>? BuildLayoutBody(DocumentationCommentTriviaSyntax comment)
	{
		SourceText sourceText = comment.SyntaxTree.GetText();
		DocCommentDelimiters delimiters = GetDelimiters(comment);

		List<DocNode> body = [];

		foreach (XmlNodeSyntax node in comment.Content)
		{
			bool kept = node is XmlElementSyntax or XmlEmptyElementSyntax && HoldsKeptElement(node);

			DocNode? built = kept
				? BuildKeptElement(node, sourceText, delimiters)
				: BuildNode(node, depth: 0, keepsOpaque: false, skipsMarkup: false);

			if (built is null)
				return null;

			body.Add(built);
		}

		return body;
	}

	/// <summary>Reads an element as the lines it stands on, or <see langword="null"/> when one has no marker.</summary>
	private static DocElement? BuildKeptElement(XmlNodeSyntax node, SourceText sourceText, DocCommentDelimiters delimiters)
	{
		TextSpan span = node.Span;

		int firstLine = sourceText.Lines.GetLineFromPosition(span.Start).LineNumber;
		int lastLine = sourceText.Lines.GetLineFromPosition(span.End - 1).LineNumber;

		List<string> lines = [];

		for (int index = firstLine; index <= lastLine; index++)
		{
			TextLine line = sourceText.Lines[index];

			if (DocCommentLines.FindTextStart(sourceText, line, delimiters) is not { } textStart)
			{
				// The element may begin after other text on its line, which keeps no marker of its own to read.
				if (index != firstLine)
					return null;

				textStart = span.Start;
			}

			// The first line keeps the indentation before the element, unless other text stands there too.
			int start = textStart;

			if (
				index == firstLine
				&& (textStart > span.Start || !StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(textStart, span.Start)))
			)
			{
				start = span.Start;
			}

			int end = index == lastLine ? span.End : line.End;

			lines.Add(sourceText.ToString(TextSpan.FromBounds(start, end)));
		}

		string name = node is XmlElementSyntax element
			? element.StartTag.Name.LocalName.ValueText
			: ((XmlEmptyElementSyntax)node).Name.LocalName.ValueText;

		return new DocElement(
			name,
			openTag: string.Empty,
			closeTag: null,
			isEmpty: node is XmlEmptyElementSyntax,
			body: [],
			phrase: ReadCollapsed(node)
		)
		{
			Source = new MarkupSpan(span.Start, span.Length),
			VerbatimLines = lines,
		};
	}

	/// <summary>Reads the nodes into a body, or <see langword="null"/> when one is unreadable.</summary>
	public static List<DocNode>? BuildBody(SyntaxList<XmlNodeSyntax> nodes, int depth, bool keepsOpaque = false, bool skipsMarkup = false)
	{
		List<DocNode> body = [];

		foreach (XmlNodeSyntax node in nodes)
		{
			if (skipsMarkup && node is XmlCommentSyntax or XmlCDataSectionSyntax or XmlProcessingInstructionSyntax)
				continue;

			if (BuildNode(node, depth, keepsOpaque, skipsMarkup) is not { } built)
				return null;

			body.Add(built);
		}

		return body;
	}

	private static DocNode? BuildNode(XmlNodeSyntax node, int depth, bool keepsOpaque, bool skipsMarkup)
	{
		return node switch
		{
			XmlTextSyntax text => ReadText(text),
			XmlEmptyElementSyntax empty => BuildEmptyElement(empty),
			XmlElementSyntax element => BuildElement(element, depth, keepsOpaque, skipsMarkup),
			_ => null,
		};
	}

	public static DocElement? BuildElement(XmlElementSyntax element, int depth, bool keepsOpaque = false, bool skipsMarkup = false)
	{
		string name = element.StartTag.Name.LocalName.ValueText;

		if (
			depth >= DocWalk.MaxElementDepth
			|| (!keepsOpaque && DocElementNames.IsUnreadable(name))
			|| BuildBody(nodes: element.Content, depth: depth + 1, keepsOpaque, skipsMarkup) is not { } body
		)
		{
			return null;
		}

		string openTag = ReadCollapsed(element.StartTag);
		string closeTag = ReadCollapsed(element.EndTag);

		bool isEmpty = false;

		string phrase = ReadCollapsed(element);

		return new DocElement(name, openTag, closeTag, isEmpty, body, phrase, isSpacePreserved: IsSpacePreserved(element))
		{
			Source = new MarkupSpan(element.SpanStart, element.Span.Length),
		};
	}

	private static DocElement BuildEmptyElement(XmlEmptyElementSyntax element)
	{
		string name = element.Name.LocalName.ValueText;

		string openTag = ReadCollapsed(element);
		string? closeTag = null;

		bool isEmpty = true;

		List<DocNode> body = [];
		string phrase = openTag;

		return new DocElement(name, openTag, closeTag, isEmpty, body, phrase, isSpacePreserved: IsSpacePreserved(element))
		{
			Source = new MarkupSpan(element.SpanStart, element.Span.Length),
			SlashGap = ReadSlashGap(element),
		};
	}

	private static string? ReadSlashGap(XmlEmptyElementSyntax element)
	{
		SyntaxToken slash = element.SlashGreaterThanToken;
		SyntaxToken previous = slash.GetPreviousToken();

		return previous.RawKind == 0 || slash.SpanStart < previous.Span.End
			? null
			: element.SyntaxTree.GetText().ToString(TextSpan.FromBounds(previous.Span.End, slash.SpanStart));
	}

	public static bool IsSpacePreserved(XmlNodeSyntax node)
	{
		SyntaxNode? current = node;

		while (current is XmlNodeSyntax xml)
		{
			if (FindStatedSpacePreserved(xml) is { } stated)
				return stated;

			current = current.Parent;
		}

		return false;
	}

	private static bool? FindStatedSpacePreserved(XmlNodeSyntax node)
	{
		SyntaxList<XmlAttributeSyntax> attributes = node switch
		{
			XmlElementSyntax element => element.StartTag.Attributes,
			XmlEmptyElementSyntax empty => empty.Attributes,
			_ => default,
		};

		foreach (XmlAttributeSyntax attribute in attributes)
		{
			if (!IsSpaceAttribute(attribute))
				continue;

			return GetAttributeValue(attribute) switch
			{
				"preserve" => true,
				"default" => false,
				_ => null,
			};
		}

		return null;
	}

	private static bool IsSpaceAttribute(XmlAttributeSyntax attribute) =>
		string.Equals(attribute.Name.ToString(), "xml:space", StringComparison.Ordinal);

	private static string GetAttributeValue(XmlAttributeSyntax attribute)
	{
		string text = attribute.ToString();
		int equals = text.IndexOf('=');

		return equals < 0 ? string.Empty : text.Substring(equals + 1).Trim().Trim('"', '\'');
	}

	public static DocText ReadText(XmlTextSyntax text)
	{
		StringBuilder builder = new(text.Span.Length);
		List<MarkupSpan>? breaks = null;
		List<DocTextPiece> pieces = new(text.TextTokens.Count);

		foreach (SyntaxToken token in text.TextTokens)
		{
			var span = new MarkupSpan(token.Span.Start, token.Span.Length);
			bool isBreak = token.IsKind(SyntaxKind.XmlTextLiteralNewLineToken);

			pieces.Add(new DocTextPiece(builder.Length, span, isBreak));

			if (!isBreak)
			{
				builder.Append(token.Text);
				continue;
			}

			breaks ??= [];
			breaks.Add(span);

			builder.Append('\n');
		}

		return new DocText(builder.ToString(), breaks) { Pieces = pieces };
	}

	public static bool IsWithinDepth(XmlElementSyntax element) => GetElementDepth(element) < DocWalk.MaxElementDepth;

	/// <summary>Counts the elements around an element, up to the depth bound of the walk.</summary>
	public static int GetElementDepth(XmlElementSyntax element)
	{
		int depth = 0;

		for (SyntaxNode? node = element.Parent; node is not null && depth < DocWalk.MaxElementDepth; node = node.Parent)
		{
			if (node is XmlElementSyntax)
				depth++;
		}

		return depth;
	}

	private static string ReadCollapsed(SyntaxNode node) => MarkupCollapser.Collapse(StripMarkers(node));

	private static string StripMarkers(SyntaxNode node)
	{
		string text = node.ToString();
		StringBuilder stripped = new(text.Length);

		int copied = 0;

		foreach (SyntaxTrivia trivia in node.DescendantTrivia(descendIntoTrivia: true))
		{
			if (!trivia.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia) || trivia.SpanStart < node.SpanStart || trivia.Span.End > node.Span.End)
				continue;

			int markerStart = trivia.SpanStart - node.SpanStart;
			stripped.Append(text, copied, markerStart - copied);

			copied = markerStart + trivia.Span.Length;
		}

		stripped.Append(text, copied, text.Length - copied);

		return stripped.ToString();
	}

	/// <summary>
	/// Whether the parser repaired the comment, read markup as text, or ended a line at a break the rewrites skip.
	/// </summary>
	public static bool IsDamaged(DocumentationCommentTriviaSyntax comment)
	{
		// A rule leaves a damaged comment as it is: its markup is not the author's, and a rewrite would split what the parser read as text.
		foreach (SyntaxToken token in comment.DescendantTokens())
		{
			if (token.IsMissing || ClosesAnotherElement(token))
				return true;

			// Text inside a CDATA section or an XML comment may hold a `<`; text between tags may not.
			if (
				token.IsKind(SyntaxKind.XmlTextLiteralToken)
				&& token.Parent is XmlTextSyntax
				&& token.Text.IndexOf('<') >= 0
				&& !IsLoneEndTag(token.Text)
			)
			{
				return true;
			}
		}

		// Roslyn also ends a line at U+0085, U+2028 and U+2029, but the rewrites read only carriage returns and line feeds,
		// so they would join the line after such a break into the comment.
		foreach (char character in comment.ToFullString())
		{
			if (StanzaText.IsExtraLineBreak(character))
				return true;
		}

		return false;
	}

	/// <summary>Whether the token names an end tag that the parser let close an element of another name.</summary>
	private static bool ClosesAnotherElement(SyntaxToken token)
	{
		return token.Parent is XmlNameSyntax { Parent: XmlElementEndTagSyntax { Parent: XmlElementSyntax element } } name
			&& !string.Equals(name.ToString(), element.StartTag.Name.ToString(), StringComparison.Ordinal);
	}

	/// <summary>
	/// Whether the text is a stray end tag: read as text, it names no element and holds no space a wrap could split.
	/// </summary>
	private static bool IsLoneEndTag(string text)
	{
		string trimmed = text.Trim();

		return trimmed.StartsWith("</", StringComparison.Ordinal)
			&& trimmed.EndsWith(">", StringComparison.Ordinal)
			&& trimmed.IndexOf('<', 1) < 0
			&& trimmed.IndexOf(' ') < 0;
	}
}
