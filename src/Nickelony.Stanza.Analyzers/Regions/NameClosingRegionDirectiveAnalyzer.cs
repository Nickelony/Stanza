using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Regions;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class NameClosingRegionDirectiveAnalyzer() : StanzaTreeAnalyzer(DiagnosticDescriptors.NameClosingRegionDirective)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context)
	{
		RegionAnalysis.ReportEdits(
			context,
			Descriptor,
			RegionEdits.CreateEndRegionNameEdits(context.Tree.GetRoot(context.CancellationToken), context.CancellationToken)
		);
	}
}
