using System;

namespace Nickelony.Stanza.Shared;

internal static class PathComparison
{
	public static StringComparer Comparer { get; } =
		OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
