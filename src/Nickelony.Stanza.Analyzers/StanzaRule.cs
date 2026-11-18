using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nickelony.Stanza.Analyzers;

/// <summary>One rule, with its node kinds, its fix, its pair, the rules it defers to and the rules it reads.</summary>
/// <remarks>
/// It defers only while that rule is on, and an anchored removal stops at the next unit's first leading comment.
/// </remarks>
internal sealed record StanzaRule(
	DiagnosticDescriptor Descriptor,
	GapFix Fix = GapFix.None,
	ImmutableArray<SyntaxKind> Kinds = default,
	RulePair? Pair = null,
	bool AnchoredOnPrevious = false,
	ImmutableArray<DiagnosticDescriptor> DefersTo = default,
	ImmutableArray<DiagnosticDescriptor> Reads = default
)
{
	/// <summary>Whether the rule defers to the target, or throws when its entry only reads it.</summary>
	public bool Defers(DiagnosticDescriptor target)
	{
		if (Lists(DefersTo, target))
			return true;

		return Lists(Reads, target)
			? throw new InvalidOperationException($"{Descriptor.Id} only reads {target.Id}, so no deferral to it may be asked.")
			: false;
	}

	/// <summary>Whether the rule reads the target, as a deferral to it does too.</summary>
	public bool ReadsStateOf(DiagnosticDescriptor target) => Lists(DefersTo, target) || Lists(Reads, target);

	/// <summary>Gets the entry of a rule this one defers to, or throws when its own entry does not record it.</summary>
	public StanzaRule GetDeferral(DiagnosticDescriptor target)
	{
		return Defers(target)
			? StanzaRules.Get(target)
			: throw new InvalidOperationException($"{Descriptor.Id} defers to {target.Id}, which its StanzaRules entry does not record.");
	}

	/// <summary>Gets the entry of a rule this one reads, or throws when its own entry does not record it.</summary>
	public StanzaRule GetReadRule(DiagnosticDescriptor target)
	{
		return ReadsStateOf(target)
			? StanzaRules.Get(target)
			: throw new InvalidOperationException($"{Descriptor.Id} reads {target.Id}, which its StanzaRules entry does not record.");
	}

	private static bool Lists(ImmutableArray<DiagnosticDescriptor> rules, DiagnosticDescriptor target) => !rules.IsDefault && rules.Contains(target);
}
