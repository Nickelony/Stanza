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
			TupleExpressionSyntax tupleExpression => ToNodes(tupleExpression.Arguments),
			TupleTypeSyntax tupleType => ToNodes(tupleType.Elements),

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

	/// <summary>Whether the trivia is an <c>#if</c>, <c>#elif</c>, <c>#else</c> or <c>#endif</c>.</summary>
	public static bool IsConditionalDirective(SyntaxTrivia trivia)
	{
		return trivia.Kind()
			is SyntaxKind.IfDirectiveTrivia
				or SyntaxKind.ElifDirectiveTrivia
				or SyntaxKind.ElseDirectiveTrivia
				or SyntaxKind.EndIfDirectiveTrivia;
	}

	/// <summary>Whether the trivia list holds an <c>#if</c>, <c>#elif</c>, <c>#else</c> or <c>#endif</c>.</summary>
	public static bool HasConditionalDirective(SyntaxTriviaList triviaList) => triviaList.Any(IsConditionalDirective);

	/// <summary>Whether the trivia list holds a directive.</summary>
	public static bool HasDirective(SyntaxTriviaList triviaList) => triviaList.Any(static trivia => trivia.IsDirective);

	/// <summary>Whether the trivia list holds a conditional directive, or the text a directive disables.</summary>
	public static bool HasConditionalDirectiveOrDisabledText(SyntaxTriviaList triviaList) =>
		triviaList.Any(static trivia => IsConditionalDirective(trivia) || trivia.IsKind(SyntaxKind.DisabledTextTrivia));

	/// <summary>Whether the trivia list holds a directive, or the text a directive disables.</summary>
	public static bool HasDirectiveOrDisabledText(SyntaxTriviaList triviaList) =>
		triviaList.Any(static trivia => trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia));

	/// <summary>Whether a directive stands in the node, in its first token's trivia, or in the next token's.</summary>
	/// <remarks>
	/// Text a directive disables is trivia of those tokens too, so another view's branches are covered.
	/// </remarks>
	public static bool HasDirectiveNear(SyntaxNode node)
	{
		if (node.ContainsDirectives)
			return true;

		// The token is found by its position, because the next token of a list's unit is found by a pass over the list.
		SyntaxToken next = node.SyntaxTree.GetRoot().FindToken(node.FullSpan.End);

		return HasDirectiveOrDisabledText(next.LeadingTrivia);
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
		syntaxTree.Options is not CSharpParseOptions options || options.LanguageVersion >= minimum;

	public static LeadingUnit GetLeadingUnit(SyntaxNodeOrToken unit)
	{
		FileLinePositionSpan nodeLineSpan = GetLineSpan(unit);

		int startLineIndex = nodeLineSpan.StartLinePosition.Line;
		int startPosition = unit.SpanStart;

		SyntaxTriviaList leadingTrivia = unit.GetLeadingTrivia();
		bool hasDirective = HasDirective(leadingTrivia);

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

	private static ImmutableArray<SyntaxNode> ToNodes<TNode>(SyntaxList<TNode> nodes)
		where TNode : SyntaxNode
	{
		return [.. nodes];
	}

	private static ImmutableArray<SyntaxNode> ToNodes<TNode>(SeparatedSyntaxList<TNode> nodes)
		where TNode : SyntaxNode
	{
		return [.. nodes];
	}

	private static ImmutableArray<SyntaxNode> ToMemberNodes(SyntaxList<MemberDeclarationSyntax> members) =>
		[.. members.Select(member => member is GlobalStatementSyntax globalStatement ? globalStatement.Statement : (SyntaxNode)member)];

	/// <summary>
	/// Finds the node that holds the unit as a sibling, past the global statement of a top-level unit.
	/// </summary>
	public static SyntaxNode? FindContainer(SyntaxNode unit) =>
		unit.Parent is GlobalStatementSyntax globalStatement ? globalStatement.Parent : unit.Parent;

	/// <summary>The line the unit ends on, which a multi-line trailing comment pushes down.</summary>
	public static int GetEndLineIndex(SyntaxNodeOrToken unit)
	{
		SyntaxToken last = unit.IsToken ? unit.AsToken() : unit.AsNode()!.GetLastToken();
		int end = unit.Span.End;

		foreach (SyntaxTrivia trivia in last.TrailingTrivia)
		{
			if (StanzaTrivia.IsComment(trivia))
				end = trivia.Span.End;
		}

		return GetLineSpan(unit, new TextSpan(end, 0)).StartLinePosition.Line;
	}

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
