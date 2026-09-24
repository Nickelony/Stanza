namespace Nickelony.Stanza.Markup.Core;

internal static class MarkupLimits
{
	// A larger value is invalid, so a typo cannot make the layout allocate or loop without bound.

	public const int MaxCount = 1000;
	public const int MaxIndentColumns = 64;

	/// <summary>
	/// The default bound on a document's length in characters; a bound below one leaves the length unchecked.
	/// </summary>
	public const long DefaultMaxDocumentCharacters = 64L * 1024L * 1024L;

	/// <summary>The default bound on a document's elements, which cost a few hundred bytes each in the tree.</summary>
	public const int DefaultMaxElements = 1024 * 1024;
}
