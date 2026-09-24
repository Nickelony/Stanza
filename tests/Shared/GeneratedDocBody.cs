using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Nickelony.Stanza.Tests.Shared;

/// <summary>Builds the damaged body of a documentation comment from a seed.</summary>
internal static partial class GeneratedDocBody
{
	private static readonly string[] s_words =
	[
		"alpha",
		"beta",
		"gamma",
		"delta",
		"reads",
		"writes",
		"returns",
		"value",
		"count",
		"index",
		"buffer",
		"token",
		"stream",
		"limit",
	];

	/// <summary>Returns the lines of one body, its elements in a random order and form.</summary>
	public static IReadOnlyList<string> Create(Random random, IReadOnlyList<string> parameters)
	{
		List<IReadOnlyList<string>> elements = [Element(random, "summary", string.Empty)];

		if (random.Next(2) == 0)
			elements.Add(Remarks(random));

		if (random.Next(3) != 0)
			elements.Add(Element(random, "returns", string.Empty));

		foreach (string parameter in parameters.Where(_ => random.Next(10) < 7))
			elements.Add(Element(random, "param", $" name=\"{parameter}\""));

		List<string> lines = [];

		foreach (IReadOnlyList<string> element in elements.OrderBy(_ => random.Next()))
		{
			lines.AddRange(element);

			if (random.Next(5) == 0)
				lines.Add(string.Empty);
		}

		return lines;
	}

	/// <summary>Returns the words of a body in order, without any tag.</summary>
	public static string[] ReadWords(IEnumerable<string> lines)
	{
		string text = string.Join(" ", lines);
		return [.. Words().Matches(Tags().Replace(text, " ")).Select(match => match.Value)];
	}

	private static List<string> Remarks(Random random)
	{
		List<string> lines = ["<remarks>"];

		lines.Add(Text(random));

		if (random.Next(2) == 0)
			lines.Add($"<para>{Text(random)}</para>");

		if (random.Next(2) == 0)
			lines.AddRange(BulletList(random));

		lines.Add("</remarks>");

		return lines;
	}

	private static IReadOnlyList<string> BulletList(Random random)
	{
		bool isDamaged = random.Next(2) == 0;

		string term = $"<term>{Pick(random)}</term>";
		string description = $"<description>{Text(random)}</description>";

		return
		[
			"<list type=\"bullet\">",
			$"<item>{(isDamaged ? description + term : term + description)}</item>",
			$"<item><description>{Text(random)}</description></item>",
			"</list>",
		];
	}

	private static IReadOnlyList<string> Element(Random random, string name, string attributes)
	{
		string text = Text(random);
		string padding = random.Next(4) == 0 ? " " : string.Empty;

		return random.Next(3) switch
		{
			0 => [$"<{name}{attributes}>{padding}{text}{padding}</{name}>"],
			1 => [$"<{name}{attributes}>", text, $"</{name}>"],
			_ => [$"<{name}{attributes}>{text}", $"</{name}>"],
		};
	}

	private static string Text(Random random)
	{
		List<string> words = [.. Enumerable.Range(0, random.Next(1, 36)).Select(_ => Pick(random))];

		// An inline element inside the text, in the forms a writer may leave it.
		switch (random.Next(5))
		{
			case 0:
				words.Insert(random.Next(words.Count), "<c>token</c>");
				break;

			case 1:
				words.Insert(random.Next(words.Count), "<see cref=\"Sample\" />");
				break;

			case 2:
				words.Insert(random.Next(words.Count), "<see cref=\"Sample\"/>");
				break;
		}

		string text = string.Join(" ", words);

		return random.Next(2) == 0 ? text + "." : text;
	}

	private static string Pick(Random random) => s_words[random.Next(s_words.Length)];

	[GeneratedRegex("<[^>]*>")]
	private static partial Regex Tags();

	[GeneratedRegex("[A-Za-z0-9]+")]
	private static partial Regex Words();
}
