using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateImportSectionAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateImportSection)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		BlankLineAnalysis.ReportImportBoundaries(
			context,
			siblings: StanzaSyntax.ReadSiblings(context.Node),
			rule: Rule,
			isBoundary: BlankLineImports.IsImportTerminator
		);
	}
}
