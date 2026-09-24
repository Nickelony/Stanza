using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class KeepTwoUnitsOfContainerTogetherAnalyzer() : StanzaRuleAnalyzer(StanzaRules.KeepTwoUnitsOfContainerTogether)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(context.Node);

		if (siblings.Length != 2 || !IsEligible(siblings[0]) || !IsEligible(siblings[1]))
			return;

		BlankLineRemoval.ReportWholeGapBoundaries(context, siblings, Rule);
	}

	private static bool IsEligible(SyntaxNode unit) =>
		unit is not (ExternAliasDirectiveSyntax or UsingDirectiveSyntax or LocalFunctionStatementSyntax);
}
