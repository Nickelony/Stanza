using System.Collections.Generic;

namespace Nickelony.Stanza.Analyzers.Tests.Bodies;

internal static class BodyFamilyFixtures
{
	internal static Dictionary<string, string> MemberPart(string value) =>
		new() { ["stanza_multi_line_member_part"] = value };

	internal static Dictionary<string, string> Width(int columns) =>
		new() { ["stanza_max_line_length"] = $"{columns}" };
}
