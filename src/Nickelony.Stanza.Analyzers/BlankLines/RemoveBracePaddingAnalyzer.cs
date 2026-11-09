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

			// An initializer that follows a comma is an element of its list, and the list's own rules own the gap above it.
			if (!headerEnd.IsKind(SyntaxKind.None) && !headerEnd.IsKind(SyntaxKind.CommaToken))
				Report(context, openBrace, headerEnd);
		}

		ImmutableArray<SyntaxNode> units = StanzaSyntax.ReadSiblings(context.Node);

		// Whitespace between the braces is an empty block, not padding.
		if (units.Length == 0)
			return;

		// The token before the closing brace is the last unit's, or the trailing comma a list may end with.
		Report(context, openBrace, units[0].GetFirstToken());
		Report(context, closeBrace, closeBrace.GetPreviousToken());
	}

	public static bool IsStatementBlock(SyntaxNode container)
	{
		if (container is not BlockSyntax block)
			return false;

		SyntaxNode? parent = block.Parent;

		return parent switch
		{
			// The block is among its parent's statements, so a statement before it is any first statement that is not the block.
			BlockSyntax blockParent => blockParent.Statements[0] != block,
			SwitchSectionSyntax section => section.Statements[0] != block,
			GlobalStatementSyntax globalStatement => HasPrecedingUnit(globalStatement),
			_ => false,
		};
	}

	private static bool HasPrecedingUnit(GlobalStatementSyntax globalStatement)
	{
		return globalStatement.Parent is CompilationUnitSyntax unit
			&& (unit.Externs.Count > 0 || unit.Usings.Count > 0 || unit.Members[0] != globalStatement);
	}

	private void Report(SyntaxNodeAnalysisContext context, SyntaxToken brace, SyntaxToken neighbor)
	{
		bool isOpenBracePair = brace.IsKind(SyntaxKind.OpenBraceToken) && neighbor.IsKind(SyntaxKind.OpenBraceToken);
		bool isCloseBracePair = brace.IsKind(SyntaxKind.CloseBraceToken) && neighbor.IsKind(SyntaxKind.CloseBraceToken);

		if (isOpenBracePair || isCloseBracePair)
			return;

		RemovableBlankLineAnalysis.ReportRemovableBlankLine(context, anchor: brace, neighbor, rule: Rule);
	}
}
