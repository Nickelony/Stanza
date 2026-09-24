using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Base of a rule that judges the gap between a closing brace and the clause keyword after it.</summary>
internal abstract class ClauseKeywordGapAnalyzer(StanzaRule rule) : StanzaRuleAnalyzer(rule)
{
	protected sealed override void Analyze(SyntaxNodeAnalysisContext context)
	{
		StanzaOptions? options = null;

		foreach ((SyntaxToken keyword, SyntaxToken brace) in GetClauseGaps(context.Node))
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			// Options are read once per statement, not once per gap.
			options ??= StanzaOptions.From(context.Options, context.Node.SyntaxTree);

			// The other half of the pair shares this gap, so only the half that owns it reports.
			if (!StanzaRuleState.OwnsTheSharedGap(scope: context, options, rule: Rule, position: keyword.SpanStart))
				continue;

			ReportGap(context, keyword, brace);
		}
	}

	protected abstract void ReportGap(SyntaxNodeAnalysisContext context, SyntaxToken keyword, SyntaxToken brace);

	private static IEnumerable<(SyntaxToken Keyword, SyntaxToken Brace)> GetClauseGaps(SyntaxNode statement)
	{
		switch (statement)
		{
			case IfStatementSyntax ifStatement:
				if (ifStatement.Else is { } elseClause && ifStatement.Statement is BlockSyntax branch)
					yield return (elseClause.ElseKeyword, branch.CloseBraceToken);

				break;

			case TryStatementSyntax tryStatement:
				SyntaxToken? previousCloseBrace = null;

				foreach ((SyntaxToken keyword, BlockSyntax block) in GetClauses(tryStatement))
				{
					if (previousCloseBrace is { } closeBrace)
						yield return (keyword, closeBrace);

					previousCloseBrace = block.CloseBraceToken;
				}

				break;

			case DoStatementSyntax doStatement when doStatement.Statement is BlockSyntax body:
				yield return (doStatement.WhileKeyword, body.CloseBraceToken);
				break;
		}
	}

	private static IEnumerable<(SyntaxToken Keyword, BlockSyntax Block)> GetClauses(TryStatementSyntax tryStatement)
	{
		yield return (tryStatement.TryKeyword, tryStatement.Block);

		foreach (CatchClauseSyntax catchClause in tryStatement.Catches)
			yield return (catchClause.CatchKeyword, catchClause.Block);

		if (tryStatement.Finally is { } finallyClause)
			yield return (finallyClause.FinallyKeyword, finallyClause.Block);
	}
}
