using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class SeparateDeclarationAndInvocationRunsAnalyzer() : StanzaRuleAnalyzer(StanzaRules.SeparateDeclarationAndInvocationRuns)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		ImmutableArray<SyntaxNode> siblings = StanzaSyntax.ReadSiblings(context.Node);

		if (siblings.Length < 2)
			return;

		var options = StanzaOptions.From(context.Options, context.Node.SyntaxTree);

		BlankLineAnalysis.ReportGroupingBoundaries(
			context,
			options,
			siblings,

			boundaries: BlankLineGrouping.ReadDeclarationRunBoundaries(
				siblings,
				options,
				gapOwners: BlankLineOwnership.ReadGapOwners(context, options, Rule),
				cancellationToken: context.CancellationToken
			),

			rule: Rule
		);
	}
}
