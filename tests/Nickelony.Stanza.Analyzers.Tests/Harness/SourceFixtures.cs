using System.Linq;

namespace Nickelony.Stanza.Analyzers.Tests.Harness;

/// <summary>The helpers that build a sample source around a body.</summary>
internal static class SourceFixtures
{
	/// <summary>Wraps a body in a method, leaving blank and directive lines unindented.</summary>
	// A directive stays in the first column, and the text before and after the method comes from the caller.
	internal static string WrapBody(
		string body,
		string signature,
		string classHeader = "class C",
		string members = "",
		string after = ""
	)
	{
		string indented = string.Join(
			"\n",

			body.ReplaceLineEndings("\n")
				.Split('\n')
				.Select(line => line.Trim().Length == 0 || line[0] == '#' ? line : "\t\t" + line)
		);

		return $"{classHeader}\n{{\n{members}\t{signature}\n\t{{\n{indented}\n\t}}\n{after}}}\n";
	}

	/// <summary>Indents each line that holds text by one level.</summary>
	internal static string Indent(string text)
	{
		return string.Join(
			"\n",
			text.Split('\n').Select(static line => line.Length == 0 ? line : "\t" + line)
		);
	}
}
