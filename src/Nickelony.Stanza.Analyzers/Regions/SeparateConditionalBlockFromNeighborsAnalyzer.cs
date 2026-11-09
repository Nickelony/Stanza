using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Regions;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateConditionalBlockFromNeighborsAnalyzer()
	: StanzaTreeAnalyzer(DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context)
	{
		RegionAnalysis.ReportEdits(
			context,
			Descriptor,
			RegionEdits.CreateConditionalBlockGapEdits(context.Tree, context.Tree.GetText(context.CancellationToken), context.CancellationToken)
		);
	}
}
