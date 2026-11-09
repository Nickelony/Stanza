using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Limits;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class BreakUpLongElseIfChainAnalyzer() : LimitAnalyzer(DiagnosticDescriptors.BreakUpLongElseIfChain)
{
	protected override int ReadLimit(StanzaOptions options) => options.MaxElseIfBranches;

	protected override IEnumerable<SyntaxNode> CollectPastLimit(SyntaxNode root, int limit, CancellationToken cancellationToken) =>
		ElseIfChains.CollectLongChains(root, limit, cancellationToken);

	protected override Location GetReportLocation(SyntaxNode node) => ((IfStatementSyntax)node).IfKeyword.GetLocation();
}
