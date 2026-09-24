using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests;

[TestClass]
public sealed class GapUnitsTests
{
	[TestMethod]
	[CrossCutting]
	public async Task TheUnitAfterTheGapIsTheWholeMemberAndNotItsFirstTokenNode()
	{
		const string source = """
			class C
			{
				int a;
				int b;
				void M()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateMultiLineConstructsAnalyzer>(source);

		Diagnostic diagnostic = result.Diagnostics.Single();

		StanzaDiagnosticLookup.NeighborUnits? units = StanzaDiagnosticLookup.FindNeighborUnits(
			diagnostic.Location.SourceTree!.GetRoot(),
			diagnostic
		);

		Assert.IsInstanceOfType<FieldDeclarationSyntax>(units!.Value.Previous);
		Assert.IsInstanceOfType<MethodDeclarationSyntax>(units.Value.Next);
	}
}
