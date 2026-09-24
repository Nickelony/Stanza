using System.Collections.Generic;

namespace Nickelony.Stanza.Analyzers.Tests;

internal static class BodyFamilyFixtures
{
	internal static Dictionary<string, string> Trigger(string value) =>
		new() { ["stanza_multiline_body_trigger"] = value };

	internal static Dictionary<string, string> Width(int columns) =>
		new() { ["stanza_max_line_length"] = $"{columns}" };
}
