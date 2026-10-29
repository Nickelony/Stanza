using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class GatherRunOfLooseStatementsAnalyzer() : StanzaRuleAnalyzer(StanzaRules.GatherRunOfLooseStatements)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(context.Node);
		var options = StanzaOptions.From(context.Options, context.Node.SyntaxTree);

		BlankLineRemoval.ReportRuns(context, siblings, runs: BlankLineLooseRuns.ReadRuns(context, options, siblings, rule: Rule), rule: Rule);
	}
}
