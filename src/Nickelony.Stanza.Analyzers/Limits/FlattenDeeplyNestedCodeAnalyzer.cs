using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Limits;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class FlattenDeeplyNestedCodeAnalyzer() : LimitAnalyzer(DiagnosticDescriptors.FlattenDeeplyNestedCode)
{
	protected override int ReadLimit(StanzaOptions options) => options.MaxNestingDepth;

	protected override IEnumerable<SyntaxNode> CollectPastLimit(SyntaxNode root, int limit, CancellationToken cancellationToken) =>
		NestedStatements.CollectDeepest(root, limit, cancellationToken);

	protected override Location GetReportLocation(SyntaxNode node) => node.GetFirstToken().GetLocation();
}
