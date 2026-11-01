using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Braces;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RemoveElseAfterJumpAnalyzer() : StanzaRuleAnalyzer(StanzaRules.RemoveElseAfterJump)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		var ifStatement = (IfStatementSyntax)context.Node;

		if (ifStatement.Else is not { } elseClause)
			return;

		SourceText sourceText = ifStatement.SyntaxTree.GetText(context.CancellationToken);

		if (ElseAfterJump.CreateRemoval(sourceText, ifStatement).IsEmpty)
			return;

		// The fix of the outer chain moves this one along, so the outer one goes first.
		if (
			ifStatement.Parent is BlockSyntax { Parent: ElseClauseSyntax { Parent: IfStatementSyntax outer } }
			&& !ElseAfterJump.CreateRemoval(sourceText, outer).IsEmpty
		)
		{
			return;
		}

		// The chain keeps the shape the braces rule gives it until that rule has nothing to add, so the two never undo each other.
		if (BraceEdits.IsWaitingForBraces(context, rule: Rule, sourceText, ifStatement))
			return;

		var fixOptions = new FixOptions(
			IndentStepColumns: StanzaOptions.From(context.Options, ifStatement.SyntaxTree).IndentStepColumns,
			SeparatesNeighbors: BraceEdits.IsSeparatingNeighbors(context, Rule, ifStatement)
		);

		context.ReportDiagnostic(Diagnostic.Create(Descriptor, elseClause.ElseKeyword.GetLocation(), properties: fixOptions.ToProperties()));
	}
}
