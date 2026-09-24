using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class KeepClauseKeywordsAdjacentAnalyzer() : ClauseKeywordGapAnalyzer(StanzaRules.KeepClauseKeywordsAdjacent)
{
	protected override void ReportGap(SyntaxNodeAnalysisContext context, SyntaxToken keyword, SyntaxToken brace) =>
		BlankLineRemoval.ReportRemovableBlankLine(context, anchor: keyword, neighbor: brace, rule: Rule);
}
