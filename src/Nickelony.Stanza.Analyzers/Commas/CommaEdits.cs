using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.Commas;

/// <summary>Builds the edits that write or remove the comma after the last element of a list.</summary>
internal static class CommaEdits
{
	public static CommaEdit? CreateAddEdit(SyntaxNode list)
	{
		if (ReadEnd(list) is not { Comma: null } end)
			return null;

		return new CommaEdit(new TextChange(new TextSpan(end.LastToken.Span.End, 0), ","), end.LastToken.Span);
	}

	public static CommaEdit? CreateRemoveEdit(SyntaxNode list)
	{
		if (ReadEnd(list) is not { Comma: { } comma } end)
			return null;

		// A comma on a line of its own is not the trailing comma of the element, so it stays.
		if (GetEndLine(end.LastToken) != GetStartLine(comma))
			return null;

		// The blanks before the comma go with it, and a comment before it stays.
		// A comment that follows the comma with no blank keeps the blank before the comma to stand apart from the element.
		SourceText sourceText = comma.SyntaxTree!.GetText();

		int start = StartsWithComment(comma.TrailingTrivia)
			? comma.Span.Start
			: StanzaText.SkipBlanksBackward(sourceText, comma.Span.Start, limit: end.LastToken.Span.End);

		return new CommaEdit(new TextChange(TextSpan.FromBounds(start, comma.Span.End), string.Empty), comma.Span);
	}

	public static List<CommaEdit> CreateAddEdits(SyntaxNode root, CancellationToken cancellationToken) =>
		CreateEdits(root, CreateAddEdit, cancellationToken);

	public static List<CommaEdit> CreateRemoveEdits(SyntaxNode root, CancellationToken cancellationToken) =>
		CreateEdits(root, CreateRemoveEdit, cancellationToken);

	private static List<CommaEdit> CreateEdits(SyntaxNode root, Func<SyntaxNode, CommaEdit?> create, CancellationToken cancellationToken)
	{
		List<CommaEdit> edits = [];

		foreach (SyntaxNode node in root.DescendantNodes())
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (StanzaKinds.TrailingCommaKinds.Contains(node.Kind()) && create(node) is { } edit)
				edits.Add(edit);
		}

		return edits;
	}

	/// <summary>Reads the end of a list whose closing token stands on a later line than its last element.</summary>
	private static ListEnd? ReadEnd(SyntaxNode list)
	{
		(SyntaxNodeOrTokenList items, SyntaxToken close) = ReadItems(list);

		if (items.Count == 0 || close.IsMissing || list.ContainsDiagnostics)
			return null;

		SyntaxNodeOrToken last = items[items.Count - 1];
		SyntaxToken? comma = last.IsToken ? last.AsToken() : null;

		// A list of nothing but a comma has no element to end.
		if (comma is not null && items.Count == 1)
			return null;

		SyntaxNodeOrToken element = comma is null ? last : items[items.Count - 2];

		if (!element.IsNode)
			return null;

		SyntaxToken lastToken = element.AsNode()!.GetLastToken();

		if (GetEndLine(comma ?? lastToken) >= GetStartLine(close))
			return null;

		// A conditional directive after the last element makes the last element, and the comma after it, differ by view.
		SyntaxTriviaList?[] around = [lastToken.TrailingTrivia, comma?.LeadingTrivia, comma?.TrailingTrivia, close.LeadingTrivia];

		if (around.Any(static list => list is { } present && StanzaSyntax.HasConditionalDirectiveOrDisabledText(present)))
			return null;

		return new ListEnd(lastToken, comma);
	}

	private static (SyntaxNodeOrTokenList Items, SyntaxToken Close) ReadItems(SyntaxNode list)
	{
		return list switch
		{
			InitializerExpressionSyntax initializer => (initializer.Expressions.GetWithSeparators(), initializer.CloseBraceToken),
			AnonymousObjectCreationExpressionSyntax anonymous => (anonymous.Initializers.GetWithSeparators(), anonymous.CloseBraceToken),
			CollectionExpressionSyntax collection => (collection.Elements.GetWithSeparators(), collection.CloseBracketToken),
			EnumDeclarationSyntax enumeration => (enumeration.Members.GetWithSeparators(), enumeration.CloseBraceToken),
			ListPatternSyntax listPattern => (listPattern.Patterns.GetWithSeparators(), listPattern.CloseBracketToken),
			PropertyPatternClauseSyntax properties => (properties.Subpatterns.GetWithSeparators(), properties.CloseBraceToken),
			SwitchExpressionSyntax switchExpression => (switchExpression.Arms.GetWithSeparators(), switchExpression.CloseBraceToken),
			_ => (default, default),
		};
	}

	private static bool StartsWithComment(SyntaxTriviaList list) => list.Count > 0 && StanzaTrivia.IsComment(list[0]);
	private static int GetStartLine(SyntaxToken token) => token.SyntaxTree!.GetLineSpan(token.Span).StartLinePosition.Line;
	private static int GetEndLine(SyntaxToken token) => token.SyntaxTree!.GetLineSpan(token.Span).EndLinePosition.Line;

	/// <summary>The last token of the last element, and the comma after it when there is one.</summary>
	private readonly record struct ListEnd(SyntaxToken LastToken, SyntaxToken? Comma);
}
