using System.IO;

namespace Nickelony.Stanza.Shared;

internal static class RepositoryPath
{
	public static string ToRelative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

	/// <summary>The part of a <c>/</c>-separated path before its last slash, or empty when it has none.</summary>
	public static string GetDirectory(string path)
	{
		int slash = path.LastIndexOf('/');
		return slash < 0 ? string.Empty : path[..slash];
	}
}
