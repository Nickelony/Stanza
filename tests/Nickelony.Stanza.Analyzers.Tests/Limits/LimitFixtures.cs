using System.Linq;
using Microsoft.CodeAnalysis;

namespace Nickelony.Stanza.Analyzers.Tests.Limits;

/// <summary>The helpers that the fixtures of the limit rules share.</summary>
internal static class LimitFixtures
{
	/// <summary>The source text that a report covers.</summary>
	internal static string Text(Diagnostic diagnostic) =>
		diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

	/// <summary>Wraps statements in a method of a class, one level in.</summary>
	internal static string Wrap(
		string statements,
		string signature = "internal static void Run(int n)",
		string classHeader = "internal static class Sample",
		string members = "",
		string tail = ""
	)
	{
		string body = string.Join(
			"\n",

			statements
				.ReplaceLineEndings("\n")
				.Split('\n')
				.Select(line => line.Length == 0 ? line : "\t\t" + line)
		);

		return $"{classHeader}\n{{\n{members}\t{signature}\n\t{{\n{body}\n{tail}\t}}\n}}\n";
	}
}
