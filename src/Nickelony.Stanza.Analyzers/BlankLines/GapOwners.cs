using System;
using System.Collections.Immutable;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>What the rules that keep a gap blank give a read of runs, so a gap they keep ends a run.</summary>
/// <remarks>
/// The constructor takes the owner IDs and the facts the run rules read with them, so one value carries a whole read.
/// </remarks>
internal readonly record struct GapOwners(
	string? MultiLineId,
	string? CommentedSiblingId,
	ImmutableArray<GapOwners.DirectiveOwner> DirectiveOwners,
	ImmutableHashSet<int>? OwnedBoundaries = null,
	bool AwaitRuleEnabled = false,
	bool YieldRuleEnabled = false,
	bool LocalFunctionRuleEnabled = false,
	ImmutableHashSet<int>? BaseCallBoundaries = null,
	ImmutableHashSet<int>? CheckedBoundaries = null,
	ImmutableHashSet<int>? ReceiverBoundaries = null,
	Func<int, bool>? ReadsReceiverAt = null,
	Func<int, bool>? ReceiverBarrier = null,
	ImmutableHashSet<int>? JoinedBoundaries = null,
	ImmutableHashSet<int>? ProtectedSections = null,
	ImmutableHashSet<int>? DisplacedBoundaries = null
)
{
	/// <summary>Whether an owner keeps the boundary, as ZA0008 and ZA0014 do beside their units.</summary>
	/// <remarks>
	/// A gap inside a kept group is joined, so no owner keeps it.
	/// </remarks>
	public bool Owns(int boundary)
	{
		if (IsJoined(boundary))
			return false;

		bool grouped =
			BaseCallBoundaries?.Contains(boundary) == true
			|| CheckedBoundaries?.Contains(boundary) == true
			|| ReceiverBoundaries?.Contains(boundary) == true
			|| ProtectedSections?.Contains(boundary) == true
			|| DisplacedBoundaries?.Contains(boundary) == true;

		return OwnedBoundaries?.Contains(boundary) == true || grouped || ReceiverBarrier?.Invoke(boundary) == true;
	}

	/// <summary>Whether a keeper joins the gap, so a read of runs takes it as tight whatever stands there.</summary>
	public bool IsJoined(int boundary) => JoinedBoundaries?.Contains(boundary) == true;

	/// <summary>A directive rule that is on, and whether its option pads inside its blocks.</summary>
	public readonly record struct DirectiveOwner(DirectiveBlock Block, bool Pads);
}
