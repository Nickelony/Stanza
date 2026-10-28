using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads the runs of single-line siblings that blank lines set apart.</summary>
internal static class BlankLineLooseRuns
{
	private const int MaxRounds = 4;

	/// <summary>A run of siblings by index, where <c>End</c> is one past the last.</summary>
	public readonly record struct LooseRun(int Start, int End);

	/// <summary>The runs that hold more siblings than the option allows.</summary>
	/// <remarks>
	/// A gap another rule asks for ends a run, as if the run were gathered, so no blank line comes back.
	/// </remarks>
	public static ImmutableArray<LooseRun> ReadRuns(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		ImmutableArray<SyntaxNode> siblings,
		StanzaRule rule
	)
	{
		int limit = options.MaxLooseStatements;

		// A run of more than `limit` siblings spans at least `limit` gaps, so a smaller container holds none.
		if (limit == 0 || siblings.Length <= limit || BlankLineOwnership.IsGapOwnedByTwoUnitRule(context, options, siblings, rule))
			return [];

		ImmutableHashSet<int> loose = ReadLooseBoundaries(context, siblings);

		if (loose.Count < limit)
			return [];

		ImmutableHashSet<int> removable = ReadRemovableBoundaries(context, options, siblings, rule, loose);

		ImmutableArray<LooseRun>.Builder runs = ImmutableArray.CreateBuilder<LooseRun>();
		int start = 0;

		for (int index = 1; index <= siblings.Length; index++)
		{
			if (index < siblings.Length && removable.Contains(index))
				continue;

			if (index - start > limit)
				runs.Add(new LooseRun(start, index));

			start = index;
		}

		return runs.ToImmutable();
	}

	/// <summary>Creates the changes that remove the blank lines from every gap of a run.</summary>
	/// <remarks>
	/// The bounds are indexes, so every gap of a run makes the same changes and a merge keeps one of each.
	/// </remarks>
	public static ImmutableArray<TextChange> CreateRemoval(
		SourceText sourceText,
		ImmutableArray<SyntaxNode> siblings,
		int runStart,
		int runEnd,
		string ruleId
	)
	{
		ImmutableArray<TextChange>.Builder changes = ImmutableArray.CreateBuilder<TextChange>();

		for (int index = runStart + 1; index < runEnd && index < siblings.Length; index++)
		{
			SyntaxToken previousToken = siblings[index - 1].GetLastToken();

			if (
				BlankLineGaps.CreateAdjacentRemoval(sourceText, previous: previousToken, next: siblings[index].GetFirstToken(), ruleId) is { } removal
			)
			{
				changes.Add(removal);
			}
		}

		return changes.ToImmutable();
	}

	/// <summary>The loose boundaries whose blank line no rule asks for once they are tight.</summary>
	/// <remarks>
	/// A tight gap that a rule would then ask for keeps the gaps of its run blank, so the fix causes no work.
	/// </remarks>
	private static ImmutableHashSet<int> ReadRemovableBoundaries(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		ImmutableArray<SyntaxNode> siblings,
		StanzaRule rule,
		ImmutableHashSet<int> loose
	)
	{
		GapOwners gapOwners = BlankLineOwnership.ReadGapOwners(context, options, rule);

		ImmutableHashSet<int> asked = ReadAskedBoundaries(context, options, siblings, rule, gapOwners);
		ImmutableHashSet<int> removable = loose;

		// A container that needs more rounds than this is left as it is, so the work stays linear.
		for (int round = 0; round < MaxRounds; round++)
		{
			ImmutableArray<BlankLineGrouping.RunBorder> borders = BlankLineOwnership.ReadGatheredGroupingBorders(
				context,
				options,
				siblings,
				rule,
				gapOwners,
				gathered: removable
			);

			ImmutableHashSet<int> staying = ReadStayingBlankBoundaries(siblings, borders, asked, removable);

			if (staying.IsEmpty)
				return removable;

			removable = removable.Except(staying);
		}

		return [];
	}

	/// <summary>The boundaries where a rule asks for a blank line whatever is gathered.</summary>
	/// <remarks>
	/// These are the runs that stand as the text has them and the gaps ZA0005, ZA0047 or ZA0053 keeps, written or not.
	/// </remarks>
	private static ImmutableHashSet<int> ReadAskedBoundaries(
		SyntaxNodeAnalysisContext context,
		StanzaOptions options,
		ImmutableArray<SyntaxNode> siblings,
		StanzaRule rule,
		GapOwners gapOwners
	)
	{
		ImmutableArray<BlankLineGrouping.RunBorder> standing = BlankLineOwnership.ReadGatheredGroupingBorders(
			context,
			options,
			siblings,
			rule,
			gapOwners,
			gathered: []
		);

		ImmutableHashSet<int>.Builder asked = ImmutableHashSet.CreateBuilder<int>();

		foreach (BlankLineGrouping.RunBorder border in standing)
			asked.Add(border.Boundary);

		for (int index = 1; index < siblings.Length; index++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (BlankLineGrouping.IsGapKeptBlank(siblings, boundaryIndex: index, gapOwners, cancellationToken: context.CancellationToken))
				asked.Add(index);
		}

		return asked.ToImmutable();
	}

	/// <summary>The removable boundaries that stay blank, since gathering them makes a rule ask for one.</summary>
	/// <remarks>
	/// A run that a rule would set apart once gathered stays blank, and the container is judged again.
	/// </remarks>
	private static ImmutableHashSet<int> ReadStayingBlankBoundaries(
		ImmutableArray<SyntaxNode> siblings,
		ImmutableArray<BlankLineGrouping.RunBorder> borders,
		ImmutableHashSet<int> asked,
		ImmutableHashSet<int> removable
	)
	{
		ImmutableHashSet<int>.Builder staying = ImmutableHashSet.CreateBuilder<int>();
		HashSet<(int Start, int End)> dissolved = [];

		foreach (BlankLineGrouping.RunBorder border in borders)
		{
			int boundary = border.Boundary;

			// A run with no gathered gap at its start or inside it stands as the text has it, so a border it lacks is not a loss.
			if (
				!removable.Contains(boundary)
				&& !asked.Contains(boundary)
				&& !BlankLineGaps.HasBlankLineBetween(siblings[boundary - 1], siblings[boundary])
				&& HasGatheredGap(removable, border.RunStart, border.RunEnd)
			)
			{
				dissolved.Add((border.RunStart, border.RunEnd));
			}
		}

		foreach ((int start, int end) in dissolved)
		{
			bool formedByInnerGap = false;

			for (int gap = start + 1; gap < end; gap++)
			{
				if (removable.Contains(gap))
				{
					staying.Add(gap);
					formedByInnerGap = true;
				}
			}

			// A run that no inner gap formed stands as the text has it, so the gap above it is the one that lost its blank line.
			if (!formedByInnerGap && removable.Contains(start))
				staying.Add(start);
		}

		foreach (BlankLineGrouping.RunBorder border in borders)
		{
			if (removable.Contains(border.Boundary) && !dissolved.Contains((border.RunStart, border.RunEnd)))
				staying.Add(border.Boundary);
		}

		return staying.ToImmutable();
	}

	/// <summary>Whether the gap above a run, or one inside it, is gathered.</summary>
	private static bool HasGatheredGap(ImmutableHashSet<int> removable, int runStart, int runEnd)
	{
		for (int gap = runStart; gap < runEnd; gap++)
		{
			if (removable.Contains(gap))
				return true;
		}

		return false;
	}

	/// <summary>The boundaries between single-line siblings whose gap holds a blank line and nothing else.</summary>
	private static ImmutableHashSet<int> ReadLooseBoundaries(SyntaxNodeAnalysisContext context, ImmutableArray<SyntaxNode> siblings)
	{
		SourceText sourceText = context.Node.SyntaxTree.GetText(context.CancellationToken);
		ImmutableHashSet<int>.Builder loose = ImmutableHashSet.CreateBuilder<int>();

		for (int index = 1; index < siblings.Length; index++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			SyntaxNode previous = siblings[index - 1];
			SyntaxNode next = siblings[index];

			if (
				!StanzaSyntax.IsMultiLine(previous)
				&& !StanzaSyntax.IsMultiLine(next)
				&& BlankLineGaps.HasBlankLineBetween(previous, next)
				&& StanzaText.IsWhitespaceOnly(
					sourceText,
					TextSpan.FromBounds(BlankLineGaps.GetGapStartToken(previous, next).Span.End, next.GetFirstToken().SpanStart)
				)
			)
			{
				loose.Add(index);
			}
		}

		return loose.ToImmutable();
	}
}
