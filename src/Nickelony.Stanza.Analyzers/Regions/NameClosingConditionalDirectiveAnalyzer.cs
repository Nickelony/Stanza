using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Regions;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class NameClosingConditionalDirectiveAnalyzer() : StanzaTreeAnalyzer(DiagnosticDescriptors.NameClosingConditionalDirective)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context)
	{
		RegionAnalysis.ReportEdits(
			context,
			Descriptor,

			RegionEdits.CreateEndIfNameEdits(
				syntaxTree: context.Tree,
				sourceText: context.Tree.GetText(context.CancellationToken),
				minLines: StanzaOptions.From(context.Options, context.Tree).MinLinesToNameEndif,
				cancellationToken: context.CancellationToken
			)
		);
	}
}
