using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class UseBracesConsistentlyAnalyzer() : StanzaRuleAnalyzer(StanzaRules.UseBracesConsistently)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		var options = StanzaOptions.From(context.Options, context.Node.SyntaxTree);

		if (options.PreferBraces is false)
			return;

		SourceText sourceText = context.Node.SyntaxTree.GetText(context.CancellationToken);
		ImmutableArray<BraceTarget> bodies = BraceEdits.FindBodiesToWrap(sourceText, BraceTargets.GetTargets(context.Node));

		if (bodies.IsEmpty || BraceEdits.IsWaitingForCollapse(context, rule: Rule, options, sourceText))
			return;

		foreach (BraceTarget target in bodies)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			context.ReportDiagnostic(
				Diagnostic.Create(Descriptor, target.Body.GetFirstToken().GetLocation(), additionalLocations: [target.Body.GetLocation()])
			);
		}
	}
}
