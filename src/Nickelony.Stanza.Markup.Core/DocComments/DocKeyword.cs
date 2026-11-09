namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>A language keyword found in prose: where its text starts, and the keyword in lowercase.</summary>
internal readonly record struct DocKeyword(int Offset, string Word);
