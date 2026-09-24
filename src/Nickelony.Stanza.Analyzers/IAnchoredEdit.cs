using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers;

/// <summary>A change with the span a diagnostic reports it at, so a fix finds its change among a file's.</summary>
internal interface IAnchoredEdit
{
	TextSpan Anchor { get; }
	TextChange Change { get; }
}
