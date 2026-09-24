using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Hands each provider a diagnostic that does not fit its text.</summary>
[TestClass]
public sealed class StaleDiagnosticTests
{
	private const string Source = "class C\n{\n\tint _first;\n\n\tint _second;\n}\n";

	[TestMethod]
	public async Task NoRuleOffersAFixForADiagnosticPastTheEndOfTheDocument()
	{
		var past = new TextSpan(Source.Length + 40, 1);

		foreach ((_, DiagnosticDescriptor descriptor) in TestRuleSets.Declared)
		{
			int offered = await RoslynTestHarness.CountOfferedFixesAsync(Source, descriptor, past);

			Assert.AreEqual(
				0,
				offered,
				$"{descriptor.Id} offers a fix for a diagnostic past the end of the text."
			);
		}
	}

	[TestMethod]
	public async Task NoRuleFailsOnADiagnosticThatLandsInsideAToken()
	{
		var inside = new TextSpan(Source.IndexOf("_first", System.StringComparison.Ordinal) + 2, 1);

		foreach ((_, DiagnosticDescriptor descriptor) in TestRuleSets.Declared)
			_ = await RoslynTestHarness.CountOfferedFixesAsync(Source, descriptor, inside);
	}

	[TestMethod]
	[DataRow("1", 1)]
	[DataRow("many", 0)]
	[DataRow("", 0)]
	[DataRow("-1", 0)]
	[DataRow("1.5", 0)]
	public async Task OnlyAKeptBlankLinesPropertyThatIsANumberOffersTheFix(string kept, int fixes)
	{
		const string source = "<a>\n\n\n\n<b />\n</a>";

		int offered = await RoslynTestHarness.CountOfferedFixesAsync(
			source,
			descriptor: DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether,
			span: new TextSpan(source.IndexOf("<b", System.StringComparison.Ordinal), 2),
			properties: ImmutableDictionary<string, string?>.Empty.Add("keptBlankLines", kept)
		);

		Assert.AreEqual(fixes, offered, $"A kept count of '{kept}' offers {offered} fixes.");
	}
}
