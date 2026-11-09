using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Limits;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class BreakUpLongTernaryChainAnalyzer() : LimitAnalyzer(DiagnosticDescriptors.BreakUpLongTernaryChain)
{
	protected override int ReadLimit(StanzaOptions options) => options.MaxTernaryOperators;

	protected override IEnumerable<SyntaxNode> CollectPastLimit(SyntaxNode root, int limit, CancellationToken cancellationToken) =>
		TernaryChains.CollectLongChains(root, limit, cancellationToken);
}
