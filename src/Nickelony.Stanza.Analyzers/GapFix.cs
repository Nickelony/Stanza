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

	/// <summary>Cuts a markup gap back to the cap, and has no count to fall back on.</summary>
	MarkupSurplusRemoval,

	/// <summary>Removes the span the rule reports, which holds only the blank lines past the cap.</summary>
	TriviaRunRemoval,
}
