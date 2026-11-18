using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Regions;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateConditionalBlockAnalyzer()
	: StanzaTreeAnalyzer(DiagnosticDescriptors.SeparateConditionalBlock)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context)
	{
		RegionAnalysis.ReportEdits(
			context,
			Descriptor,

			RegionEdits.CreateMarkerGapEdits(
				block: DirectiveBlock.For(Descriptor),
				syntaxTree: context.Tree,
				sourceText: context.Tree.GetText(context.CancellationToken),
				options: StanzaOptions.From(context.Options, context.Tree),
				cancellationToken: context.CancellationToken
			)
		);
	}
}
