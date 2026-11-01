using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Limits;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class ShortenLongConditionAnalyzer() : LimitAnalyzer(DiagnosticDescriptors.ShortenLongCondition)
{
	protected override int ReadLimit(StanzaOptions options) => options.MaxLogicalOperators;

	protected override IEnumerable<SyntaxNode> CollectPastLimit(SyntaxNode root, int limit, CancellationToken cancellationToken) =>
		LogicalConditions.CollectLongConditions(root, limit, cancellationToken);
}
