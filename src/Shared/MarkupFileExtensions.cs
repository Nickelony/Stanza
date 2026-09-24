using System;
using System.Collections.Immutable;
using System.IO;

namespace Nickelony.Stanza.Shared;

/// <summary>The extensions each markup profile claims, so both tools agree on which files are markup.</summary>
internal static class MarkupFileExtensions
{
	public static readonly ImmutableArray<string> Xml = [".xml", ".config"];

	public static readonly ImmutableArray<string> MsBuild =
	[
		".csproj",
		".vbproj",
		".fsproj",
		".vcxproj",
		".props",
		".targets",
		".proj",
		".projitems",
		".shproj",
	];

	public static readonly ImmutableArray<string> Slnx = [".slnx"];
	public static readonly ImmutableArray<string> Xaml = [".xaml", ".axaml"];

	private static readonly ImmutableHashSet<string> s_all = ImmutableHashSet.CreateRange(
		StringComparer.OrdinalIgnoreCase,
		[.. Xml, .. MsBuild, .. Slnx, .. Xaml]
	);

	/// <summary>Whether a profile claims the extension of the path, whatever its case.</summary>
	public static bool IsMarkup(string path) => s_all.Contains(Path.GetExtension(path));
}
