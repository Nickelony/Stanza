using System.Collections.Immutable;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>What the rules that keep a gap blank give a read of runs, so a gap they keep ends a run.</summary>
/// <remarks>
/// The constructor takes the owner IDs and the facts the run rules read with them, so one value carries a whole read.
/// </remarks>
internal readonly record struct GapOwners(
	string? MultiLineId,
	string? RegionMarkerId,
	string? ConditionalBlockId,
	string? CommentedSiblingId,
	ImmutableHashSet<int>? OwnedBoundaries = null,
	bool AwaitRuleEnabled = false,
	ImmutableHashSet<int>? BaseCallBoundaries = null
)
{
	/// <summary>Whether an owner keeps the boundary, as ZA0007 and ZA0010 do beside their units.</summary>
	public bool Owns(int boundary) => OwnedBoundaries?.Contains(boundary) == true || BaseCallBoundaries?.Contains(boundary) == true;
}
