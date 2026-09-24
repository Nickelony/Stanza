using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RemoveRedundantBracesAnalyzer()
	: StanzaNodeAnalyzer(DiagnosticDescriptors.RemoveRedundantBraces, StanzaKinds.BraceStatementKinds)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		var options = StanzaOptions.From(context.Options, context.Node.SyntaxTree);

		if (options.PreferBraces is true)
			return;

		ImmutableArray<BraceTarget> targets = BraceTargets.GetTargets(context.Node);

		// One multi-line branch pins the whole chain to the braced shape, and so does an else that holds only an if,
		// so the chain keeps one shape and the braced rule has nothing to restore.
		if (
			BraceTargets.HasMultiLineTarget(targets)
			|| targets.Any(static target => target.Block is { } block && BraceTargets.IsElseHoldingAnIf(block))
		)
		{
			return;
		}

		SourceText sourceText = context.Node.SyntaxTree.GetText(context.CancellationToken);
		int tabColumns = options.TabColumns;

		foreach (BraceTarget target in targets)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (target.Block is not { } block)
				continue;

			if (BraceEdits.CreateSafeBraceRemoval(sourceText, block, tabColumns).IsEmpty)
				continue;

			// The report points at the opening brace and adds the closing one as a second location:
			// a span over both braces would make the editor fade the body too.
			// The tab width travels in the properties, so the fix measures indentation the way this rule does.
			context.ReportDiagnostic(
				Diagnostic.Create(
					descriptor: Descriptor,
					location: block.OpenBraceToken.GetLocation(),
					additionalLocations: [block.CloseBraceToken.GetLocation()],
					properties: new FixOptions(TabColumns: tabColumns).ToProperties()
				)
			);
		}
	}
}
