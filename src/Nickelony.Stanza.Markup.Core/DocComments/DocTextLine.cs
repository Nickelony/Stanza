namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>A run of text, or a line feed when <see langword="null"/>, with its offset in the text.</summary>
internal readonly record struct DocTextLine(string? Text, int Offset);
