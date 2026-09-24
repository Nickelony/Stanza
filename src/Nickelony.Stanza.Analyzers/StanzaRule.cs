using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nickelony.Stanza.Analyzers;

/// <summary>One rule, with the node kinds it judges, its fix, its pair and the rules it defers to.</summary>
/// <remarks>
/// It defers only while that rule is on, and an anchored removal stops at the next unit's first leading comment.
/// </remarks>
internal sealed record StanzaRule(
	DiagnosticDescriptor Descriptor,
	GapFix Fix = GapFix.None,
	ImmutableArray<SyntaxKind> Kinds = default,
	GapPair? Pair = null,
	bool AnchoredOnPrevious = false,
	ImmutableArray<DiagnosticDescriptor> DefersTo = default
)
{
	public bool Defers(DiagnosticDescriptor target) => !DefersTo.IsDefault && DefersTo.Contains(target);

	/// <summary>Gets the entry of a rule this one defers to, or throws when its own entry does not record it.</summary>
	public StanzaRule GetDeferral(DiagnosticDescriptor target)
	{
		return Defers(target)
			? StanzaRules.Get(target)
			: throw new InvalidOperationException($"{Descriptor.Id} defers to {target.Id}, which its StanzaRules entry does not record.");
	}
}
