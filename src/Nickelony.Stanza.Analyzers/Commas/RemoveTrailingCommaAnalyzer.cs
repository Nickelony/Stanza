using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Commas;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RemoveTrailingCommaAnalyzer() : TrailingCommaAnalyzer(StanzaRules.RemoveTrailingComma)
{
	protected override CommaEdit? CreateEdit(SyntaxNode list) => CommaEdits.CreateRemoveEdit(list);
}
