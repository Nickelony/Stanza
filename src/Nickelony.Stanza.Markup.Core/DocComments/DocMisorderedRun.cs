namespace Nickelony.Stanza.Markup.Core.DocComments;

/// <summary>A run of ranked entries out of order, with the order that sorts it and the first entry it moves.</summary>
internal readonly record struct DocMisorderedRun(int Start, int End, int[] Arrangement, int Misplaced);
