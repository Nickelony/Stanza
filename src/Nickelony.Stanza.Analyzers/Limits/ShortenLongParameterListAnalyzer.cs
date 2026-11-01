using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Limits;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class ShortenLongParameterListAnalyzer() : LimitAnalyzer(DiagnosticDescriptors.ShortenLongParameterList)
{
	protected override int ReadLimit(StanzaOptions options) => options.MaxParameters;

	protected override IEnumerable<SyntaxNode> CollectPastLimit(SyntaxNode root, int limit, CancellationToken cancellationToken) =>
		ParameterLists.CollectLongLists(root, limit, cancellationToken);
}
