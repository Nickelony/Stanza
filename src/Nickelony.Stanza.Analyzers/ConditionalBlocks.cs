using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers;

/// <summary>Reads whether a conditional directive block stands between units.</summary>
internal static class ConditionalBlocks
{
	/// <summary>The tokens this thread's block reads have taken; a test seam for a bound without a clock.</summary>
	[ThreadStatic]
	public static long StepsTaken;

	/// <summary>Whether the directive is the <c>#if</c> or <c>#endif</c> of an active block between units.</summary>
	/// <remarks>
	/// A block inside a list keeps its gaps, as does one with a branch, active or disabled, that holds part of a unit.
	/// </remarks>
	public static bool IsBetweenUnits(DirectiveTriviaSyntax directive)
	{
		if (!directive.IsActive || directive is not (IfDirectiveTriviaSyntax or EndIfDirectiveTriviaSyntax))
			return false;

		List<DirectiveTriviaSyntax> related = directive.GetRelatedDirectives();

		if (related.Count < 2 || related[0] is not IfDirectiveTriviaSyntax || related[related.Count - 1] is not EndIfDirectiveTriviaSyntax)
			return false;

		return IsBlockStart(related[0])
			&& IsUnitBoundary(related[related.Count - 1].ParentTrivia.Token)
			&& BlocksAboveStartAtUnits(related[0])
			&& HoldsOnlyWholeUnits(related);
	}

	/// <summary>Whether the token opens a unit, ends a container or opens a switch section.</summary>
	private static bool IsUnitBoundary(SyntaxToken token) => StartsUnit(token) || EndsContainer(token) || StartsSection(token);

	/// <summary>Whether the <c>#if</c> stands at a unit boundary or at a comma that opens an enum member.</summary>
	private static bool IsBlockStart(DirectiveTriviaSyntax opener)
	{
		SyntaxToken token = opener.ParentTrivia.Token;
		return IsUnitBoundary(token) || (token.IsKind(SyntaxKind.CommaToken) && token.Parent is EnumDeclarationSyntax);
	}

	/// <summary>Whether each block directly above the <c>#if</c> starts at a unit boundary too.</summary>
	/// <remarks>
	/// Blocks with no code between them are judged as one, as a block may complete a statement head in one view only.
	/// </remarks>
	private static bool BlocksAboveStartAtUnits(DirectiveTriviaSyntax opener) =>
		GetOpenersBelowUnitBlocks(opener.SyntaxTree).Contains(opener.SpanStart);

	private static HashSet<int> GetOpenersBelowUnitBlocks(SyntaxTree syntaxTree) =>
		s_openersBelowUnitBlocks.GetOrCompute(syntaxTree, static tree => ReadOpenersBelowUnitBlocks(tree));

	/// <summary>The positions of the <c>#if</c> directives whose blocks directly above all start at units.</summary>
	/// <remarks>
	/// One pass in text order judges each <c>#if</c> from the block above it, pairing each <c>#endif</c> by a stack.
	/// </remarks>
	private static HashSet<int> ReadOpenersBelowUnitBlocks(SyntaxTree syntaxTree)
	{
		HashSet<int> openers = [];

		// The #if of each #endif, by the #endif's position, which is how the directive above an #if is found again.
		Dictionary<int, IfDirectiveTriviaSyntax> openerOfEndIf = [];
		Stack<IfDirectiveTriviaSyntax> open = new();

		// The callers pass no token, and the analyzers have read the same cached markers with theirs first.
		foreach (SyntaxTrivia trivia in StanzaCommentCache.GetConditionalBlockMarkers(syntaxTree, CancellationToken.None))
		{
			StepsTaken++;

			if (trivia.GetStructure() is EndIfDirectiveTriviaSyntax endIf)
			{
				if (open.Count > 0)
					openerOfEndIf[endIf.SpanStart] = open.Pop();

				continue;
			}

			if (trivia.GetStructure() is not IfDirectiveTriviaSyntax opener)
				continue;

			open.Push(opener);

			if (
				FindDirectiveAbove(opener) is not EndIfDirectiveTriviaSyntax above
				|| (
					openerOfEndIf.TryGetValue(above.SpanStart, out IfDirectiveTriviaSyntax? aboveOpener)
					&& IsBlockStart(aboveOpener)
					&& openers.Contains(aboveOpener.SpanStart)
				)
			)
			{
				openers.Add(opener.SpanStart);
			}
		}

		return openers;
	}

	/// <summary>The directive directly above the directive, with only layout between them.</summary>
	private static DirectiveTriviaSyntax? FindDirectiveAbove(DirectiveTriviaSyntax directive)
	{
		SyntaxTriviaList leading = directive.ParentTrivia.Token.LeadingTrivia;

		for (int index = leading.IndexOf(directive.ParentTrivia) - 1; index >= 0; index--)
		{
			if (StanzaTrivia.IsLayout(leading[index]))
				continue;

			return leading[index].GetStructure() as DirectiveTriviaSyntax;
		}

		return null;
	}

	/// <summary>Whether the token is the first of a switch section, which ends the section before it.</summary>
	private static bool StartsSection(SyntaxToken token)
	{
		return token.Parent is SwitchLabelSyntax { Parent: SwitchSectionSyntax section } label
			&& section.Labels[0] == label
			&& label.GetFirstToken() == token;
	}

	/// <summary>Whether every branch of the block, active or disabled, holds whole units.</summary>
	/// <remarks>
	/// A branch is read in text order, its disabled text and its nested blocks included.
	/// </remarks>
	private static bool HoldsOnlyWholeUnits(List<DirectiveTriviaSyntax> related)
	{
		int start = related[0].Span.End;
		int end = related[related.Count - 1].SpanStart;

		SyntaxToken first = related[0].ParentTrivia.Token;
		SyntaxToken after = related[related.Count - 1].ParentTrivia.Token;

		// A last enum member needs no comma, so a balanced branch before the enum's closing brace ends that member, whatever its last token.
		bool endsEnum = after.IsKind(SyntaxKind.CloseBraceToken) && after.Parent is EnumDeclarationSyntax;

		// A directive is leading trivia of the token after it, and the directives of the block split its branches.
		HashSet<int> edges = [.. related.Select(directive => directive.SpanStart)];
		List<SyntaxToken> branch = [];

		bool reachedFirst = false;

		// One pass over the span's tokens, as finding the next token of each from a long list again would square the cost of a block around a whole file.
		foreach (SyntaxToken token in related[0].SyntaxTree.GetRoot().DescendantTokens(TextSpan.FromBounds(first.FullSpan.Start, after.FullSpan.End)))
		{
			// The span touches the token before the block, which the pass skips.
			reachedFirst |= token == first;

			if (!reachedFirst)
				continue;

			StepsTaken++;

			if (!TryTakeLeadingTrivia(token, start, end, edges, endsEnum, branch))
				return false;

			if (token == after)
				break;

			if (!token.IsMissing && !token.IsKind(SyntaxKind.EndOfFileToken))
				branch.Add(token);
		}

		return true;
	}

	/// <summary>Adds the disabled text of the token to the branch, and judges the branch at each edge.</summary>
	private static bool TryTakeLeadingTrivia(SyntaxToken token, int start, int end, HashSet<int> edges, bool endsEnum, List<SyntaxToken> branch)
	{
		foreach (SyntaxTrivia trivia in token.LeadingTrivia)
		{
			if (trivia.SpanStart > end)
				break;

			if (trivia.SpanStart < start)
				continue;

			if (trivia.IsDirective && edges.Contains(trivia.SpanStart))
			{
				if (!IsWholeUnits(branch, endsEnum))
					return false;

				branch.Clear();
			}
			else if (trivia.IsKind(SyntaxKind.DisabledTextTrivia))
			{
				branch.AddRange(SyntaxFactory.ParseTokens(trivia.ToString()).Where(parsed => !parsed.IsKind(SyntaxKind.EndOfFileToken)));
			}
		}

		return true;
	}

	/// <summary>Whether the tokens start outside a clause, balance their brackets and braces, and end a unit.</summary>
	/// <remarks>
	/// No tokens hold whole units, and an enum member ends with a comma, or with anything when it is the last.
	/// </remarks>
	private static bool IsWholeUnits(IEnumerable<SyntaxToken> tokens, bool endsEnum)
	{
		SyntaxKind last = SyntaxKind.None;
		SyntaxKind beforeLast = SyntaxKind.None;

		int count = 0;
		bool opensWithLabel = false;

		int braces = 0;
		int parentheses = 0;
		int brackets = 0;

		foreach (SyntaxToken token in tokens)
		{
			StepsTaken++;

			SyntaxKind kind = token.Kind();

			if (kind == SyntaxKind.EndOfFileToken)
				break;

			// The first token decides whether the text continues a clause, so such text ends the read at once.
			if (last == SyntaxKind.None && kind is SyntaxKind.ElseKeyword or SyntaxKind.CatchKeyword or SyntaxKind.FinallyKeyword)
				return false;

			// A switch label in text that does not open with one splits a switch section, so such text holds no whole units.
			if (braces == 0 && parentheses == 0 && brackets == 0 && FindLabelStart(kind, last, beforeLast, index: count) is { } labelIndex)
			{
				opensWithLabel |= labelIndex == 0;

				if (!opensWithLabel)
					return false;
			}

			switch (kind)
			{
				case SyntaxKind.OpenBraceToken:
					braces++;
					break;

				case SyntaxKind.CloseBraceToken:
					braces--;
					break;

				case SyntaxKind.OpenParenToken:
					parentheses++;
					break;

				case SyntaxKind.CloseParenToken:
					parentheses--;
					break;

				case SyntaxKind.OpenBracketToken:
					brackets++;
					break;

				case SyntaxKind.CloseBracketToken:
					brackets--;
					break;
			}

			// A closer with no opener ends a unit the text did not start.
			if (braces < 0 || parentheses < 0 || brackets < 0)
				return false;

			beforeLast = last;
			last = kind;
			count++;
		}

		bool endsUnit = endsEnum || last is SyntaxKind.None or SyntaxKind.SemicolonToken or SyntaxKind.CloseBraceToken or SyntaxKind.CommaToken;

		return endsUnit && braces == 0 && parentheses == 0 && brackets == 0;
	}

	/// <summary>The index of the switch label the token reveals, or <see langword="null"/> for none.</summary>
	/// <remarks>
	/// A <c>case</c> and the colon after a <c>default</c> reveal a label, but not after a <c>goto</c> or a <c>?</c>.
	/// </remarks>
	private static int? FindLabelStart(SyntaxKind kind, SyntaxKind last, SyntaxKind beforeLast, int index)
	{
		if (kind == SyntaxKind.CaseKeyword && last != SyntaxKind.GotoKeyword)
			return index;

		if (
			kind == SyntaxKind.ColonToken
			&& last == SyntaxKind.DefaultKeyword
			&& beforeLast != SyntaxKind.QuestionToken
			&& beforeLast != SyntaxKind.GotoKeyword
		)
		{
			return index - 1;
		}

		return null;
	}

	private static bool EndsContainer(SyntaxToken token)
	{
		return token.IsKind(SyntaxKind.EndOfFileToken)
			|| (
				token.IsKind(SyntaxKind.CloseBraceToken)
				&& token.Parent is { } parent
				&& (StanzaKinds.ContainerKinds.Contains(parent.Kind()) || parent.IsKind(SyntaxKind.SwitchStatement))
			);
	}

	private static bool StartsUnit(SyntaxToken token)
	{
		for (SyntaxNode? node = token.Parent; node?.GetFirstToken(includeZeroWidth: true) == token; node = node.Parent)
		{
			if (
				node is StatementSyntax or MemberDeclarationSyntax or UsingDirectiveSyntax or ExternAliasDirectiveSyntax
				&& node.Parent is { } parent
				&& StanzaKinds.ContainerKinds.Contains(parent.Kind())
			)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Reads each run of touching blocks once per tree, so a run costs time in step with its length.</summary>
	private static readonly WeakCache<SyntaxTree, HashSet<int>> s_openersBelowUnitBlocks = new();
}
