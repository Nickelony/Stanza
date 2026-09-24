using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class SourceShapeTests
{
	[TestMethod]
	public async Task AByteOrderMarkKeepsTheBoundaryAfterTheImportSection()
	{
		const string source =
			"\uFEFF" + "using System;\nclass C\n{\n\tvoid M()\n\t{\n\t\tDoThing();\n\t}\n}\n";

		const string fixedSource =
			"\uFEFF" + "using System;\n\nclass C\n{\n\tvoid M()\n\t{\n\t\tDoThing();\n\t}\n}\n";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task NonAsciiContentBeforeTheBoundaryDoesNotShiftTheFix()
	{
		const string source = """
			class C
			{
				void M()
				{
					var text = "café 😀";
				}
				void N()
				{
					DoThing();
				}
			}
			""";

		const string fixedSource = """
			class C
			{
				void M()
				{
					var text = "café 😀";
				}

				void N()
				{
					DoThing();
				}
			}
			""";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AFileWithoutATrailingBreakStillGetsItsBlankLine()
	{
		const string source =
			"class C\n{\n\tvoid M()\n\t{\n\t\tDoThing();\n\t}\n\tvoid N()\n\t{\n\t\tDoThing();\n\t}\n}";

		const string fixedSource =
			"class C\n{\n\tvoid M()\n\t{\n\t\tDoThing();\n\t}\n\n\tvoid N()\n\t{\n\t\tDoThing();\n\t}\n}";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	public async Task AMixedDocumentWritesTheBreakOfTheLineAboveTheGap()
	{
		const string source =
			"class C\r\n{\r\n\tvoid M()\r\n\t{\r\n\t\tDoThing();\r\n\t}\r\n\tvoid N()\n\t{\n\t\tDoThing();\n\t}\n}\n";

		const string fixedSource =
			"class C\r\n{\r\n\tvoid M()\r\n\t{\r\n\t\tDoThing();\r\n\t}\r\n\r\n\tvoid N()\n\t{\n\t\tDoThing();\n\t}\n}\n";

		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(source);

		Assert.HasCount(1, result.Diagnostics);
		RoslynTestHarness.AssertFixedText(fixedSource, result);
	}

	[TestMethod]
	[DataRow("")]
	[DataRow("   ")]
	[DataRow("   \n\t\n")]
	[DataRow("\n\n")]
	public async Task AFileOfNothingButWhitespaceStaysUntouched(string source)
	{
		AnalyzerRunResult result = await RoslynTestHarness.RunAllAsync(
			source,
			TestRuleSets.AllRulesEnabled
		);

		Assert.IsEmpty(result.Diagnostics);
	}
}
