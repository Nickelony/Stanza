using System.Text;

namespace Nickelony.Stanza.Markup.Files;

/// <summary>The encoding a document's bytes name, its mark's length, and what refuses a read or a write.</summary>
internal readonly record struct MarkupEncodingChoice(Encoding Encoding, int PreambleLength, string? WriteBackRefusal, string? Refusal);
