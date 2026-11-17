using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateClauseKeywordsAnalyzer() : ClauseKeywordGapAnalyzer(StanzaRules.SeparateClauseKeywords)
{
	protected override void ReportGap(SyntaxNodeAnalysisContext context, SyntaxToken keyword, SyntaxToken brace) =>
		RequiredBlankLineAnalysis.ReportRequiredBlankLine(context, anchor: keyword, neighbor: brace, rule: Rule);
}
