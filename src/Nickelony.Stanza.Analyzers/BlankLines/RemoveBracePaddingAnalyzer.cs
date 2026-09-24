using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nickelony.Stanza.Analyzers.BlankLines;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RemoveBracePaddingAnalyzer() : StanzaRuleAnalyzer(StanzaRules.RemoveBracePadding)
{
	protected override void Analyze(SyntaxNodeAnalysisContext context)
	{
		if (StanzaBraces.FindBracePair(context.Node) is not { } braces)
			return;

		SyntaxToken openBrace = braces.Previous;
		SyntaxToken closeBrace = braces.Next;

		// A block statement that follows a sibling has no header to pad: the gap before its brace lies between two statements,
		// which the blank line rules own whether or not they are enabled, so removing it here would undo them.
		if (!IsStatementBlock(context.Node))
		{
			SyntaxToken headerEnd = openBrace.GetPreviousToken();

			if (!headerEnd.IsKind(SyntaxKind.None))
				Report(context, openBrace, headerEnd);
		}

		ImmutableArray<SyntaxNode> units = StanzaSyntax.ReadSiblings(context.Node);

		// Whitespace between the braces is an empty block, not padding.
		if (units.Length == 0)
			return;

		Report(context, openBrace, units[0].GetFirstToken());
		Report(context, closeBrace, units[units.Length - 1].GetLastToken());
	}

	private static bool IsStatementBlock(SyntaxNode container)
	{
		if (container is not BlockSyntax block)
			return false;

		SyntaxNode? parent = block.Parent;

		return parent switch
		{
			BlockSyntax blockParent => blockParent.Statements.IndexOf(block) > 0,
			SwitchSectionSyntax section => section.Statements.IndexOf(block) > 0,
			GlobalStatementSyntax => HasPrecedingUnit(block),
			_ => false,
		};
	}

	private static bool HasPrecedingUnit(SyntaxNode block) =>
		block.Parent is GlobalStatementSyntax { Parent: { } container } && StanzaSyntax.ReadSiblings(container).IndexOf(block) > 0;

	private void Report(SyntaxNodeAnalysisContext context, SyntaxToken brace, SyntaxToken neighbor)
	{
		bool isOpenBracePair = brace.IsKind(SyntaxKind.OpenBraceToken) && neighbor.IsKind(SyntaxKind.OpenBraceToken);
		bool isCloseBracePair = brace.IsKind(SyntaxKind.CloseBraceToken) && neighbor.IsKind(SyntaxKind.CloseBraceToken);

		if (isOpenBracePair || isCloseBracePair)
			return;

		BlankLineRemoval.ReportRemovableBlankLine(context, anchor: brace, neighbor, rule: Rule);
	}
}
