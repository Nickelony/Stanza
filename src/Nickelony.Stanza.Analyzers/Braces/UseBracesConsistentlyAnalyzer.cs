using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class UseBracesConsistentlyAnalyzer()
	: StanzaNodeAnalyzer(DiagnosticDescriptors.UseBracesConsistently, StanzaKinds.BraceStatementKinds)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		if (StanzaOptions.From(context.Options, context.Node.SyntaxTree).PreferBraces is false)
			return;

		ImmutableArray<BraceTarget> targets = BraceTargets.GetTargets(context.Node);

		if (!BraceTargets.HasMultiLineTarget(targets))
			return;

		SourceText sourceText = context.Node.SyntaxTree.GetText(context.CancellationToken);

		foreach (BraceTarget target in targets)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (target.Block is not null)
				continue;

			if (BraceEdits.CreateBraceInsertions(sourceText, target.HeaderSpan, target.Body).IsEmpty)
				continue;

			context.ReportDiagnostic(
				Diagnostic.Create(Descriptor, target.Body.GetFirstToken().GetLocation(), additionalLocations: [target.Body.GetLocation()])
			);
		}
	}
}
