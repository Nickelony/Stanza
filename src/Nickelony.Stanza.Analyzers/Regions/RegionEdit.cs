using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Regions;

/// <summary>One change to a region directive, with the span it reports at and the directive it pairs with.</summary>
internal readonly record struct RegionEdit(TextChange Change, TextSpan Anchor, TextSpan? Neighbor) : IAnchoredEdit;
