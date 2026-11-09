using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CollapseEmptyBlocksAnalyzer() : StanzaRuleAnalyzer(StanzaRules.CollapseEmptyBlocks)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		var options = StanzaOptions.From(context.Options, context.Node.SyntaxTree);

		// With `csharp_preserve_single_line_blocks = false`, the formatter would expand the braces again,
		// so joining them would loop rather than settle.
		if (!options.PreserveSingleLineBlocks)
			return;

		if (StanzaBraces.FindBracePair(context.Node) is not { } braces)
			return;

		SourceText sourceText = context.Node.SyntaxTree.GetText(context.CancellationToken);

		if (BraceEdits.CreateEmptyBlockCollapse(sourceText, braces.Previous, braces.Next) is null)
			return;

		int separatedUnits = BraceEdits.ReadSeparatedUnits(context, Rule, units: BraceEdits.CollectJoinedUnits(sourceText, braces.Previous));

		context.ReportDiagnostic(
			Diagnostic.Create(
				descriptor: Descriptor,
				location: braces.Next.GetLocation(),
				additionalLocations: [braces.Previous.GetLocation()],
				properties: new FixOptions(IndentStepColumns: options.IndentStepColumns, SeparatedUnits: separatedUnits).ToProperties()
			)
		);
	}
}
