namespace Nickelony.Stanza.Analyzers.Braces;

/// <summary>Where moved comments land, and the step their later lines lose.</summary>
internal readonly record struct CommentPlacement(string LineEnding, string Indentation, string? DedentStep = null, bool SeparatesFromAnchor = false);
