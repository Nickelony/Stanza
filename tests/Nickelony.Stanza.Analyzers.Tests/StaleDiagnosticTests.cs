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
}
