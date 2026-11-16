using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RemoveSwitchLabelPaddingAnalyzer() : StanzaRuleAnalyzer(StanzaRules.RemoveSwitchLabelPadding)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		var section = (SwitchSectionSyntax)context.Node;

		if (section.Statements.Count == 0)
			return;

		StanzaOptions? options = null;

		for (int index = 0; index < section.Labels.Count; index++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			bool isLast = index == section.Labels.Count - 1;

			SyntaxToken colon = section.Labels[index].ColonToken;
			SyntaxToken next = isLast ? section.Statements[0].GetFirstToken() : section.Labels[index + 1].GetFirstToken();

			// A block that opens a section is padded by its header, which the brace padding rule owns where it is on.
			if (isLast && section.Statements[0] is BlockSyntax)
			{
				options ??= StanzaOptions.From(context.Options, context.Node.SyntaxTree);

				if (BlankLineOwnership.IsBracePaddingOnAt(scope: context, options, rule: Rule, position: next.SpanStart))
					continue;
			}

			RemovableBlankLineAnalysis.ReportRemovableBlankLine(context, anchor: colon, neighbor: next, rule: Rule);
		}
	}
}
