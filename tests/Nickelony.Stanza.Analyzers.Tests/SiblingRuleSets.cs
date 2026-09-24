using System.Collections.Generic;
using System.Collections.Immutable;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>The sets of sibling rules the pair contract tests run over.</summary>
internal static class SiblingRuleSets
{
	internal static ImmutableArray<SiblingRuleSet> All { get; } =
	[
		new SiblingRuleSet(
			Name: "accessors",
			Separate: DiagnosticDescriptors.SeparateMultiLineAccessors,
			KeepMultiLine: DiagnosticDescriptors.KeepMultiLineAccessorsTogether,
			KeepSingleLine: DiagnosticDescriptors.KeepSingleLineAccessorsTogether,

			Frame: """
			class C
			{
				private int _value;

				@@
			}
			""",

			Container: """
			public int Value{n}
			{
				@@
			}
			""",

			Separator: "",
			MultiLine: ["get\n{\n\treturn _value;\n}", "set\n{\n\t_value = value;\n}"],
			SingleLine: ["get { return _value; }", "set { _value = value; }"]
		),

		new SiblingRuleSet(
			Name: "switch sections",
			Separate: DiagnosticDescriptors.SeparateMultiLineSwitchSections,
			KeepMultiLine: DiagnosticDescriptors.KeepMultiLineSwitchSectionsTogether,
			KeepSingleLine: DiagnosticDescriptors.KeepSingleLineSwitchSectionsTogether,

			Frame: """
			class C
			{
				void M(int value)
				{
					@@
				}
			}
			""",

			Container: """
			switch (value)
			{
				@@
			}
			""",

			Separator: "",
			MultiLine: ["case 1:\n\treturn;", "case 2:\n\treturn;"],
			SingleLine: ["case 1: return;", "case 2: return;"]
		),

		new SiblingRuleSet(
			Name: "switch expression arms",
			Separate: DiagnosticDescriptors.SeparateMultiLineSwitchExpressionArms,
			KeepMultiLine: DiagnosticDescriptors.KeepMultiLineSwitchExpressionArmsTogether,
			KeepSingleLine: DiagnosticDescriptors.KeepSingleLineSwitchExpressionArmsTogether,

			Frame: """
			class C
			{
				void M(int value)
				{
					@@
				}

				string Build(int value)
				{
					return value.ToString();
				}
			}
			""",

			Container: """
			var result{n} = value switch
			{
				@@
			};
			""",

			Separator: ",",
			MultiLine: ["1 => Build(\n\tvalue\n)", "2 => Build(\n\tvalue\n)"],
			SingleLine: ["1 => \"one\"", "2 => \"two\""]
		),

		new SiblingRuleSet(
			Name: "initializer members",
			Separate: DiagnosticDescriptors.SeparateMultiLineInitializerMembers,
			KeepMultiLine: DiagnosticDescriptors.KeepMultiLineInitializerMembersTogether,
			KeepSingleLine: DiagnosticDescriptors.KeepSingleLineInitializerMembersTogether,

			Frame: """
			class C
			{
				void M()
				{
					@@
				}
			}
			""",

			Container: """
			var item{n} = new Item
			{
				@@
			};
			""",

			Separator: ",",

			MultiLine:
			[
				"Tags = new List<string>\n{\n\t\"x\",\n}",
				"Name = string.Concat(\n\t\"a\",\n\t\"b\"\n)",
			],

			SingleLine: ["Name = \"c\"", "Count = 3"]
		),

		new SiblingRuleSet(
			Name: "collection elements",
			Separate: DiagnosticDescriptors.SeparateMultiLineCollectionElements,
			KeepMultiLine: DiagnosticDescriptors.KeepMultiLineCollectionElementsTogether,
			KeepSingleLine: DiagnosticDescriptors.KeepSingleLineCollectionElementsTogether,

			Frame: """
			class C
			{
				void M()
				{
					@@
				}

				int Build(int value)
				{
					return value;
				}
			}
			""",

			Container: """
			var items{n} = new[]
			{
				@@
			};
			""",

			Separator: ",",
			MultiLine: ["Build(\n\t1)", "Build(\n\t2)"],
			SingleLine: ["1", "2"]
		),

		new SiblingRuleSet(
			Name: "arguments",
			Separate: DiagnosticDescriptors.SeparateMultiLineArguments,
			KeepMultiLine: DiagnosticDescriptors.KeepMultiLineArgumentsTogether,
			KeepSingleLine: DiagnosticDescriptors.KeepSingleLineArgumentsTogether,

			Frame: """
			class C
			{
				void M()
				{
					@@
				}
			}
			""",

			Container: """
			Call(
				@@
			);
			""",

			Separator: ",",
			MultiLine: ["Build(\n\t1\n)", "Build(\n\t2\n)"],
			SingleLine: ["1", "2"]
		),

		new SiblingRuleSet(
			Name: "parameters",
			Separate: DiagnosticDescriptors.SeparateMultiLineParameters,
			KeepMultiLine: DiagnosticDescriptors.KeepMultiLineParametersTogether,
			KeepSingleLine: DiagnosticDescriptors.KeepSingleLineParametersTogether,

			Frame: """
			class C
			{
				@@
			}
			""",

			Container: """
			void M{n}(
				@@)
			{
				return;
			}
			""",

			Separator: ",",

			MultiLine:
			[
				"IReadOnlyDictionary<\n\tstring,\n\tint> lookup",
				"IReadOnlyDictionary<\n\tstring,\n\tint> other",
			],

			SingleLine: ["int first", "int second"]
		),

		new SiblingRuleSet(
			Name: "declarators",
			Separate: DiagnosticDescriptors.SeparateMultiLineDeclarators,
			KeepMultiLine: DiagnosticDescriptors.KeepMultiLineDeclaratorsTogether,
			KeepSingleLine: DiagnosticDescriptors.KeepSingleLineDeclaratorsTogether,

			Frame: """
			class C
			{
				void M()
				{
					@@
				}
			}
			""",

			Container: "int @@;",
			Separator: ",",
			MultiLine: ["lookup{n} = 1 +\n\t2", "capacity{n} = 3 +\n\t4"],
			SingleLine: ["first{n} = 1", "second{n} = 2"]
		),
	];

	/// <summary>The rows of a dynamic-data test that runs over every set.</summary>
	internal static IEnumerable<object[]> Rows()
	{
		foreach (SiblingRuleSet siblings in All)
			yield return [siblings];
	}
}
