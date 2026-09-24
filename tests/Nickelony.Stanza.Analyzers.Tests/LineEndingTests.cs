using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.Regions;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class LineEndingTests
{
	/// <summary>
	/// Lines end in CRLF down to the fix and in LF below it, so a fix reading a later line fails.
	/// </summary>
	public static IEnumerable<object[]> MixedEndingSources { get; } =
	[
		[
			"the braces the consistency rule adds",

			"class C\r\n{\r\n\tvoid M(int value)\r\n\t{\r\n\t\tif (value > 0)\r\n\t\t{\r\n\t\t\tDoThing();\r\n\t\t\tDoThing();\r\n\t\t}\r\n\t\telse\r\n\t\t\tDoThing();\r\n"
				+ "\t}\n}\n",

			"class C\r\n{\r\n\tvoid M(int value)\r\n\t{\r\n\t\tif (value > 0)\r\n\t\t{\r\n\t\t\tDoThing();\r\n\t\t\tDoThing();\r\n\t\t}\r\n\t\telse\r\n\t\t{\r\n\t\t\tDoThing();\r\n\t\t}\r\n"
				+ "\t}\n}\n",

			TestRuleSets.Only(DiagnosticDescriptors.UseBracesConsistently),
		],

		[
			"the documentation comment the layout rule wraps",

			"class C\r\n{\r\n\t/// <summary>One two three four five six seven eight nine ten eleven twelve thirteen fourteen.</summary>\r\n"
				+ "\tvoid M() { }\n}\n",

			"class C\r\n{\r\n\t/// <summary>\r\n\t/// One two three four five six seven eight nine ten eleven twelve thirteen fourteen.\r\n\t/// </summary>\r\n"
				+ "\tvoid M() { }\n}\n",

			TestRuleSets.SuggestionTierEnabled,
		],

		[
			"the region markers the marker rule separates",
			"class C\r\n{\r\n\t#region Fields\r\n\tint value;\r\n" + "\t#endregion\n}\n",

			"class C\r\n{\r\n\t#region Fields\r\n\r\n\tint value;\r\n\r\n"
				+ "\t#endregion Fields\n}\n",

			TestRuleSets.StandaloneRulesEnabled,
		],

		[
			"the arguments the list rule separates",

			"class C\r\n{\r\n\tvoid M()\r\n\t{\r\n\t\tCall(\r\n\t\t\tBuild(\r\n\t\t\t\t1),\r\n"
				+ "\t\t\tBuild(\n\t\t\t\t2)\n\t\t);\n\t}\n\n\tvoid Call(int first, int second) { }\n\n\tint Build(int value) => value;\n}\n",

			"class C\r\n{\r\n\tvoid M()\r\n\t{\r\n\t\tCall(\r\n\t\t\tBuild(\r\n\t\t\t\t1),\r\n\r\n"
				+ "\t\t\tBuild(\n\t\t\t\t2)\n\t\t);\n\t}\n\n\tvoid Call(int first, int second) { }\n\n\tint Build(int value) => value;\n}\n",

			TestRuleSets.ListRulesEnabled,
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(MixedEndingSources),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task AFixInAMixedDocumentWritesTheBreakOfItsOwnLines(
		string name,
		string source,
		string fixedSource,
		IReadOnlyDictionary<string, ReportDiagnostic> options
	)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source, options);
		RoslynTestHarness.AssertFixedText(fixedSource, result, message: $"{name}:");
	}

	[TestMethod]
	[CrossCutting]
	public async Task ARegionMarkerGapTakesTheEndingOfTheLineAboveIt()
	{
		const string source =
			"class C\r\n{\r\n\tint a;\r\n\t#region Fields\n\tint b;\n\t#endregion\n}\r\n";

		const string fixedSource =
			"class C\r\n{\r\n\tint a;\r\n\r\n\t#region Fields\n\n\tint b;\n\n\t#endregion\n}\r\n";

		AnalyzerRunResult result =
			await RoslynTestHarness.RunAsync<SeparateRegionMarkerFromNeighborsAnalyzer>(source);

		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}
}
