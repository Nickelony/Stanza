namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>
/// The IDs of ZA0005, ZA0047 and ZA0053 while each is on, so a gap that one of them keeps blank ends a grouping run.
/// </summary>
internal readonly record struct GapOwners(string? MultiLineId, string? RegionMarkerId, string? CommentedSiblingId);
