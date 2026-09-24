using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Nickelony.Stanza.Markup.Core.Xml;
using Nickelony.Stanza.Shared;

namespace Nickelony.Stanza.Markup.Configuration;

internal static class MarkupProfiles
{
	public static readonly MarkupProfile Xml = new("xml", MarkupFileExtensions.Xml, new MarkupPolicy(ContainersOnly: false));
	public static readonly MarkupProfile MsBuild = new("msbuild", MarkupFileExtensions.MsBuild, new MarkupPolicy(ContainersOnly: true));
	public static readonly MarkupProfile Slnx = new("slnx", MarkupFileExtensions.Slnx, new MarkupPolicy(ContainersOnly: true));

	public static readonly MarkupProfile Xaml = new(
		"xaml",
		MarkupFileExtensions.Xaml,
		new MarkupPolicy(ContainersOnly: true, SkipsPropertyElements: true)
	);

	public static readonly IReadOnlyList<MarkupProfile> All = [Xml, MsBuild, Slnx, Xaml];

	public static readonly string Summary = BuildSummary();

	public static MarkupProfile? FindByName(string name)
	{
		foreach (MarkupProfile profile in All)
		{
			if (string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
				return profile;
		}

		return null;
	}

	public static MarkupProfile? FindByPath(string path)
	{
		string extension = Path.GetExtension(path);

		foreach (MarkupProfile profile in All)
		{
			if (profile.ClaimsExtension(extension))
				return profile;
		}

		return null;
	}

	private static string BuildSummary()
	{
		StringBuilder builder = new();

		foreach (MarkupProfile profile in All)
		{
			if (builder.Length > 0)
				builder.Append(", ");

			builder.Append(profile.Name);
			builder.Append(" (");
			builder.Append(string.Join(", ", profile.Extensions));
			builder.Append(')');
		}

		return builder.ToString();
	}
}
