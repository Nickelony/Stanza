using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers;

internal static class StanzaSyntax
{
	public static ImmutableArray<SyntaxNode> ReadSiblings(SyntaxNode container)
	{
		return container switch
		{
			BlockSyntax block => ToNodes(block.Statements),
			SwitchSectionSyntax switchSection => ToNodes(switchSection.Statements),
			SwitchStatementSyntax switchStatement => ToNodes(switchStatement.Sections),
			SwitchExpressionSyntax switchExpression => ToNodes(switchExpression.Arms),
			InitializerExpressionSyntax initializer => ToNodes(initializer.Expressions),
			CollectionExpressionSyntax collectionExpression => ToNodes(collectionExpression.Elements),
			AnonymousObjectCreationExpressionSyntax anonymousObject => ToNodes(anonymousObject.Initializers),
			ArgumentListSyntax argumentList => ToNodes(argumentList.Arguments),
			AttributeArgumentListSyntax attributeArgumentList => ToNodes(attributeArgumentList.Arguments),
			BracketedArgumentListSyntax bracketedArgumentList => ToNodes(bracketedArgumentList.Arguments),
			ParameterListSyntax parameterList => ToNodes(parameterList.Parameters),
			BracketedParameterListSyntax bracketedParameterList => ToNodes(bracketedParameterList.Parameters),

			// Assembly-level attribute lists are deliberately absent:
			// `[assembly: ...]` states something about the assembly rather than adding a member to the file,
			// so no rule judges the gap around it.
			CompilationUnitSyntax compilationUnit => ToNodes(compilationUnit.Externs)
				.AddRange(ToNodes(compilationUnit.Usings))
				.AddRange(ToMemberNodes(compilationUnit.Members)),

			BaseNamespaceDeclarationSyntax namespaceDeclaration => ToNodes(namespaceDeclaration.Externs)
				.AddRange(ToNodes(namespaceDeclaration.Usings))
				.AddRange(ToMemberNodes(namespaceDeclaration.Members)),

			TypeDeclarationSyntax typeDeclaration => ToNodes(typeDeclaration.Members),
			EnumDeclarationSyntax enumDeclaration => ToNodes(enumDeclaration.Members),
			AccessorListSyntax accessorList => ToNodes(accessorList.Accessors),
			VariableDeclarationSyntax variableDeclaration => ToNodes(variableDeclaration.Variables),
			_ => [],
		};
	}

	public static bool IsMultiLine(SyntaxNodeOrToken unit) => unit.SyntaxTree is { } syntaxTree && IsMultiLine(syntaxTree, unit.Span);

	public static bool IsMultiLine(SourceText sourceText, TextSpan span)
	{
		TextLineCollection lines = sourceText.Lines;
		return lines.GetLineFromPosition(span.Start).LineNumber != lines.GetLineFromPosition(span.End).LineNumber;
	}

	public static bool IsMultiLine(SyntaxTree? syntaxTree, TextSpan span)
	{
		if (syntaxTree is null)
			return false;

		FileLinePositionSpan lineSpan = syntaxTree.GetLineSpan(span);
		return lineSpan.StartLinePosition.Line != lineSpan.EndLinePosition.Line;
	}

	/// <summary>Whether the tree's language version is at least the given one.</summary>
	public static bool SupportsLanguage(SyntaxTree syntaxTree, LanguageVersion minimum) =>
		syntaxTree.Options is not CSharpParseOptions options || options.LanguageVersion.MapSpecifiedToEffectiveVersion() >= minimum;

	private static bool TriviaContainsDirective(SyntaxTriviaList triviaList)
	{
		foreach (SyntaxTrivia trivia in triviaList)
		{
			if (trivia.IsDirective)
				return true;
		}

		return false;
	}

	public static LeadingUnit GetLeadingUnit(SyntaxNodeOrToken unit)
	{
		FileLinePositionSpan nodeLineSpan = GetLineSpan(unit);

		int startLineIndex = nodeLineSpan.StartLinePosition.Line;
		int startPosition = unit.SpanStart;

		SyntaxTriviaList leadingTrivia = unit.GetLeadingTrivia();
		bool hasDirective = TriviaContainsDirective(leadingTrivia);

		// The comment run directly above the unit travels with it;
		// a comment more than one line above stops the run.
		// A documentation comment documents the unit across any number of blank lines, as the compiler reads it,
		// so it travels with the unit wherever it stands.
		for (int index = leadingTrivia.Count - 1; index >= 0; index--)
		{
			SyntaxTrivia trivia = leadingTrivia[index];

			if (!StanzaTrivia.IsComment(trivia))
				continue;

			FileLinePositionSpan commentLineSpan = GetLineSpan(unit, trivia.Span);
			int commentEndLineIndex = commentLineSpan.EndLinePosition.Line;

			if (!StanzaTrivia.IsDocumentationComment(trivia) && startLineIndex - commentEndLineIndex > 1)
				break;

			startLineIndex = commentLineSpan.StartLinePosition.Line;

			// For a documentation comment, the first `///` lies outside the trivia's own span,
			// as the leading trivia of the structure's first token,
			// so the full span is what has to be measured.
			startPosition = trivia.FullSpan.Start;
		}

		return new LeadingUnit(startPosition, startLineIndex, hasDirective);
	}

	public static bool HasModifier(SyntaxTokenList modifiers, SyntaxKind kind)
	{
		foreach (SyntaxToken modifier in modifiers)
		{
			if (modifier.IsKind(kind))
				return true;
		}

		return false;
	}

	private static ImmutableArray<SyntaxNode> ToNodes<TNode>(IEnumerable<TNode> nodes)
		where TNode : SyntaxNode
	{
		return [.. nodes.Cast<SyntaxNode>()];
	}

	private static ImmutableArray<SyntaxNode> ToMemberNodes(SyntaxList<MemberDeclarationSyntax> members) =>
		[.. members.Select(member => member is GlobalStatementSyntax globalStatement ? globalStatement.Statement : (SyntaxNode)member)];

	public static FileLinePositionSpan GetLineSpan(SyntaxNodeOrToken unit) => GetLineSpan(unit, unit.Span);

	/// <summary>
	/// A detached unit has no tree, so its span reports line 0 and a blank-line change lands at the top.
	/// </summary>
	public static FileLinePositionSpan GetLineSpan(SyntaxNodeOrToken unit, TextSpan span)
	{
		return unit.SyntaxTree is { } syntaxTree
			? syntaxTree.GetLineSpan(span)
			: new FileLinePositionSpan(string.Empty, new LinePositionSpan(default, default));
	}

	public readonly record struct LeadingUnit(int StartPosition, int StartLineIndex, bool HasDirective);
}
