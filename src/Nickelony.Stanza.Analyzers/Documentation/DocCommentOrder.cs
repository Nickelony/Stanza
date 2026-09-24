using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nickelony.Stanza.Markup.Core.DocComments;

namespace Nickelony.Stanza.Analyzers.Documentation;

/// <summary>Builds the edits that put each run of documentation elements back in order.</summary>
internal static class DocCommentOrder
{
	public static List<DocCommentEdit> CreateElementOrderEdits(DocumentationCommentTriviaSyntax comment, SourceText sourceText, SyntaxNode? owner)
	{
		if (DocCommentSyntax.IsDamaged(comment))
			return [];

		List<OrderSlot> slots = [];

		foreach (XmlNodeSyntax node in comment.Content)
		{
			if (FindElementName(node) is not { } name)
				continue;

			slots.Add(new OrderSlot(node, name, index: slots.Count, sourceText));
		}

		if (slots.Count < 2)
			return [];

		// The declaration's signature is read only for a comment that orders a parameter or a type parameter:
		// no other element's place depends on it.
		SignatureNames signature = HoldsASignedElement(slots) ? GetSignatureNames(owner) : SignatureNames.None;

		DocCommentDelimiters delimiters = DocCommentSyntax.GetDelimiters(comment);

		int[] ranks = new int[slots.Count];

		for (int index = 0; index < slots.Count; index++)
			ranks[index] = slots[index].Rank;

		int openingLineIndex = sourceText.Lines.GetLinePosition(comment.FullSpan.Start).Line;
		List<DocCommentEdit> edits = [];

		foreach (
			DocMisorderedRun run in DocElementOrder.FindMisorderedRuns(
				ranks,
				compareWithinRank: (left, right) => CompareNames(slots[left], slots[right], signature)
			)
		)
		{
			if (
				!DocCommentLines.StandsOnTheOpeningLine(delimiters, slots[run.Start].StartLineIndex, openingLineIndex)
				&& CreateOrderEdit(run, slots, sourceText, delimiters) is { } edit
			)
			{
				edits.Add(edit);
			}
		}

		return edits;
	}

	private static DocCommentEdit? CreateOrderEdit(
		DocMisorderedRun run,
		List<OrderSlot> slots,
		SourceText sourceText,
		DocCommentDelimiters delimiters
	)
	{
		if (!CoversTheRun(slots, start: run.Start, end: run.End, sourceText, delimiters))
			return null;

		TextLine first = sourceText.Lines[slots[run.Start].StartLineIndex];
		TextLine last = sourceText.Lines[slots[run.End - 1].EndLineIndex];

		var region = TextSpan.FromBounds(first.Start, last.EndIncludingLineBreak);
		string text = BuildRunText(slots, start: run.Start, sourceText, order: run.Arrangement);

		return new DocCommentEdit(new TextChange(region, text), slots[run.Start + run.Misplaced].Anchor);
	}

	/// <summary>
	/// Whether each element has its lines to itself, with only blank comment lines between two, as a move takes
	/// whole lines.
	/// </summary>
	private static bool CoversTheRun(List<OrderSlot> slots, int start, int end, SourceText sourceText, DocCommentDelimiters delimiters)
	{
		TextLineCollection lines = sourceText.Lines;

		// Any other layout would make a move carry text it does not order, such as prose, a sibling or the `*/` a comment closes with,
		// or leave behind a line of prose.
		for (int index = start; index < end; index++)
		{
			OrderSlot slot = slots[index];
			TextLine lastLine = lines[slot.EndLineIndex];

			if (
				!DocCommentLines.StandsAfterAMarker(sourceText, line: lines[slot.StartLineIndex], position: slot.Node.SpanStart, delimiters)
				|| !StanzaText.IsWhitespaceOnly(sourceText, TextSpan.FromBounds(slot.Node.Span.End, lastLine.End))
			)
			{
				return false;
			}

			if (index == start)
				continue;

			for (int line = slots[index - 1].EndLineIndex + 1; line < slot.StartLineIndex; line++)
			{
				if (!DocCommentLines.StandsAfterAMarker(sourceText, line: lines[line], position: lines[line].End, delimiters))
					return false;
			}
		}

		return true;
	}

	/// <summary>Moves the elements, and keeps the blank lines between two places where they stand.</summary>
	private static string BuildRunText(List<OrderSlot> slots, int start, SourceText sourceText, int[] order)
	{
		TextLineCollection lines = sourceText.Lines;
		StringBuilder text = new();

		for (int index = 0; index < order.Length; index++)
		{
			OrderSlot slot = slots[start + order[index]];

			text.Append(sourceText.ToString(TextSpan.FromBounds(lines[slot.StartLineIndex].Start, lines[slot.EndLineIndex].EndIncludingLineBreak)));

			if (index + 1 < order.Length)
			{
				int gapStart = lines[slots[start + index].EndLineIndex].EndIncludingLineBreak;
				int gapEnd = lines[slots[start + index + 1].StartLineIndex].Start;

				text.Append(sourceText.ToString(TextSpan.FromBounds(gapStart, gapEnd)));
			}
		}

		return text.ToString();
	}

	/// <summary>Breaks a tie of one rank by the signature's order of the names, then by position.</summary>
	private static int CompareNames(OrderSlot left, OrderSlot right, SignatureNames signature)
	{
		int comparison = GetNamedIndex(left, signature).CompareTo(GetNamedIndex(right, signature));
		return comparison == 0 ? left.Index.CompareTo(right.Index) : comparison;
	}

	private static int GetNamedIndex(OrderSlot slot, SignatureNames signature)
	{
		return slot.Name switch
		{
			"param" => GetSortIndex(signature.Parameters, slot.Named),
			"typeparam" => GetSortIndex(signature.TypeParameters, slot.Named),
			_ => slot.Index,
		};
	}

	private static int GetSortIndex(List<string> names, string? name)
	{
		if (name is null)
			return int.MaxValue;

		int index = names.IndexOf(name);
		return index < 0 ? int.MaxValue : index;
	}

	private static bool HoldsASignedElement(List<OrderSlot> slots)
	{
		foreach (OrderSlot slot in slots)
		{
			if (slot.Name is "param" or "typeparam")
				return true;
		}

		return false;
	}

	private static SignatureNames GetSignatureNames(SyntaxNode? owner)
	{
		return owner switch
		{
			MethodDeclarationSyntax method => new SignatureNames(
				GetParameterNames(method.ParameterList),
				GetTypeParameterNames(method.TypeParameterList)
			),

			LocalFunctionStatementSyntax function => new SignatureNames(
				GetParameterNames(function.ParameterList),
				GetTypeParameterNames(function.TypeParameterList)
			),

			DelegateDeclarationSyntax declaration => new SignatureNames(
				GetParameterNames(declaration.ParameterList),
				GetTypeParameterNames(declaration.TypeParameterList)
			),

			BaseMethodDeclarationSyntax method => new SignatureNames(GetParameterNames(method.ParameterList), []),
			IndexerDeclarationSyntax indexer => new SignatureNames(GetParameterNames(indexer.ParameterList), []),
			TypeDeclarationSyntax type => new SignatureNames(GetParameterNames(type.ParameterList), GetTypeParameterNames(type.TypeParameterList)),
			_ => SignatureNames.None,
		};
	}

	private static List<string> GetParameterNames(BaseParameterListSyntax? list)
	{
		if (list is null)
			return [];

		List<string> names = [];

		foreach (ParameterSyntax parameter in list.Parameters)
			names.Add(parameter.Identifier.ValueText);

		return names;
	}

	private static List<string> GetTypeParameterNames(TypeParameterListSyntax? list)
	{
		if (list is null)
			return [];

		List<string> names = [];

		foreach (TypeParameterSyntax parameter in list.Parameters)
			names.Add(parameter.Identifier.ValueText);

		return names;
	}

	private static string? FindElementName(XmlNodeSyntax node)
	{
		return node switch
		{
			XmlElementSyntax element => element.StartTag.Name.LocalName.ValueText,
			XmlEmptyElementSyntax empty => empty.Name.LocalName.ValueText,
			_ => null,
		};
	}

	private static string? FindNamedAttribute(XmlNodeSyntax node)
	{
		SyntaxList<XmlAttributeSyntax> attributes = node switch
		{
			XmlElementSyntax element => element.StartTag.Attributes,
			XmlEmptyElementSyntax empty => empty.Attributes,
			_ => default,
		};

		foreach (XmlAttributeSyntax attribute in attributes)
		{
			if (attribute is XmlNameAttributeSyntax named && string.Equals(named.Name.LocalName.ValueText, "name", StringComparison.Ordinal))
				return named.Identifier.Identifier.ValueText;
		}

		return null;
	}

	/// <summary>The names a declaration gives its parameters and type parameters, in order.</summary>
	private sealed record SignatureNames(List<string> Parameters, List<string> TypeParameters)
	{
		public static SignatureNames None { get; } = new([], []);
	}

	private readonly struct OrderSlot(XmlNodeSyntax node, string name, int index, SourceText sourceText)
	{
		public XmlNodeSyntax Node { get; } = node;
		public string Name { get; } = name;
		public int Index { get; } = index;

		public int Rank { get; } = DocElementOrder.FindRank(DocElementOrder.ElementOrder, name);
		public int StartLineIndex { get; } = sourceText.Lines.GetLineFromPosition(node.SpanStart).LineNumber;
		public int EndLineIndex { get; } = DocCommentLines.GetLastLineIndex(sourceText, node.Span);
		public string? Named { get; } = FindNamedAttribute(node);
		public TextSpan Anchor { get; } = node is XmlElementSyntax element ? element.StartTag.Span : node.Span;
	}
}
