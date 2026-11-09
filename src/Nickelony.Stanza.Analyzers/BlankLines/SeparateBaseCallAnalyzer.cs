using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateBaseCallAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateBaseCall)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(context.Node);

		// A base call needs a statement beside it, so a body of fewer than two has no gap to judge.
		if (siblings.Length < 2)
			return;

		var container = new ContainerContext(context, StanzaOptions.From(context.Options, context.Node.SyntaxTree), siblings);
		ImmutableArray<int> boundaries = BlankLineBaseCalls.FindBaseCallBoundaries(container);

		if (boundaries.IsEmpty || BlankLineOwnership.IsGapOwnedByTwoUnitRule(container, rule: Rule))
			return;

		// The run, jump and guard rules defer to this rule at these gaps, so it reports them whatever those rules ask.
		RequiredBlankLineAnalysis.ReportGroupingBoundaries(container, boundaries, rule: Rule);
	}
}
