using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

		// A chain is judged at its head, so a long chain is walked once.
		if (targets.IsEmpty)
			return;

		// One multi-line branch pins the whole chain to the braced shape, and so does an else that holds only an if,
		// so the chain keeps one shape and the braced rule has nothing to restore.
		// Where the option asks for no braces, that rule is off and nothing restores the braces, so only the else that holds an if pins.
		if (
			(options.PreferBraces is null && BraceTargets.HasMultiLineTarget(targets))
			|| targets.Any(static target => target.Block is { } block && BraceTargets.IsElseHoldingAnIf(block))
		)
		{
			return;
		}

		SourceText sourceText = context.Node.SyntaxTree.GetText(context.CancellationToken);
		int tabColumns = options.TabColumns;

		ImmutableArray<BraceTarget> blocked = [.. targets.Where(static target => target.Block is not null)];

		ImmutableArray<BraceTarget> removable =
		[
			.. blocked.Where(target => !BraceEdits.CreateSafeBraceRemoval(sourceText, target, tabColumns).IsEmpty),
		];

		// IDE0011 braces a chain with one braced branch, so a block that keeps its braces keeps those of the chain,
		// and ZA0205 braces the view with a multi-line branch, so a chain whose branches a directive can change keeps them too.
		// Where the option asks for no braces, each block that can lose them does.
		if (options.PreferBraces is null && (removable.Length < blocked.Length || BraceTargets.HoldsAConditionalBranch(context.Node)))
			return;

		foreach (BraceTarget target in removable)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			BlockSyntax block = target.Block!;

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
