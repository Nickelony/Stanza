using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Commas;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class AddTrailingCommaAnalyzer() : TrailingCommaAnalyzer(StanzaRules.AddTrailingComma)
{
	protected override CommaEdit? CreateEdit(SyntaxNode list) => CommaEdits.CreateAddEdit(list);
}
