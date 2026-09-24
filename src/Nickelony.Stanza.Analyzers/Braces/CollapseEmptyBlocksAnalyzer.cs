using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CollapseEmptyBlocksAnalyzer() : StanzaNodeAnalyzer(DiagnosticDescriptors.CollapseEmptyBlocks, StanzaKinds.EmptyBlockKinds)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		// With `csharp_preserve_single_line_blocks = false`, the formatter would expand the braces again,
		// so joining them would loop rather than settle.
		if (!StanzaOptions.From(context.Options, context.Node.SyntaxTree).PreserveSingleLineBlocks)
			return;

		if (StanzaBraces.FindBracePair(context.Node) is not { } braces)
			return;

		SourceText sourceText = context.Node.SyntaxTree.GetText(context.CancellationToken);

		if (BraceEdits.CreateEmptyBlockCollapse(sourceText, braces.Previous, braces.Next) is null)
			return;

		context.ReportDiagnostic(Diagnostic.Create(Descriptor, braces.Next.GetLocation(), additionalLocations: [braces.Previous.GetLocation()]));
	}
}
