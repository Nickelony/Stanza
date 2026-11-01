namespace Nickelony.Stanza.Format.Planning;

/// <summary>The options of a run; a <see langword="null"/> layout leaves the choice to the manifest.</summary>
/// <remarks>
/// A ref in <paramref name="Since"/> scopes the run, a check too, to the files changed from that ref.
/// </remarks>
internal sealed record FormatOptions(bool All, bool Check, string Path, FormatLayout? Layout, string? Since = null);
