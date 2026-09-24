namespace Nickelony.Stanza.Format.Planning;

/// <summary>The options of a run; a <see langword="null"/> layout leaves the choice to the manifest.</summary>
internal sealed record FormatOptions(bool All, bool Check, string Path, FormatLayout? Layout);
