using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateCommentedSiblingAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateCommentedSibling)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) =>
		BlankLineAnalysis.ReportCommentedSiblingGaps(context, StanzaSyntax.ReadSiblings(context.Node), Rule);
}
