using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateImportGroupsAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateImportGroups)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		RequiredBlankLineAnalysis.ReportImportBoundaries(
			context,
			siblings: StanzaSyntax.ReadSiblings(context.Node),
			rule: Rule,
			isBoundary: BlankLineImports.IsImportKindChange
		);
	}
}
