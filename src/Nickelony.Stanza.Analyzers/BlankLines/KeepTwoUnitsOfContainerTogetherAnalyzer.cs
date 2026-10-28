using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class KeepTwoUnitsOfContainerTogetherAnalyzer() : StanzaRuleAnalyzer(StanzaRules.KeepTwoUnitsOfContainerTogether)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(context.Node);

		if (BlankLineOwnership.HoldsTwoKeptUnits(siblings))
			BlankLineRemoval.ReportWholeGapBoundaries(context, siblings, Rule);
	}
}
