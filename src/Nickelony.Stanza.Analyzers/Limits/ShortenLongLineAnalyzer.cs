using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Limits;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class ShortenLongLineAnalyzer() : StanzaTreeAnalyzer(DiagnosticDescriptors.ShortenLongLine)
{
	protected override void Analyze(SyntaxTreeAnalysisContext context)
	{
		var options = StanzaOptions.From(context.Options, context.Tree);

		// A width of zero turns the rule off, and it skips the walk.
		if (!options.HasLineLimit)
			return;

		ImmutableArray<TextSpan> overflows = LongLines.CollectOverflows(
			syntaxTree: context.Tree,
			maxColumns: options.MaxLineLength,
			tabColumns: options.TabColumns,
			cancellationToken: context.CancellationToken
		);

		foreach (TextSpan overflow in overflows)
			context.ReportDiagnostic(Diagnostic.Create(Descriptor, Location.Create(context.Tree, overflow)));
	}
}
