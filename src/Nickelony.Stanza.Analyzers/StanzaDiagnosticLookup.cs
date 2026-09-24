using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Finds the units and tokens a diagnostic names, so a fix can read them back.</summary>
internal static class StanzaDiagnosticLookup
{
	public static TokenPair? FindTokenPair(SyntaxNode root, Diagnostic diagnostic)
	{
		if (diagnostic.AdditionalLocations.Count != 1)
			return null;

		TextSpan anchor = diagnostic.Location.SourceSpan;
		TextSpan neighbor = diagnostic.AdditionalLocations[0].SourceSpan;

		(TextSpan previous, TextSpan next) = anchor.Start <= neighbor.Start ? (anchor, neighbor) : (neighbor, anchor);

		if (previous.End > next.Start)
			return null;

		SyntaxToken previousToken = root.FindToken(Math.Max(previous.End - 1, 0));
		SyntaxToken nextToken = root.FindToken(next.Start);

		return previousToken.Span.End <= nextToken.SpanStart ? new TokenPair(previousToken, nextToken) : null;
	}

	/// <summary>
	/// Whether every span lies inside the text; it does not check which tree produced the diagnostic.
	/// </summary>
	public static bool IsDiagnosticInDocument(Diagnostic diagnostic, SourceText sourceText)
	{
		if (!IsSpanInDocument(diagnostic.Location.SourceSpan, sourceText))
			return false;

		foreach (Location location in diagnostic.AdditionalLocations)
		{
			if (!IsSpanInDocument(location.SourceSpan, sourceText))
				return false;
		}

		return true;
	}

	private static bool IsSpanInDocument(TextSpan span, SourceText sourceText) =>
		span.Start >= 0 && span.Start <= span.End && span.End <= sourceText.Length;

	public static NeighborUnits? FindNeighborUnits(SyntaxNode root, Diagnostic diagnostic)
	{
		// The separating rules attach the previous unit as an additional location,
		// so the neighboring units can be read straight off the diagnostic,
		// instead of walking the ancestor chain.
		if (diagnostic.AdditionalLocations.Count == 1)
		{
			SyntaxNode previous = root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan, getInnermostNodeForTie: false);
			SyntaxNode next = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: false);

			// The diagnostic sits on the unit's first token, so the unit is the outermost node that starts there.
			while (next.Parent is { } parent && parent.SpanStart == next.SpanStart && parent is not GlobalStatementSyntax)
				next = parent;

			if (previous.Span.End <= next.SpanStart)
				return new NeighborUnits(previous, next);
		}

		return FindNeighborUnits(root, diagnostic.Location.SourceSpan);
	}

	private static NeighborUnits? FindNeighborUnits(SyntaxNode root, TextSpan diagnosticSpan)
	{
		SyntaxNode? candidate = root.FindNode(diagnosticSpan, getInnermostNodeForTie: false);

		while (candidate is not null)
		{
			if (candidate.Parent is not null)
			{
				ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(candidate.Parent);
				int index = -1;

				for (int siblingIndex = 0; siblingIndex < siblings.Length; siblingIndex++)
				{
					if (siblings[siblingIndex].Span == candidate.Span)
					{
						index = siblingIndex;
						break;
					}
				}

				if (index > 0)
					return new NeighborUnits(siblings[index - 1], siblings[index]);
			}

			candidate = candidate.Parent;
		}

		return null;
	}

	public readonly record struct NeighborUnits(SyntaxNode Previous, SyntaxNode Next);

	public readonly record struct TokenPair(SyntaxToken Previous, SyntaxToken Next);
}
