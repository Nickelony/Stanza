namespace Nickelony.Stanza.Shared;

/// <summary>Writes a path a tool passes to another program so the program never reads it as an option.</summary>
internal static class ArgumentPath
{
	/// <summary>Starts a path with <c>./</c>, as a name that starts with <c>@</c> is read as a response file.</summary>
	public static string ToPrefixed(string path) => $"./{path}";

	/// <summary>Protects a target that starts with <c>@</c> or <c>-</c>, and leaves any other as it is.</summary>
	public static string ToProtected(string path) => path.StartsWith('@') || path.StartsWith('-') ? ToPrefixed(path) : path;
}
