using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Nickelony.Stanza.Analyzers.BlankLines;

namespace Nickelony.Stanza.Analyzers.Tests.Harness;

// This analyzer hands a test callback each container its rule judges,
// and is never shipped as a compiler extension.
#pragma warning disable RS1036, RS1038, RS1041
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class ContainerReadingAnalyzer(StanzaRule rule, Action<ContainerContext> read)
	: StanzaRuleAnalyzer(rule)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context) =>
		read(ContainerContext.From(context));
}
#pragma warning restore RS1036, RS1038, RS1041
