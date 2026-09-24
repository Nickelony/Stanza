namespace Nickelony.Stanza.Analyzers.Documentation;

internal readonly struct DocCommentDelimiters(string marker, string opening, string closing)
{
	public static DocCommentDelimiters Lines { get; } = new(marker: "///", opening: string.Empty, closing: string.Empty);
	public static DocCommentDelimiters Block { get; } = new(marker: " *", opening: "/**", closing: " */");

	public string Marker { get; } = marker;
	public string Opening { get; } = opening;
	public string Closing { get; } = closing;

	/// <summary>Whether the comment is a <c>/** */</c> block, which opens and closes on lines of its own.</summary>
	public bool IsBlock => Opening.Length > 0;

	/// <summary>The columns a line's marker and the space after it take before the text.</summary>
	public int MarkerWidth => Marker.Length + 1;

	/// <summary>Writes a line of text after its marker, or the marker alone when the line is blank.</summary>
	public static string BuildLine(string marker, string content) => content.Length == 0 ? marker : marker + " " + content;
}
