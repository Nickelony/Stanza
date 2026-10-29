namespace Nickelony.Stanza.Analyzers;

/// <summary>Names how a code fix repairs the gap a rule reports.</summary>
internal enum GapFix
{
	/// <summary>The rule reports no gap the blank line fix repairs; its own provider fixes it.</summary>
	None,
	SiblingInsertion,
	MarkupInsertion,
	TokenInsertion,
	AdjacentRemoval,
	RunRemoval,
	DeclarationGapRemoval,
	MarkupCapRemoval,
	MarkupExtraRemoval,
}
