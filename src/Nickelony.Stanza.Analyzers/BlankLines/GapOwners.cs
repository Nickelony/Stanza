namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>
/// The IDs of ZA0004 and ZA0046 while each is on, so a gap that either rule keeps blank ends a grouping run.
/// </summary>
internal readonly record struct GapOwners(string? MultiLineId, string? RegionMarkerId);
