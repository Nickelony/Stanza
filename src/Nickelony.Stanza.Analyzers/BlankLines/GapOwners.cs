namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>
/// The IDs of ZA0005 and ZA0047 while each is on, so a gap that either rule keeps blank ends a grouping run.
/// </summary>
internal readonly record struct GapOwners(string? MultiLineId, string? RegionMarkerId);
