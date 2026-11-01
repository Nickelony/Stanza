using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Regions;

/// <summary>One change at a region or conditional directive, with its report span and its paired directive.</summary>
internal readonly record struct RegionEdit(TextChange Change, TextSpan Anchor, TextSpan? Neighbor) : IAnchoredEdit;
