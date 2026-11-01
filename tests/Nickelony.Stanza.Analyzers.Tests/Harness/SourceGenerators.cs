using System;
using System.Collections.Generic;
using System.Linq;

namespace Nickelony.Stanza.Analyzers.Tests.Harness;

/// <summary>Draws the C# sources the seeded fuzz and soak tests fix.</summary>
internal static class SourceGenerators
{
	// A comment above a single-line unit meets the commented sibling rule in every container it judges.
	// Each template is one statement, written without its indent, and carries loose runs the loose run rule gathers.
	internal static (string Name, Func<int, string[]> Template)[] CommentedSiblings { get; } =
	[
		(
			"statements of a switch section",

			index =>
				[
					"switch (loose)",
					"{",
					"\tcase 1:",
					"\t\tDoThing();",
					$"\t\t// Note {index}.",
					"\t\tDoThing();",
					"",
					"\t\tloose = 1;",
					"",
					"\t\tloose = 2;",
					"\t\tbreak;",
					"\tdefault:",
					"\t\tbreak;",
					"}",
				]
		),

		(
			"single-line switch sections",

			index =>
				[
					"switch (loose)",
					"{",
					"\tcase 1: break;",
					$"\t// Section note {index}.",
					"\tcase 2: break;",
					"\tcase 3: break;",
					"\t// Default note.",
					"\tdefault: break;",
					"}",
				]
		),

		(
			"statements of a lambda block body",

			index =>
				[
					$"Action lambda{index} = () =>",
					"{",
					"\tDoThing();",
					$"\t// Note {index}.",
					"\tDoThing();",
					"",
					"\tloose = 1;",
					"",
					"\tloose = 2;",
					"};",
				]
		),

		(
			"statements of a lambda block body passed as an argument",

			index =>
				[
					"new List<int>().ForEach(item =>",
					"{",
					"\tBuild(item);",
					$"\t// Note {index}.",
					"\tBuild(item);",
					"});",
				]
		),

		(
			"members of an initializer",

			index =>
				[
					$"var initialized{index} = new Item",
					"{",
					"\tName = \"a\",",
					$"\t// Count {index}.",
					"\tCount = 1, // Trailing.",
					"\t// Below the trailing comment.",
					"\tTags = [],",
					"};",
				]
		),

		(
			"elements of a collection expression",

			index =>
				[
					$"int[] elements{index} =",
					"[",
					"\t1,",
					$"\t// Two {index}.",
					"\t2,",
					"\t3,",
					"\t// Four.",
					"\t4,",
					"];",
				]
		),

		("arguments of a call", index => ["Call(", "\t1,", $"\t// Second {index}.", "\t2);"]),

		(
			"elements of a tuple literal",
			index => [$"var pair{index} = (", "\t1,", $"\t// Second {index}.", "\t2);"]
		),

		(
			"elements of a tuple type",

			index =>
				[
					"(",
					"\tint First,",
					$"\t// Second {index}.",
					$"\tint Second) typed{index} = default;",
				]
		),
	];

	private static readonly string[] s_importLines = ["using System.Linq;", "using System.Text;"];

	// Each template is one statement, written without its indent.
	// They mix the kinds the grouping rules read, with comments, directives and multi-line statements among them.
	internal static Func<int, string[]>[] Statements { get; } =
	[
		index => [$"var variable{index} = {index};"],
		index => [$"int number{index} = {index};"],
		index => [$"int count{index} = {index};"],
		index => [$"string text{index} = \"{index}\";"],
		index => [$"const int constant{index} = {index};"],
		index => [$"using var resource{index} = Open();"],
		index => [$"loose = {index};"],
		index => [$"loose += {index};"],
		index => ["loose++;"],
		index => ["(loose, loose) = Read();"],
		index => ["_ = loose;"],
		index => [$"Store.Field = {index};"],
		index => ["Store.Field++;"],
		index => [$"Store.Property = {index};"],
		index => [$"Store.Items[0] = {index};"],
		index => ["(loose, Store.Field) = Read();"],
		index => ["Events.Changed += DoThing;"],
		index => ["Events.Closed += DoThing;"],
		index => ["Events.Changed -= DoThing;"],
		index => ["DoThing();"],
		index => [$"Build({index});"],
		index => ["Assert.That(1);"],
		index => [$"var (left{index}, right{index}) = Read();"],
		index => ["_ = Read();"],
		index => ["await Load().ConfigureAwait(false);"],
		index => ["await Save().ConfigureAwait(false);"],
		index => [$"await Load({index});"],
		index => ["_ = await Save();"],
		index => ["First().ToArray();"],
		index => ["Second().ToArray();"],
		index => [$"// Note {index}.", $"loose = {index};"],
		index => [$"DoThing(); // Trailing {index}."],
		index => ["Call(", "\t1,", "\t2);"],
		index => [$"#region Group{index}", "DoThing();", "#endregion"],
		index => ["if (loose > 0) DoThing();"],
		index => [$"void Local{index}() {{ }}"],
		index => ["return;"],
		index => [$"throw new System.InvalidOperationException(\"{index}\");"],
		index => ["if (loose > 0)", "{", "\tDoThing();", "}", "return;"],

		index =>
			[
				"while (loose > 1)",
				"{",
				"\tif (loose < 5) continue;",
				"\tloose--;",
				"\tloose--;",
				"}",
			],

		index =>
			[
				"if (loose > 7)",
				"{",
				"\tif (loose < 5) return;",
				"",
				"\tloose = 8;",
				"",
				"\tloose = 9;",
				"}",
			],

		index =>
			[
				$"switch (loose + {index})",
				"{",
				"\tcase 1:",
				"\t\tloose = 1;",
				"\t\tloose = 2;",
				"\t\treturn;",
				"\tdefault:",
				"\t\tbreak;",
				"}",
			],

		// A block wholly inside a conditional holds its units in the view that takes it and none in another.
		index =>
			[
				"{",
				"#if !STANZA_SOAK",
				$"\tloose = {index};",
				"",
				"\tDoThing();",
				"",
				"\tloose++;",
				"#endif",
				"}",
			],

		// A conditional above the next switch section, and one among the labels, move no statement of a section.
		index =>
			[
				$"switch (loose + {index})",
				"{",
				"\tcase 1:",
				"\t\tloose = 1;",
				"",
				"\t\tloose = 2;",
				"",
				"\t\tbreak;",
				"#if !STANZA_SOAK",
				"\tcase 2:",
				"\t\tloose = 3;",
				"\t\tbreak;",
				"#endif",
				"\tcase 3:",
				"#if !STANZA_SOAK",
				"\tcase 4:",
				"#endif",
				"\t\tloose = 4;",
				"",
				"\t\tloose = 5;",
				"",
				"\t\tbreak;",
				"}",
			],

		.. CommentedSiblings.Select(static sibling => sibling.Template),
	];

	// Each template is one guard, written without its indent, for a method that opens with a few of them.
	private static readonly Func<int, string[]>[] s_guards =
	[
		index => [$"if (loose < {index}) return;"],
		index => [$"if (loose < {index})", "\treturn;"],
		index => [$"if (loose > {index}) {{ return; }}"],
		index => ["if (loose > 100)", "{", "\tthrow new System.InvalidOperationException();", "}"],
		index => [$"if (loose > {index}) throw new System.ArgumentException(\"loose\");"],
		index => ["ArgumentNullException.ThrowIfNull(items);"],
		index => ["ArgumentOutOfRangeException.ThrowIfNegative(loose);"],
		index => ["Guard.NotNull(items);"],
		index => ["Guard.Positive(loose);"],
		index => [$"// Why {index}.", $"if (loose < {index}) return;"],
		index => [$"if (loose < {index}) return; // Trailing {index}."],
		index => ["#if !STANZA_SOAK", $"if (loose < {index}) return;", "#endif"],
		index => ["if (loose > 3)", "{", "\t// Why.", "\treturn;", "}"],
	];

	// Each template is one base call, written without its indent, for a method that opens or closes with one.
	// A call of DoThing or Load shares its name with a statement, so a run of calls meets the gap beside it.
	private static readonly Func<int, string[]>[] s_baseCalls =
	[
		index => ["base.OnStart();"],
		index => ["base.DoThing();"],
		index => [$"await base.Load({index});"],
		index => ["_ = base.Fetch();"],
		index => ["base.Items.Clear();"],
		index => [$"// Base {index}.", "base.OnStop();"],
	];

	// Each template is one member that a run of loose members is made of or is broken by, written without its indent.
	private static readonly Func<int, string[]>[] s_looseMembers =
	[
		index => [$"private int _field{index};"],
		index => [$"private string _text{index} = \"{index}\";"],
		index => [$"private const int Constant{index} = {index};"],
		index => [$"private static readonly int s_static{index} = {index};"],
		index => [$"public int Property{index} {{ get; set; }}"],
		index => [$"public int Expression{index}() => {index};"],
		index => [$"public void Empty{index}() {{ }}"],
		index => [$"public delegate void Handler{index}();"],
		index => [$"[System.Obsolete] public int Attributed{index};"],
		index => ["[System.Obsolete]", $"public int OwnLine{index};"],
		index => [$"/// <summary>Documented {index}.</summary>", $"public int Documented{index};"],
		index => [$"// Note {index}.", $"public int Noted{index};"],
		index => [$"private int _trailing{index}; // Trailing {index}."],
		index => [$"public void Multi{index}()", "{", "\tDoThing();", "}"],
		index => [$"#region Group{index}", $"private int _grouped{index};", "#endregion"],

		index =>
			[
				$"public enum Kind{index}",
				"{",
				"\tFirst,",
				"",
				"\tSecond, // Trailing.",
				"",
				"\t[System.Obsolete]",
				"\tThird,",
				"",
				"\tFourth,",
				"}",
			],

		// A type whose members all stand inside a conditional holds them in the view that takes it and none in another.
		index =>
			[
				$"public class Wrapped{index}",
				"{",
				"#if !STANZA_SOAK",
				"\tprivate int _first;",
				"",
				"\tprivate int _second;",
				"",
				"\tprivate int _third;",
				"#endif",
				"}",
			],

		index => ["#if !STANZA_SOAK", $"private int _enabled{index};", "#endif"],
	];

	// Each template is one member, written without its indent. None of them needs more than the shared support code.
	private static readonly Func<int, string[]>[] s_members =
	[
		index => [$"private int _field{index};"],
		index => [$"public int Property{index} {{ get; set; }}"],
		index => [$"private int _trailing{index}; // trailing {index}"],
		index => [$"/* block {index} */", $"private int _blocked{index};"],
		index => [$"public void Empty{index}()", "{", "}"],
		index => [$"// note {index}", $"public int Noted{index}() => 1;"],
		index => [$"#region Group{index}", $"private int _grouped{index};", "#endregion"],

		index =>
			[$"public int Accessors{index}", "{", "\tget { return 1; }", "\tset { return; }", "}"],

		index =>
			[
				$"public Item Initializer{index}() => new Item",
				"{",
				"\tName = \"a\",",
				"\tCount = 1,",
				"};",
			],

		index =>
			[
				$"public Item Commented{index}() => new Item",
				"{",
				"\tName = \"a\",",
				"\t// The tags stay empty.",
				"\tTags = [],",
				"",
				"\t// The count is one.",
				"\tCount = 1,",
				"};",
			],

		index =>
			[
				$"public void Commented{index}()",
				"{",
				"\tDoThing();",
				"\t// The next step.",
				"\tDoThing();",
				"",
				"\t// The last step.",
				"\treturn;",
				"}",
			],

		index => [$"public List<int> Elements{index}() => [", "\t1,", "\t2,", "];"],

		index =>
			[
				$"public int Arms{index}(int value) => value switch",
				"{",
				"\t1 => 1,",
				"\t2 => 2,",
				"\t_ => 0,",
				"};",
			],

		index =>
			[
				$"public void Statements{index}()",
				"{",
				"\tvar first = 1;",
				"\tDoThing();",
				"\tDoThing();",
				"\treturn;",
				"}",
			],

		index =>
			[
				$"public void Calls{index}()",
				"{",
				"\tCall(",
				"\t\t1,",
				"\t\t2);",
				"\tBuild(",
				"\t\t3);",
				"}",
			],

		index =>
			[
				$"public int Switch{index}(int value)",
				"{",
				"\tswitch (value)",
				"\t{",
				"\t\tcase 1:",
				"\t\t\treturn 1;",
				"\t\tcase 2:",
				"\t\t\treturn 2;",
				"\t\tdefault:",
				"\t\t\treturn 0;",
				"\t}",
				"}",
			],

		index => [$"public sealed record Pair{index}(int First, int Second);"],

		index =>
			[
				$"public sealed record Named{index}(string Name)",
				"{",
				"\tpublic int Length { get; init; }",
				"\tpublic int Size() => 1;",
				"}",
			],

		index =>
			[
				$"public Func<int, int> Lambda{index}() => value =>",
				"{",
				"\tvar doubled = value * 2;",
				"\treturn doubled;",
				"};",
			],

		index =>
			[
				$"public int Lambdas{index}(List<int> values)",
				"{",
				"\tvar first = values.Select(value => value + 1);",
				"\tvar second = values.Where(value =>",
				"\t{",
				"\t\treturn value > 0;",
				"\t});",
				"\treturn first.Count() + second.Count();",
				"}",
			],

		index => ["#if !STANZA_SOAK", $"private int _enabled{index};", "#endif"],

		index =>
			[
				"#if STANZA_SOAK",
				$"public int Switched{index}() => 1;",
				"#else",
				$"public int Switched{index}() => 2;",
				"#endif",
			],
	];

	internal static string CreateStatementSource(Random random, int looseRun) =>
		WrapStatements(CreateStatementLines(random, looseRun));

	// The method takes the names its guards check as parameters, so its first statement is a guard.
	// A base call may open the statements below the guards and may close the method, as the base call rule judges.
	internal static string CreateGuardedSource(Random random, int looseRun)
	{
		List<string> lines = [];

		int guards = random.Next(1, 4);

		for (int index = 0; index < guards; index++)
		{
			if (index > 0)
				lines.AddRange(StatementGap(random));

			lines.AddRange(IndentStatement(s_guards[random.Next(s_guards.Length)](index)));
		}

		lines.AddRange(StatementGap(random));

		// The base calls are drawn after the statements, so drawing them leaves the guards and statements of a seed unchanged.
		List<string> statements = CreateStatementLines(random, looseRun);

		if (random.Next(2) == 0)
		{
			lines.AddRange(IndentStatement(s_baseCalls[random.Next(s_baseCalls.Length)](0)));
			lines.AddRange(StatementGap(random));
		}

		lines.AddRange(statements);

		if (random.Next(2) == 0)
		{
			lines.AddRange(StatementGap(random));
			lines.AddRange(IndentStatement(s_baseCalls[random.Next(s_baseCalls.Length)](1)));
		}

		return "internal sealed class Sample : Stage\n{\n\tpublic async System.Threading.Tasks.Task Run(object items, int loose)\n\t{\n"
			+ string.Join("\n", lines)
			+ "\n\t}\n}\n";
	}

	private static List<string> CreateStatementLines(Random random, int looseRun)
	{
		List<string> lines = [];

		int count = random.Next(6, 13);
		int runIndex = random.Next(count);

		for (int index = 0; index < count; index++)
		{
			if (index > 0)
				lines.AddRange(StatementGap(random));

			// One run of loose assignments, set apart on both sides, gives every seed a run to gather.
			if (index == runIndex)
			{
				if (index > 0 && lines[^1].Length > 0)
					lines.Add(string.Empty);

				for (int value = 0; value < looseRun; value++)
					lines.AddRange([$"\t\tloose = {value};", string.Empty]);
			}

			lines.AddRange(IndentStatement(Statements[random.Next(Statements.Length)](index)));
		}

		return lines;
	}

	internal static string CreateMemberSource(Random random, int looseRun)
	{
		List<string> lines = [];

		foreach (string import in s_importLines)
		{
			lines.AddRange(Gap(random));
			lines.Add(import);
		}

		lines.AddRange(Gap(random));

		lines.Add("internal sealed class Sample");
		lines.Add("{");

		lines.AddRange(CreateMemberLines(random, looseRun));

		lines.Add("}");

		return string.Join("\n", lines) + "\n";
	}

	// The statements come first and the types below them, as a file of top-level statements requires.
	internal static string CreateFileSource(Random random, int looseRun)
	{
		List<string> lines = [];

		foreach (string import in s_importLines)
		{
			lines.AddRange(Gap(random));
			lines.Add(import);
		}

		lines.AddRange(Gap(random));

		lines.Add("int loose = 0;");

		int count = random.Next(3, 8);
		int runIndex = random.Next(count);

		for (int index = 0; index < count; index++)
		{
			lines.AddRange(StatementGap(random));

			// One run of loose assignments, set apart on both sides, gives every seed a run to gather.
			if (index == runIndex)
			{
				if (lines[^1].Length > 0)
					lines.Add(string.Empty);

				for (int value = 0; value < looseRun; value++)
					lines.AddRange([$"loose = {value};", string.Empty]);
			}

			lines.AddRange(Statements[random.Next(Statements.Length)](index));
		}

		lines.AddRange(StatementGap(random));

		lines.Add("internal sealed class Sample");
		lines.Add("{");

		lines.AddRange(CreateMemberLines(random, looseRun));

		lines.Add("}");

		return string.Join("\n", lines) + "\n";
	}

	// A conditional directive leaves its container as written, so only some seeds hold one.
	private static List<string> CreateMemberLines(Random random, int looseRun)
	{
		List<string> lines = [];

		Func<int, string[]>[] pool = random.Next(5) == 0 ? s_looseMembers : s_looseMembers[..^1];

		int count = random.Next(6, 13);
		int runIndex = random.Next(count);

		for (int index = 0; index < count; index++)
		{
			if (index > 0)
				lines.AddRange(StatementGap(random));

			// One run of loose fields, set apart on both sides, gives every seed a run to gather.
			if (index == runIndex)
			{
				if (index > 0 && lines[^1].Length > 0)
					lines.Add(string.Empty);

				for (int value = 0; value < looseRun; value++)
					lines.AddRange([$"\tprivate int _loose{value};", string.Empty]);
			}

			lines.AddRange(
				pool[random.Next(pool.Length)]
					(index)
					.Select(static line => line.Length == 0 || line[0] == '#' ? line : "\t" + line)
			);
		}

		return lines;
	}

	internal static string CreateLayoutSource(Random random)
	{
		List<string> lines = [];

		foreach (string import in s_importLines)
		{
			lines.AddRange(Gap(random));
			lines.Add(import);
		}

		lines.AddRange(Gap(random));

		lines.Add("internal sealed class Sample");
		lines.Add("{");

		for (int index = 0; index < 6; index++)
		{
			lines.AddRange(Gap(random));

			foreach (string line in s_members[random.Next(s_members.Length)](index))
			{
				lines.Add(line.Length == 0 ? line : "\t" + line);

				// A blank line may fall between any two lines of a member, which is valid wherever it lands.
				if (random.Next(6) == 0)
					lines.AddRange(Gap(random));
			}
		}

		lines.Add("}");

		return string.Join("\n", lines) + "\n";
	}

	internal static IEnumerable<string> IndentStatement(IEnumerable<string> statement)
	{
		return statement.Select(static line =>
			line.Length == 0 || line[0] == '#' ? line : "\t\t" + line
		);
	}

	internal static string WrapStatements(IEnumerable<string> lines)
	{
		return "internal sealed class Sample\n{\n\tpublic async System.Threading.Tasks.Task Run()\n\t{\n\t\tint loose = 0;\n\n"
			+ string.Join("\n", lines)
			+ "\n\t}\n}\n";
	}

	// Most gaps hold one blank line, so runs of loose statements come up often.
	internal static IEnumerable<string> StatementGap(Random random)
	{
		return Enumerable.Repeat(
			string.Empty,

			random.Next(20) switch
			{
				< 5 => 0,
				< 17 => 1,
				_ => 2,
			}
		);
	}

	private static IEnumerable<string> Gap(Random random)
	{
		return Enumerable.Repeat(
			string.Empty,

			random.Next(10) switch
			{
				< 4 => 0,
				< 8 => 1,
				_ => 2,
			}
		);
	}
}
