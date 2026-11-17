using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Reads the empty blocks that the collapse rule joins, for the rules that wait for it.</summary>
internal static class StanzaEmptyBlocks
{
	public static TextChange? CreateCollapse(SourceText sourceText, SyntaxToken openBrace, SyntaxToken closeBrace)
	{
		if (!openBrace.IsKind(SyntaxKind.OpenBraceToken) || !closeBrace.IsKind(SyntaxKind.CloseBraceToken))
			return null;

		var gap = TextSpan.FromBounds(openBrace.Span.End, closeBrace.SpanStart);
		return StanzaText.IsWhitespaceOnly(sourceText, gap) && StanzaSyntax.IsMultiLine(sourceText, gap) ? new TextChange(gap, " ") : null;
	}

	/// <summary>Whether the expression spans lines only through empty blocks the collapse rule joins.</summary>
	/// <remarks>
	/// A rule that writes a block body waits, or its block would outlive the line breaks that asked for it.
	/// </remarks>
	public static bool IsSingleLineAfterCollapse(SyntaxNodeAnalysisContext context, StanzaRule rule, StanzaOptions options, SourceText sourceText, ExpressionSyntax expression)
	{
		return ReadCollapsedText(context, rule, options, sourceText, root: expression, nodes: expression.DescendantNodesAndSelf()) is { } text
			&& SourceText.From(text).Lines.Count == 1;
	}

	/// <summary>Reads a node's text as collapsed, or <see langword="null"/> when none of it collapses.</summary>
	public static string? ReadCollapsedText(
		SyntaxNodeAnalysisContext context,
		StanzaRule rule,
		StanzaOptions options,
		SourceText sourceText,
		SyntaxNode root,
		IEnumerable<SyntaxNode> nodes
	)
	{
		if (!options.PreserveSingleLineBlocks)
			return null;

		ImmutableArray<TextChange>.Builder collapses = ImmutableArray.CreateBuilder<TextChange>();
		int removed = 0;

		foreach (SyntaxNode node in nodes)
		{
			if (!StanzaKinds.EmptyBlockKinds.Contains(node.Kind()) || StanzaBraces.FindBracePair(node) is not { } braces)
				continue;

			if (CreateCollapse(sourceText, braces.Previous, braces.Next) is not { } collapse)
				continue;

			DiagnosticDescriptor target = DiagnosticDescriptors.CollapseEmptyBlocks;

			if (!StanzaRuleState.IsRequiredDeferralOnAt(scope: context, options, rule, target, position: braces.Next.SpanStart))
				continue;

			collapses.Add(collapse);
			removed += collapse.Span.Length - collapse.NewText!.Length;
		}

		if (collapses.Count == 0)
			return null;

		return sourceText.WithChanges(collapses).ToString(TextSpan.FromBounds(root.SpanStart, root.Span.End - removed));
	}
}
