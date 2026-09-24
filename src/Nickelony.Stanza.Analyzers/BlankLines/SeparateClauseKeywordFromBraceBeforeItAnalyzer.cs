using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateClauseKeywordFromBraceBeforeItAnalyzer() : ClauseKeywordGapAnalyzer(StanzaRules.SeparateClauseKeywordFromBraceBeforeIt)
{
	protected override void ReportGap(SyntaxNodeAnalysisContext context, SyntaxToken keyword, SyntaxToken brace) =>
		BlankLineAnalysis.ReportRequiredBlankLine(context, anchor: keyword, neighbor: brace, rule: Rule);
}
