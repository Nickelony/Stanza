using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;

namespace Nickelony.Stanza.Markup.Core.Xml;

/// <summary>Parses a markup document into its element tree and comments, bounded against hostile input.</summary>
internal static class MarkupParser
{
	private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

	private const char ByteOrderMark = (char)0xFEFF;

	/// <summary>
	/// Caps the characters entity expansion may add, so a self-multiplying entity cannot exhaust memory.
	/// </summary>
	private const long MaxExpandedEntityCharacters = 1024L * 1024L;

	/// <summary>Parses a markup document; the text must start without a byte order mark.</summary>
	/// <exception cref="XmlException">The text is malformed, starts with U+FEFF, or passes a bound.</exception>
	public static (MarkupElement? Root, IReadOnlyList<MarkupComment> Comments) Parse(
		MarkupSourceText sourceText,
		long maxDocumentCharacters,
		int maxElements = MarkupLimits.DefaultMaxElements,
		CancellationToken cancellationToken = default
	)
	{
		// The caller strips the mark it reads, so one left at the front would only surface as "Data at the root level is invalid".
		if (sourceText.Text.Length > 0 && sourceText.Text[0] == ByteOrderMark)
			throw new XmlException("The document starts with a byte order mark (U+FEFF), which this parser does not read.");

		// A document over the bound is refused before the reader starts, so a document with no root element is bounded too.
		if (maxDocumentCharacters > 0 && sourceText.Text.Length > maxDocumentCharacters)
			throw new XmlException($"The document holds {sourceText.Text.Length} characters, past the {maxDocumentCharacters} this parser lays out.");

		using StringReader textReader = new(sourceText.Text);
		using var reader = XmlReader.Create(textReader, BuildReaderSettings());

		ParseState state = new() { MaxElements = maxElements };

		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();

			bool read;

			try
			{
				read = reader.Read();
			}
			catch (XmlException exception) when (!state.SawContent && IsEndOfInput(exception) && HoldsOnlyCommentsAndInstructions(sourceText.Text))
			{
				break;
			}

			if (!read)
				break;

			ReadNode(sourceText, reader, state);
		}

		Seal(state.Root);

		return (state.Root, state.Comments);
	}

	private static XmlReaderSettings BuildReaderSettings()
	{
		return new XmlReaderSettings
		{
			ConformanceLevel = ConformanceLevel.Document,

			// The DOCTYPE is parsed, but no external resource is resolved:
			// an entity the document declares itself is expanded, and an external one cannot be reached.
			DtdProcessing = DtdProcessing.Parse,
			MaxCharactersFromEntities = MaxExpandedEntityCharacters,
			IgnoreComments = false,
			IgnoreProcessingInstructions = false,
			IgnoreWhitespace = false,
			XmlResolver = null,
		};
	}

	private static void ReadNode(MarkupSourceText sourceText, XmlReader reader, ParseState state)
	{
		switch (reader.NodeType)
		{
			case XmlNodeType.DocumentType:
				state.DocumentType = FindDocumentType(sourceText, GetOffset(sourceText, GetLineInfo(reader)));
				break;

			case XmlNodeType.Element:
				state.SawContent = true;

				// The reader reports a node of an entity at its declaration, so markup there is skipped:
				// an entity used twice would otherwise be laid out twice at the one place.
				if (state.SkippedDepth > 0 || IsInsideDocumentType(sourceText, reader, state))
				{
					if (!reader.IsEmptyElement)
						state.SkippedDepth++;

					break;
				}

				// Each element the tree holds costs a few hundred bytes, so the count is bounded as the length is.
				if (++state.ElementCount > state.MaxElements)
				{
					// The element that passes the cap is where the refusal stands, so a report points at it and not at the first line.
					IXmlLineInfo position = GetLineInfo(reader);

					throw new XmlException(
						message: $"The document holds more than {state.MaxElements} elements, past the limit this parser lays out.",
						innerException: null,
						lineNumber: position.LineNumber,
						linePosition: position.LinePosition
					);
				}

				state.Root = ReadElement(sourceText, reader, open: state.Open, root: state.Root);
				break;

			case XmlNodeType.EndElement:
				if (state.SkippedDepth > 0)
					state.SkippedDepth--;
				else
					CloseElement(sourceText, reader, state.Open);

				break;

			case XmlNodeType.Comment:
				if (state.SkippedDepth == 0 && !IsInsideDocumentType(sourceText, reader, state))
					ReadComment(sourceText, reader, open: state.Open, comments: state.Comments);

				break;

			case XmlNodeType.ProcessingInstruction:
				if (state.SkippedDepth == 0 && !IsInsideDocumentType(sourceText, reader, state))
					ReadProcessingInstruction(sourceText, reader, state.Open);

				break;

			case XmlNodeType.CDATA:
			case XmlNodeType.Text:
			case XmlNodeType.Whitespace:
			case XmlNodeType.SignificantWhitespace:
				bool isContent = IsContent(sourceText, reader);

				// Text inside a skipped entity's expansion belongs to the entity, not to the element around the reference.
				if (state.SkippedDepth == 0)
					ReadText(reader, state.Open, isContent);

				state.SawContent |= isContent;
				break;
		}
	}

	private static bool IsInsideDocumentType(MarkupSourceText sourceText, XmlReader reader, ParseState state) =>
		state.DocumentType.Contains(GetOffset(sourceText, GetLineInfo(reader)));

	/// <summary>Sealing is iterative, like the parse walk: deep nesting cannot overflow the stack.</summary>
	private static void Seal(MarkupElement? root)
	{
		if (root is null)
			return;

		Stack<MarkupElement> pending = new();

		pending.Push(root);

		while (pending.Count > 0)
		{
			MarkupElement element = pending.Pop();

			element.Seal();

			foreach (MarkupElement child in element.ChildElements)
				pending.Push(child);
		}
	}

	private static bool IsEndOfInput(XmlException exception) => exception.LineNumber == 0 && exception.LinePosition == 0;

	/// <summary>
	/// Both a missing root and a failed content read report line 0, column 0, so only a text of nothing but comments
	/// and instructions is a placeholder.
	/// </summary>
	private static bool HoldsOnlyCommentsAndInstructions(string text)
	{
		int offset = 0;

		while (offset < text.Length)
		{
			if (MarkupWhitespace.IsXmlWhitespace(text[offset]))
			{
				offset++;
				continue;
			}

			string opener;
			string closer;

			if (StartsWith(text, offset, marker: "<!--"))
				(opener, closer) = ("<!--", "-->");
			else if (StartsWith(text, offset, marker: "<?"))
				(opener, closer) = ("<?", "?>");
			else
				return false;

			// The closer is searched for after the opener, whose own dashes would start one in `<!-->`.
			int end = text.IndexOf(closer, offset + opener.Length, StringComparison.Ordinal);

			if (end < 0)
				return false;

			offset = end + closer.Length;
		}

		return true;
	}

	private static bool StartsWith(string text, int offset, string marker) =>
		string.CompareOrdinal(strA: text, indexA: offset, strB: marker, indexB: 0, length: marker.Length) == 0;

	/// <summary>Finds the span of the document type declaration the offset falls in.</summary>
	private static MarkupSpan FindDocumentType(MarkupSourceText sourceText, int offset)
	{
		const string opener = "<!DOCTYPE";

		int start = sourceText.Text.LastIndexOf(opener, Math.Min(offset + opener.Length, sourceText.Length - 1), StringComparison.Ordinal);

		if (start < 0)
			return default;

		bool insideSubset = false;
		char quote = '\0';

		for (int cursor = start + opener.Length; cursor < sourceText.Length; cursor++)
		{
			char current = sourceText[cursor];

			if (quote != '\0')
			{
				if (current == quote)
					quote = '\0';
			}
			else if (current is '"' or '\'')
			{
				quote = current;
			}
			else if (IsCommentMarker(sourceText, cursor))
			{
				cursor = GetCommentEnd(sourceText, cursor) + MarkupComment.CloserLength - 1;
			}
			else if (StartsWith(sourceText.Text, cursor, "<?"))
			{
				// An apostrophe in a processing instruction is text rather than a quote, so the instruction is skipped whole.
				int end = sourceText.Text.IndexOf("?>", cursor + 2, StringComparison.Ordinal);

				if (end < 0)
					return MarkupSpan.FromBounds(start, sourceText.Length);

				cursor = end + 1;
			}
			else if (current == '[')
			{
				insideSubset = true;
			}
			else if (current == ']')
			{
				insideSubset = false;
			}
			else if (current == '>' && !insideSubset)
			{
				return MarkupSpan.FromBounds(start, cursor + 1);
			}
		}

		return MarkupSpan.FromBounds(start, sourceText.Length);
	}

	private static MarkupElement? ReadElement(MarkupSourceText sourceText, XmlReader reader, OpenElements open, MarkupElement? root)
	{
		IXmlLineInfo lineInfo = GetLineInfo(reader);

		int tagStart = GetTagStart(sourceText, lineInfo);
		int tagEnd = GetTagEnd(sourceText, tagStart);

		MarkupElement element = new(reader.Name, lineInfo.LineNumber - 1)
		{
			IsSpacePreserved = ResolveSpacePreserved(reader, open.Count > 0 && open.Peek().IsSpacePreserved),
			SourceText = sourceText,
			OpenTagStart = tagStart,
			OpenTagEnd = tagEnd + 1,
			IsEmpty = reader.IsEmptyElement,
		};

		if (reader.IsEmptyElement)
			element.EndLineIndex = sourceText.GetLineFromOffset(tagEnd).LineIndex;

		if (open.Count > 0)
			open.Peek().Add(element);
		else
			root ??= element;

		if (!reader.IsEmptyElement)
			open.Push(element);

		return root;
	}

	private static void CloseElement(MarkupSourceText sourceText, XmlReader reader, OpenElements open)
	{
		MarkupElement element = open.Pop();

		int tagStart = GetCloseTagStart(sourceText, GetLineInfo(reader));
		int tagEnd = GetTagEnd(sourceText, tagStart);

		element.CloseTagStart = tagStart;
		element.CloseTagEnd = tagEnd + 1;
		element.EndLineIndex = sourceText.GetLineFromOffset(tagEnd).LineIndex;
	}

	private static void ReadComment(MarkupSourceText sourceText, XmlReader reader, OpenElements open, List<MarkupComment> comments)
	{
		IXmlLineInfo lineInfo = GetLineInfo(reader);

		int startLineIndex = lineInfo.LineNumber - 1;
		int start = GetCommentStart(sourceText, GetOffset(sourceText, lineInfo));
		int closeStart = GetCommentEnd(sourceText, start);

		MarkupComment comment = new(
			startLineIndex,
			endLineIndex: sourceText.GetLineFromOffset(closeStart).LineIndex,
			start,
			closeStart,
			isSpacePreserved: open.Count > 0 && open.Peek().IsSpacePreserved,
			isInPropertyElement: open.AnyPropertyElement
		);

		// A comment is recorded even when no element holds it, since its markers are laid out either way.
		comments.Add(comment);

		if (open.Count > 0)
			open.Peek().Add(comment);
	}

	private static void ReadProcessingInstruction(MarkupSourceText sourceText, XmlReader reader, OpenElements open)
	{
		if (open.Count == 0)
			return;

		IXmlLineInfo lineInfo = GetLineInfo(reader);
		int startLineIndex = lineInfo.LineNumber - 1;

		MarkupProcessingInstruction instruction = new(startLineIndex, GetProcessingInstructionEndLineIndex(sourceText, lineInfo));

		open.Peek().Add(instruction);
	}

	private static int GetProcessingInstructionEndLineIndex(MarkupSourceText sourceText, IXmlLineInfo lineInfo)
	{
		int startLineIndex = lineInfo.LineNumber - 1;

		for (int offset = GetOffset(sourceText, lineInfo); offset + 1 < sourceText.Length; offset++)
		{
			if (sourceText[offset] == '?' && sourceText[offset + 1] == '>')
				return sourceText.GetLineFromOffset(offset + 1).LineIndex;
		}

		return startLineIndex;
	}

	/// <summary>
	/// A reference such as <c>&amp;#10;</c> is author data even as whitespace, so only literal whitespace is layout.
	/// </summary>
	private static bool IsContent(MarkupSourceText sourceText, XmlReader reader)
	{
		string? value = reader.Value;

		return !string.IsNullOrEmpty(value)
			&& (
				reader.NodeType == XmlNodeType.CDATA
				|| !MarkupWhitespace.IsBlank(value)
				|| WritesAReference(sourceText, GetOffset(sourceText, GetLineInfo(reader)))
			);
	}

	private static void ReadText(XmlReader reader, OpenElements open, bool isContent)
	{
		string? value = reader.Value;

		if (string.IsNullOrEmpty(value) || open.Count == 0)
			return;

		open.Peek().Add(new MarkupText(value));

		if (isContent)
			open.Peek().HasCharacterData = true;
	}

	private static bool WritesAReference(MarkupSourceText sourceText, int offset)
	{
		for (int cursor = Math.Max(offset, 0); cursor < sourceText.Length && sourceText[cursor] != '<'; cursor++)
		{
			if (sourceText[cursor] == '&')
				return true;
		}

		return false;
	}

	private static bool ResolveSpacePreserved(XmlReader reader, bool inherited)
	{
		string? value = reader.GetAttribute("space", XmlNamespace);

		return value switch
		{
			"preserve" => true,
			"default" => false,
			_ => inherited,
		};
	}

	private static int GetTagStart(MarkupSourceText sourceText, IXmlLineInfo lineInfo)
	{
		int offset = GetOffset(sourceText, lineInfo);

		for (int candidate = offset; candidate >= 0; candidate--)
		{
			char current = sourceText[candidate];

			if (current == '<')
				return candidate;

			if (current == '>')
				break;
		}

		return offset;
	}

	private static int GetCloseTagStart(MarkupSourceText sourceText, IXmlLineInfo lineInfo)
	{
		int offset = GetOffset(sourceText, lineInfo);

		for (int candidate = offset; candidate >= offset - 4 && candidate >= 0; candidate--)
		{
			if (candidate + 1 < sourceText.Length && sourceText[candidate] == '<')
			{
				if (sourceText[candidate + 1] == '/')
					return candidate;
			}
		}

		return offset;
	}

	private static int GetTagEnd(MarkupSourceText sourceText, int tagStart)
	{
		char quote = '\0';

		for (int offset = tagStart; offset < sourceText.Length; offset++)
		{
			char current = sourceText[offset];

			if (quote != '\0')
			{
				if (current == quote)
					quote = '\0';

				continue;
			}

			if (current is '"' or '\'')
			{
				quote = current;
				continue;
			}

			if (current == '>')
				return offset;
		}

		return tagStart;
	}

	private static int GetCommentStart(MarkupSourceText sourceText, int offset)
	{
		for (int candidate = offset; candidate >= offset - MarkupComment.OpenerLength && candidate >= 0; candidate--)
		{
			if (IsCommentMarker(sourceText, candidate))
				return candidate;
		}

		return offset;
	}

	private static bool IsCommentMarker(MarkupSourceText sourceText, int offset)
	{
		return offset + 3 < sourceText.Length
			&& sourceText[offset] == '<'
			&& sourceText[offset + 1] == '!'
			&& sourceText[offset + 2] == '-'
			&& sourceText[offset + 3] == '-';
	}

	private static int GetCommentEnd(MarkupSourceText sourceText, int start)
	{
		// The scan starts after the opener: the comment `<!-->` holds text, and its own dashes are not a closer.
		for (int offset = start + MarkupComment.OpenerLength; offset < sourceText.Length - 2; offset++)
		{
			if (sourceText[offset] == '-' && sourceText[offset + 1] == '-' && sourceText[offset + 2] == '>')
				return offset;
		}

		return start;
	}

	private static int GetOffset(MarkupSourceText sourceText, IXmlLineInfo lineInfo) =>
		sourceText.GetLine(lineInfo.LineNumber - 1).Start + lineInfo.LinePosition - 1;

	private static IXmlLineInfo GetLineInfo(XmlReader reader) =>
		reader as IXmlLineInfo ?? throw new InvalidOperationException("The XML reader does not report line information.");

	/// <summary>What the parse has read so far.</summary>
	private sealed class ParseState
	{
		public OpenElements Open { get; } = new();

		public List<MarkupComment> Comments { get; } = [];

		public MarkupElement? Root { get; set; }

		public MarkupSpan DocumentType { get; set; }

		/// <summary>The depth of an entity's markup the parse is skipping; zero outside one.</summary>
		public int SkippedDepth { get; set; }

		/// <summary>Whether content was read; a placeholder document holds only comments and instructions.</summary>
		public bool SawContent { get; set; }

		public int ElementCount { get; set; }

		public int MaxElements { get; init; }
	}

	private sealed class OpenElements
	{
		private readonly Stack<MarkupElement> _elements = new();

		private int _propertyCount;

		public int Count => _elements.Count;
		public bool AnyPropertyElement => _propertyCount > 0;

		public MarkupElement Peek() => _elements.Peek();

		public MarkupElement Pop()
		{
			MarkupElement element = _elements.Pop();

			if (element.IsPropertyElement)
				_propertyCount--;

			return element;
		}

		public void Push(MarkupElement element)
		{
			_elements.Push(element);

			if (element.IsPropertyElement)
				_propertyCount++;
		}
	}
}
