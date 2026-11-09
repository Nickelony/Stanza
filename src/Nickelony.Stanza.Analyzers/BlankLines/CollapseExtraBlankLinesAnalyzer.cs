using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CollapseExtraBlankLinesAnalyzer() : StanzaTreeAnalyzer(DiagnosticDescriptors.CollapseExtraBlankLines)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context)
	{
		int cap = StanzaOptions.From(context.Options, context.Tree).MaxBlankLines;

		// Zero is no cap, and a cap never reaches the last blank line of a run, so no rule that asks for one can fight this one.
		if (cap == 0)
			return;

		SyntaxNode root = context.Tree.GetRoot(context.CancellationToken);
		SourceText sourceText = context.Tree.GetText(context.CancellationToken);

		List<TextSpan> surpluses = BlankLineSurplus.CollectSurplusSpans(root, sourceText, cap, cancellationToken: context.CancellationToken);

		foreach (TextSpan surplus in surpluses)
		{
			// The reported span holds exactly the lines to remove, so the fix needs nothing more than the diagnostic.
			context.ReportDiagnostic(Diagnostic.Create(Descriptor, Location.Create(context.Tree, surplus)));
		}
	}
}
