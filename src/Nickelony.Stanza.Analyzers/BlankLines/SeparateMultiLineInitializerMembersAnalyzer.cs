using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateMultiLineInitializerMembersAnalyzer()
	: SeparateMultiLineSiblingsAnalyzer(StanzaRules.SeparateMultiLineInitializerMembers);
