using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Commas;

/// <summary>One change to the comma after the last element of a list, with the span it reports at.</summary>
internal readonly record struct CommaEdit(TextChange Change, TextSpan Anchor) : IAnchoredEdit;
