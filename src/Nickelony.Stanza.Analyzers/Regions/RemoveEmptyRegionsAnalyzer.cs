using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Regions;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RemoveEmptyRegionsAnalyzer() : StanzaTreeAnalyzer(DiagnosticDescriptors.RemoveEmptyRegions)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context)
	{
		RegionAnalysis.ReportEdits(
			context,
			Descriptor,

			RegionEdits.CreateEmptyRegionEdits(
				context.Tree.GetRoot(context.CancellationToken),
				context.Tree.GetText(context.CancellationToken),
				context.CancellationToken
			)
		);
	}
}
