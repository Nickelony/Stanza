using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads the jump statements that ask for a blank line above them.</summary>
internal static class BlankLineJumps
{
	/// <summary>Finds the jump statements past the first sibling, in ascending order.</summary>
	/// <remarks>
	/// The break that closes a switch section ends the section rather than a stanza, so it is not one.
	/// </remarks>
	public static ImmutableArray<int> FindJumps(ImmutableArray<SyntaxNode> siblings)
	{
		ImmutableArray<int>.Builder jumps = ImmutableArray.CreateBuilder<int>();

		for (int index = 1; index < siblings.Length; index++)
		{
			if (IsJump(siblings[index]) && !IsSectionEndBreak(siblings[index]))
				jumps.Add(index);
		}

		return jumps.ToImmutable();
	}

	/// <summary>Finds the jump statements that the jump rule is on for.</summary>
	public static ImmutableArray<int> FindJudgedJumps(ContainerContext container, StanzaRule jumpRule)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		ImmutableArray<int> found = FindJumps(siblings);

		if (found.IsEmpty)
			return found;

		ImmutableArray<int>.Builder jumps = ImmutableArray.CreateBuilder<int>();

		foreach (int index in found)
		{
			if (!StanzaRuleState.IsOffAt(scope: container, options: container.Options, rule: jumpRule, position: siblings[index].SpanStart))
				jumps.Add(index);
		}

		return jumps.ToImmutable();
	}

	/// <summary>Reads the boundaries above the jumps whose stanza spans two or more lines.</summary>
	/// <remarks>
	/// It reads the text as written, and a boundary another rule asks for ends a stanza as if its blank line stood.
	/// </remarks>
	public static ImmutableArray<int> ReadAskedJumpBoundaries(
		ImmutableArray<SyntaxNode> siblings,
		ImmutableArray<int> jumps,
		GapOwners gapOwners,
		ImmutableHashSet<int> standing,
		CancellationToken cancellationToken
	)
	{
		var stanzas = new Stanzas(siblings, jumps, gapOwners, standing, gathered: [], cancellationToken);
		ImmutableArray<int>.Builder asked = ImmutableArray.CreateBuilder<int>();

		foreach (int jump in jumps)
		{
			if (stanzas.Asks(jump))
				asked.Add(jump);
		}

		return asked.ToImmutable();
	}

	/// <summary>Reads the gathered boundaries above a jump that would ask for the blank line back.</summary>
	/// <remarks>
	/// Each stanza is read from the whole container, so one that reaches past the stretch is judged whole.
	/// </remarks>
	public static ImmutableHashSet<int> ReadStayingJumpBoundaries(
		ImmutableArray<SyntaxNode> siblings,
		ImmutableArray<int> jumps,
		GapOwners gapOwners,
		ImmutableHashSet<int> standing,
		BlankLineGrouping.GatheredStretch gathered,
		CancellationToken cancellationToken
	)
	{
		if (jumps.IsEmpty)
			return [];

		int at = jumps.BinarySearch(gathered.Start);

		if (at < 0)
			at = ~at;

		var stanzas = new Stanzas(siblings, jumps, gapOwners, standing, gathered: gathered.Boundaries, cancellationToken);
		ImmutableHashSet<int>.Builder staying = ImmutableHashSet.CreateBuilder<int>();

		for (; at < jumps.Length && jumps[at] < gathered.End; at++)
		{
			if (gathered.Boundaries.Contains(jumps[at]) && stanzas.Asks(jumps[at]))
				staying.Add(jumps[at]);
		}

		return staying.ToImmutable();
	}

	/// <summary>Whether the statement is a jump, which a guard clause also ends with.</summary>
	public static bool IsJump(SyntaxNode node)
	{
		return node.Kind()
			is SyntaxKind.ReturnStatement
				or SyntaxKind.BreakStatement
				or SyntaxKind.ContinueStatement
				or SyntaxKind.ThrowStatement
				or SyntaxKind.GotoStatement
				or SyntaxKind.GotoCaseStatement
				or SyntaxKind.GotoDefaultStatement
				or SyntaxKind.YieldBreakStatement;
	}

	/// <summary>Whether the statement is the <c>break</c> that closes a switch section.</summary>
	/// <remarks>
	/// It stands in the section, or ends the block that is the section's only statement.
	/// </remarks>
	private static bool IsSectionEndBreak(SyntaxNode statement)
	{
		if (statement is not BreakStatementSyntax)
			return false;

		return statement.Parent switch
		{
			SwitchSectionSyntax => true,

			BlockSyntax { Parent: SwitchSectionSyntax section } block => section.Statements.Count == 1
				&& block.Statements[block.Statements.Count - 1] == statement,

			_ => false,
		};
	}

	/// <summary>Reads whether the stanza above a jump spans two or more lines.</summary>
	/// <remarks>
	/// A gathered gap counts as tight, so it puts the sibling below it on a line of its own.
	/// </remarks>
	private sealed class Stanzas(
		ImmutableArray<SyntaxNode> siblings,
		ImmutableArray<int> jumps,
		GapOwners gapOwners,
		ImmutableHashSet<int> standing,
		ImmutableHashSet<int> gathered,
		CancellationToken cancellationToken
	)
	{
		/// <summary>
		/// A jump asks by the stanza above it, so each jump added to a run doubles the time without this cache.
		/// </summary>
		private readonly Dictionary<int, Reach> _reaches = [];

		/// <summary>Whether the jump at the boundary asks for a blank line, whether or not one stands there.</summary>
		public bool Asks(int boundary) => jumps.BinarySearch(boundary) >= 0 && SpansTwoLines(boundary);

		/// <summary>Whether the siblings above the boundary that no gap sets apart span two or more lines.</summary>
		private bool SpansTwoLines(int boundary)
		{
			Reach reach = GetReach(boundary - 1);
			int endLine = StanzaSyntax.GetLineSpan(siblings[boundary - 1]).EndLinePosition.Line;

			// The siblings start in order, so the first of the stanza starts on the earliest line.
			return endLine > StanzaSyntax.GetLineSpan(siblings[reach.First]).StartLinePosition.Line || reach.LastGathered > reach.First;
		}

		/// <summary>Gets the stanza that ends at the sibling.</summary>
		/// <remarks>
		/// The read works upward from the nearest gap that needs no jump, so a run of jumps costs no call stack.
		/// </remarks>
		private Reach GetReach(int siblingIndex)
		{
			if (_reaches.TryGetValue(siblingIndex, out Reach reach))
				return reach;

			Stack<int> pending = new();
			int at = siblingIndex;

			while (!_reaches.TryGetValue(at, out reach))
			{
				cancellationToken.ThrowIfCancellationRequested();

				if (at < 1 || IsGapSetApart(at))
				{
					reach = new Reach(First: at, LastGathered: at >= 1 && gathered.Contains(at) ? at : -1);
					_reaches[at] = reach;

					break;
				}

				pending.Push(at);
				at--;
			}

			while (pending.Count > 0)
			{
				int next = pending.Pop();

				reach = new Reach(First: Asks(next) ? next : reach.First, LastGathered: gathered.Contains(next) ? next : reach.LastGathered);
				_reaches[next] = reach;
			}

			return reach;
		}

		/// <summary>Whether a blank line, a rule or a directive ends the stanza at the gap, not a jump.</summary>
		/// <remarks>
		/// A stanza holds statements, so an import above the first of them ends it too.
		/// </remarks>
		private bool IsGapSetApart(int boundary)
		{
			if (siblings[boundary - 1] is UsingDirectiveSyntax or ExternAliasDirectiveSyntax)
				return true;

			return (!gathered.Contains(boundary) && BlankLineGaps.HasBlankLineBetween(siblings[boundary - 1], siblings[boundary]))
				|| standing.Contains(boundary)
				|| BlankLineGrouping.IsGapKeptBlank(siblings, boundary, gapOwners, cancellationToken)
				|| BlankLineGaps.HasDirectiveBefore(siblings[boundary]);
		}

		/// <summary>The stanza that ends at a sibling: where it starts, and its last gathered gap.</summary>
		private readonly record struct Reach(int First, int LastGathered);
	}
}
