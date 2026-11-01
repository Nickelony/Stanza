using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Limits;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class ShortenLongMemberAnalyzer() : LimitAnalyzer(DiagnosticDescriptors.ShortenLongMember)
{
	protected override int ReadLimit(StanzaOptions options) => options.MaxMemberStatements;

	protected override IEnumerable<SyntaxNode> CollectPastLimit(SyntaxNode root, int limit, CancellationToken cancellationToken) =>
		MemberStatements.CollectLongMembers(root, limit, cancellationToken);

	protected override Location GetReportLocation(SyntaxNode node) => MemberStatements.ReadNameToken(node).GetLocation();
}
