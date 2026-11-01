using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.Commas;

/// <summary>Base of the two trailing comma rules, which share the lists they judge and the way they report.</summary>
internal abstract class TrailingCommaAnalyzer(StanzaRule rule) : StanzaRuleAnalyzer(rule)
{
	protected sealed override void Analyze(SyntaxNodeAnalysisContext context)
	{
		if (CreateEdit(context.Node) is not { } edit)
			return;

		var options = StanzaOptions.From(context.Options, context.Node.SyntaxTree);

		if (!StanzaRuleState.IsTheOwningHalf(scope: context, options, rule: Rule, position: edit.Anchor.Start))
			return;

		// The change rides the diagnostic, so the fix applies it without finding every edit of the file again.
		context.ReportDiagnostic(
			Diagnostic.Create(
				Descriptor,
				location: Location.Create(context.Node.SyntaxTree, edit.Anchor),
				properties: new FixOptions(Change: edit.Change).ToProperties()
			)
		);
	}

	protected abstract CommaEdit? CreateEdit(SyntaxNode list);
}
