using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>A set of sibling rules and the source its contract tests build.</summary>
/// <remarks>
/// The constructor takes one parameter for each part, since the one job is to hold them together.
/// </remarks>
public sealed record SiblingRuleSet(
	string Name,
	DiagnosticDescriptor Separate,
	DiagnosticDescriptor KeepMultiLine,
	DiagnosticDescriptor KeepSingleLine,
	string Frame,
	string Container,
	string Separator,
	ImmutableArray<string> MultiLine,
	ImmutableArray<string> SingleLine
)
{
	public const string Tight = "\n";

	public const string Blank = "\n\n";

	public const string CommentTravels = "\n\n// why\n";

	public const string CommentTravelsFixed = "\n// why\n";

	public const string CommentBlank = "\n\n// why\n\n";

	public const string TwoNotes = "\n\n// first note\n\n// second note\n";

	public const string Directive = "\n#if STANZA_UNDEFINED\n\n#endif\n";

	/// <summary>Builds a frame holding one container for each set of units and gap.</summary>
	public string Build(params (ImmutableArray<string> Units, string Gap)[] containers)
	{
		string[] rendered =
		[
			.. containers.Select(
				(container, index) => Fill(Container, Join(container.Units, container.Gap), index)
			),
		];

		string source = Fill(Frame, string.Join("\n\n", rendered), index: 0);

		// The harness runs a second form of each source in the other ending, so a source with a carriage return would test neither.
		Assert.DoesNotContain("\r", source, $"{Name} builds a source with a carriage return.");

		return source;
	}

	public override string ToString() => Name;

	private string Join(ImmutableArray<string> units, string gap)
	{
		StringBuilder builder = new(units[0]);

		for (int index = 1; index < units.Length; index++)
			builder.Append(Separator).Append(gap).Append(units[index]);

		return builder.ToString();
	}

	// The frame and the container each hold one `@@` marker, where the next level goes.
	// A `{n}` becomes the container's number, so containers can share a method.
	// Each later line of the text takes the indentation of the marker's line.
	// A template takes the ending of its checkout and the units use a line feed, so the template is read with one too.
	private static string Fill(string template, string text, int index)
	{
		string[] lines = template
			.Replace("\r\n", "\n", StringComparison.Ordinal)
			.Replace("{n}", index.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
			.Split('\n');

		int at = Array.FindIndex(lines, line => line.Contains("@@", StringComparison.Ordinal));
		string line = lines[at];
		int marker = line.IndexOf("@@", StringComparison.Ordinal);
		string indent = line[..(line.Length - line.TrimStart('\t').Length)];

		string[] inserted = text.Replace(
				"{n}",
				index.ToString(CultureInfo.InvariantCulture),
				StringComparison.Ordinal
			)
			.Split('\n');

		lines[at] =
			line[..marker]
			+ inserted[0]
			+ string.Concat(
				inserted.Skip(1).Select(next => "\n" + (next.Length == 0 ? next : indent + next))
			)
			+ line[(marker + 2)..];

		return string.Join("\n", lines);
	}
}
