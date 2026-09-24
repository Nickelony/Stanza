using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateFileScopedNamespaceDeclarationFromFirstMemberAnalyzer()
	: StanzaRuleAnalyzer(StanzaRules.SeparateFileScopedNamespaceDeclarationFromFirstMember)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) => BlankLineAnalysis.ReportFileScopedNamespaceGap(context, Rule);
}
