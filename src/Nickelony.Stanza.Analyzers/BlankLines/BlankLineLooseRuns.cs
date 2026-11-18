using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads the runs of single-line siblings that blank lines set apart.</summary>
internal static class BlankLineLooseRuns
{
	private static int s_thirdRounds;

	/// <summary>The containers that needed a third round since the last discard; a test seam.</summary>
	public static int ThirdRounds => Volatile.Read(ref s_thirdRounds);

	/// <summary>Sets the count of <see cref="ThirdRounds"/> back to zero; a test seam.</summary>
	public static void DiscardThirdRounds() => Volatile.Write(ref s_thirdRounds, 0);

	/// <summary>A run of siblings by index, where <c>End</c> is one past the last.</summary>
	public readonly record struct LooseRun(int Start, int End);

	/// <summary>The siblings a rule judges, from <c>First</c> to one before <c>End</c>.</summary>
	/// <remarks>
	/// The grouping rules read the whole container, so they ask for blank lines beside a unit outside it too.
	/// </remarks>
	public readonly record struct UnitRange(int First, int End)
	{
		public int Count => End - First;

		/// <summary>Whether the boundary stands between two units.</summary>
		public bool HoldsBoundary(int boundary) => boundary > First && boundary < End;
	}

	/// <summary>The runs of more units than the option allows, by index among the siblings of the container.</summary>
	/// <remarks>
	/// A gap another rule asks for ends a run, as if the run were gathered, so no blank line comes back.
	/// </remarks>
	public static ImmutableArray<LooseRun> ReadRuns(ContainerContext container, StanzaRule rule, bool judgesMembers)
	{
		int limit = container.Options.MaxLooseRunSize;

		if (limit == 0 || FindLooseUnits(container, rule, judgesMembers, limit) is not { } read)
			return [];

		UnitRange unitRange = read.UnitRange;
		ImmutableHashSet<int> loose = read.Loose;

		// The keep rule joins its runs first and writes the blank lines around them, so a container it has work in waits for it.
		if (HasKeptRun(container, rule, judgesMembers))
			return [];

		(ImmutableHashSet<int> removable, int rounds) = ReadRemovableBoundaries(container, unitRange, rule, loose);

		if (rounds > 2)
			Interlocked.Increment(ref s_thirdRounds);

		return ReadRunsOfBoundaries(unitRange, removable, limit);
	}

	/// <summary>The units a rule judges, with the gaps among them that hold a blank line and nothing else.</summary>
	public readonly record struct LooseUnits(UnitRange UnitRange, ImmutableHashSet<int> Loose);

	/// <summary>Finds the units and loose gaps a rule judges, or none in a container too small for a run.</summary>
	/// <remarks>
	/// A run of more than <paramref name="limit"/> units spans that many gaps, so a smaller container holds none.
	/// </remarks>
	public static LooseUnits? FindLooseUnits(ContainerContext container, StanzaRule rule, bool judgesMembers, int limit)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		UnitRange unitRange = ReadUnitRange(siblings, judgesMembers);

		// The two-unit rule counts every sibling of the container, imports and the other kind of unit included.
		if (unitRange.Count <= limit || BlankLineOwnership.IsGapOwnedByTwoUnitRule(container, rule))
			return null;

		ImmutableHashSet<int> loose = ReadLooseBoundaries(container, unitRange);

		// A conditional statement gives each view runs of its own, so no gap of the container can be judged for all of them.
		if (loose.Count < limit || BlankLineDirectives.HoldsConditionalDirective([.. siblings.Skip(unitRange.First).Take(unitRange.Count)]))
			return null;

		return new LooseUnits(unitRange, loose);
	}

	/// <summary>Reads the runs the gaps of the set join, each of over <paramref name="limit"/> units.</summary>
	public static ImmutableArray<LooseRun> ReadRunsOfBoundaries(UnitRange unitRange, ImmutableHashSet<int> boundaries, int limit)
	{
		ImmutableArray<LooseRun>.Builder runs = ImmutableArray.CreateBuilder<LooseRun>();
		int start = unitRange.First;

		for (int boundary = unitRange.First + 1; boundary <= unitRange.End; boundary++)
		{
			if (boundary < unitRange.End && boundaries.Contains(boundary))
				continue;

			if (boundary - start > limit)
				runs.Add(new LooseRun(start, boundary));

			start = boundary;
		}

		return runs.ToImmutable();
	}

	/// <summary>Whether the keep rule is on and reports a run in the container.</summary>
	/// <remarks>
	/// A run whose every gap a pragma silences reports nothing, so it holds nothing back.
	/// </remarks>
	private static bool HasKeptRun(ContainerContext container, StanzaRule rule, bool judgesMembers)
	{
		DiagnosticDescriptor target = judgesMembers
			? DiagnosticDescriptors.KeepRunOfMembersTogether
			: DiagnosticDescriptors.KeepRunOfStatementsTogether;

		if (!rule.Defers(target))
			return false;

		return !BlankLineKeptRuns.ReadReportedRuns(container, keepRule: rule.GetDeferral(target), judgesMembers).Runs.IsEmpty;
	}

	/// <summary>Reads the first stretch of statements or members, since imports are no unit.</summary>
	/// <remarks>
	/// A top-level statement among the members is a compile error, and the units after it are not judged.
	/// </remarks>
	public static UnitRange ReadUnitRange(ImmutableArray<SyntaxNode> siblings, bool judgesMembers)
	{
		int first = 0;

		while (first < siblings.Length && !IsUnit(siblings[first], judgesMembers))
			first++;

		int end = first;

		while (end < siblings.Length && IsUnit(siblings[end], judgesMembers))
			end++;

		return new UnitRange(first, end);
	}

	private static bool IsUnit(SyntaxNode sibling, bool judgesMembers) =>
		judgesMembers ? sibling is MemberDeclarationSyntax : sibling is StatementSyntax;

	/// <summary>Creates the changes that remove a run's blank lines and write those at its boundaries.</summary>
	/// <remarks>
	/// The bounds are indexes, so every gap of a run makes the same changes and a merge keeps one of each.
	/// </remarks>
	public static ImmutableArray<TextChange> CreateRemoval(
		SourceText sourceText,
		ImmutableArray<SyntaxNode> siblings,
		int runStart,
		int runEnd,
		string ruleId,
		ImmutableArray<int> boundaries,
		int indentStepColumns
	)
	{
		ImmutableArray<TextChange>.Builder changes = ImmutableArray.CreateBuilder<TextChange>();

		for (int boundary = runStart + 1; boundary < runEnd && boundary < siblings.Length; boundary++)
		{
			SyntaxToken previousToken = siblings[boundary - 1].GetLastToken();

			if (
				BlankLineGaps.CreateAdjacentRemoval(sourceText, previous: previousToken, next: siblings[boundary].GetFirstToken(), ruleId) is
				{ } removal
			)
			{
				changes.Add(removal);
			}
		}

		// A listed boundary is a gap that stays tight in the text, so its change never overlaps a removal.
		// The grouping rules write their blank lines with this insertion too, so a boundary splits a line two units share as they do.
		foreach (int boundary in boundaries.IsDefault ? [] : boundaries)
		{
			if (
				boundary > 0
				&& boundary < siblings.Length
				&& SiblingGaps.CreateSiblingInsertion(sourceText, previous: siblings[boundary - 1], next: siblings[boundary], indentStepColumns)
					is { } blankLine
			)
			{
				changes.Add(ToReplacement(sourceText, blankLine));
			}
		}

		return [.. changes.OrderBy(static change => change.Span.Start)];
	}

	/// <summary>Turns an insertion into a replacement of the line break or the character before it.</summary>
	/// <remarks>
	/// A merge keeps one of identical replacements but every insertion; inside a line, the text follows the first unit.
	/// </remarks>
	private static TextChange ToReplacement(SourceText sourceText, TextChange change)
	{
		if (change.Span.Length > 0)
			return change;

		int position = change.Span.Start;
		TextLine line = sourceText.Lines.GetLineFromPosition(position);

		TextSpan replaced =
			position == line.Start ? TextSpan.FromBounds(sourceText.Lines[line.LineNumber - 1].End, line.Start) : new TextSpan(position - 1, 1);

		return new TextChange(replaced, sourceText.ToString(replaced) + change.NewText);
	}

	/// <summary>The loose boundaries away from a stanza whose blank line no rule asks for once tight.</summary>
	/// <remarks>
	/// A tight gap that a rule would then ask for keeps the gaps of its run blank, so the fix causes no work.
	/// </remarks>
	private static (ImmutableHashSet<int> Removable, int Rounds) ReadRemovableBoundaries(
		ContainerContext container,
		UnitRange unitRange,
		StanzaRule rule,
		ImmutableHashSet<int> loose
	)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		GapOwners gapOwners = BlankLineOwnership.ReadGapOwners(container, rule);

		ImmutableArray<int> jumps = ReadDeferredJumps(container, rule);
		int guardBoundary = BlankLineGuards.FindAskedGuardBoundary(container, askingRule: rule);

		ImmutableArray<BlankLineGrouping.RunBorder> standing = BlankLineOwnership.ReadGatheredGroupingBorders(
			container,
			rule,
			gapOwners,
			gathered: null
		);

		// A border that its rule holds for the gap above goes once another fix writes that gap, so it reads tight.
		(ImmutableHashSet<int> pending, ImmutableHashSet<int> waiting) = ReadPendingBoundaries(siblings, standing);

		ImmutableHashSet<int> asked = ReadAllAskedBoundaries(
			container,
			rule,
			gapOwners,
			jumps,
			guardBoundary,
			standing: standing.Where(border => !waiting.Contains(border.Boundary))
		);

		ImmutableHashSet<int> candidates =
		[
			.. ReadCandidateBoundaries(siblings, unitRange, loose, asked, joined: gapOwners.JoinedBoundaries ?? [])
				.Except(gapOwners.ProtectedSections ?? [])
				.Where(boundary => !IsReceiverKept(container, rule, boundary) && !IsDependentBindingKept(container, rule, boundary)),
		];

		ImmutableHashSet<int> removable = candidates;

		// A gap a keeper joins goes whoever else owns it, so it never stays.
		int[] owned =
		[
			.. (gapOwners.BaseCallBoundaries ?? [])
				.Union(gapOwners.CheckedBoundaries ?? [])
				.Union(gapOwners.ReceiverBoundaries ?? [])
				.Union(gapOwners.DisplacedBoundaries ?? [])
				.Except(gapOwners.JoinedBoundaries ?? [])
				.OrderBy(static boundary => boundary),
		];

		// A retry reads a stretch of a few siblings, so it reads only the owned gaps inside it.
		IEnumerable<int> ReadOwnedIn(BlankLineGrouping.GatheredStretch gathered)
		{
			int at = Array.BinarySearch(owned, gathered.Start);

			for (at = at < 0 ? ~at : at; at < owned.Length && owned[at] <= gathered.End; at++)
			{
				if (gathered.Boundaries.Contains(owned[at]))
					yield return owned[at];
			}
		}

		// A gap that stays only for a border its rule then holds for a blank line another fix writes above it is gathered,
		// since that blank line ends the run that asks for the border.
		ImmutableHashSet<int> ReadStaying(BlankLineGrouping.GatheredStretch gathered)
		{
			(ImmutableHashSet<int> staying, ImmutableArray<BlankLineGrouping.RunBorder> borders) = ReadStayingAsGathered(gathered);
			ILookup<int, BlankLineGrouping.RunBorder>? lookup = null;

			foreach (int boundary in staying)
			{
				if (!gathered.Boundaries.Contains(boundary) || !pending.Contains(boundary - 1))
					continue;

				lookup ??= borders.ToLookup(static border => border.Boundary);

				if (
					RequiredBlankLineAnalysis.IsHeldByAnotherRule(lookup, siblings, boundary)
					&& !ReadStayingAsGathered(gathered with { Written = [boundary - 1] }).Staying.Contains(boundary)
				)
				{
					staying = staying.Remove(boundary);
				}
			}

			return staying;
		}

		(ImmutableHashSet<int> Staying, ImmutableArray<BlankLineGrouping.RunBorder> Borders) ReadStayingAsGathered(
			BlankLineGrouping.GatheredStretch gathered
		)
		{
			ImmutableArray<BlankLineGrouping.RunBorder> borders = BlankLineOwnership.ReadGatheredGroupingBorders(
				container,
				rule,
				gapOwners,
				gathered
			);

			// A jump asks for the blank line above it once the gathered gaps make its stanza two lines long.
			ImmutableHashSet<int> staying = ReadStayingBlankBoundaries(siblings, borders, asked, removable: gathered.Boundaries)
				.Union(
					BlankLineJumps.ReadStayingJumpBoundaries(
						siblings,
						jumps,
						gapOwners,
						standing: asked,
						gathered,
						cancellationToken: container.CancellationToken
					)
				);

			// The guards and the base calls stay what they are when the gaps are gathered, so the gaps beside them are asked for whatever else is.
			if (guardBoundary >= 0 && gathered.Boundaries.Contains(guardBoundary))
				staying = staying.Add(guardBoundary);

			return (staying.Union(ReadOwnedIn(gathered)), borders);
		}

		ImmutableHashSet<int> ReadStayingAround(int boundary, ImmutableHashSet<int> gathered)
		{
			BlankLineGrouping.GatheredStretch stretch = BlankLineGrouping.ReadStretchAround(
				siblings,
				options: container.Options,
				gapOwners,
				gathered,
				boundary,
				cancellationToken: container.CancellationToken
			);

			return ReadStaying(stretch);
		}

		// Each read takes only the gathered gaps, so what stays is among the removable ones and each round that holds one back shrinks them.
		// So the loop ends in at most one round per candidate gap plus the last, and no input is known to need a third.
		// A gap held back is blank in the text, so it ends a run as a border and the second round holds nothing back.
		for (int round = 1; ; round++)
		{
			ImmutableHashSet<int> staying = ReadStaying(
				new BlankLineGrouping.GatheredStretch(removable, Start: 0, End: siblings.Length)
			);

			if (staying.IsEmpty)
				return (ReadRegatheredBoundaries(candidates, removable, jumps, readStayingAround: ReadStayingAround), round);

			removable = removable.Except(staying);
		}
	}

	/// <summary>The removable boundaries, with each held-back one added again while no rule asks for it.</summary>
	/// <remarks>
	/// A gap held back for one that then stayed blank is gathered now, so no run waits for a second pass.
	/// </remarks>
	private static ImmutableHashSet<int> ReadRegatheredBoundaries(
		ImmutableHashSet<int> candidates,
		ImmutableHashSet<int> removable,
		ImmutableArray<int> jumps,
		Func<int, ImmutableHashSet<int>, ImmutableHashSet<int>> readStayingAround
	)
	{
		// Nothing stays among the removable gaps, so a retry can only lose the runs beside the gap it adds, and it reads only those.
		// A first gathered gap changes how each lone declaration reads, but a border that far off touches no gathered gap, so it holds none back.
		foreach (int boundary in candidates.Except(removable).OrderBy(held => held))
		{
			ImmutableHashSet<int> trial = removable.Add(boundary);
			ImmutableHashSet<int> staying = readStayingAround(boundary, trial);

			// One fix at a time settles a gap before the jump below it, whose stanza then reads that gap as it stands.
			// So a jump below that asks only once the gap is gathered keeps its own blank line, and the gap is gathered.
			if (!staying.IsEmpty && staying.All(below => below > boundary && jumps.BinarySearch(below) >= 0))
			{
				trial = trial.Except(staying);
				staying = readStayingAround(boundary, trial);
			}

			if (staying.IsEmpty)
				removable = trial;
		}

		return removable;
	}

	/// <summary>The loose boundaries that no statement of a tight stanza stands beside.</summary>
	/// <remarks>
	/// A statement tight against a neighbor belongs to that stanza, so the blank line that sets the stanza apart stays.
	/// </remarks>
	private static ImmutableHashSet<int> ReadCandidateBoundaries(
		ImmutableArray<SyntaxNode> siblings,
		UnitRange unitRange,
		ImmutableHashSet<int> loose,
		ImmutableHashSet<int> asked,
		ImmutableHashSet<int> joined
	)
	{
		ImmutableHashSet<int>.Builder kept = ImmutableHashSet.CreateBuilder<int>();

		foreach (int boundary in loose)
		{
			if (
				!IsTightGap(siblings, unitRange, asked, joined, boundary: boundary - 1)
				&& !IsTightGap(siblings, unitRange, asked, joined, boundary: boundary + 1)
			)
			{
				kept.Add(boundary);
			}
		}

		return kept.ToImmutable();
	}

	/// <summary>Whether the gap at the boundary has no blank line and no rule asks for one.</summary>
	private static bool IsTightGap(
		ImmutableArray<SyntaxNode> siblings,
		UnitRange unitRange,
		ImmutableHashSet<int> asked,
		ImmutableHashSet<int> joined,
		int boundary
	)
	{
		// A gap a keeper joins is tight once it fixes it, so it reads tight before that fix too.
		return unitRange.HoldsBoundary(boundary)
			&& !asked.Contains(boundary)
			&& (joined.Contains(boundary) || !BlankLineGaps.HasBlankLineBetween(siblings[boundary - 1], siblings[boundary]));
	}

	/// <summary>The boundaries where a rule asks for a blank line whatever is gathered.</summary>
	/// <remarks>
	/// The held boundaries, the standing runs and the jump gaps.
	/// </remarks>
	private static ImmutableHashSet<int> ReadAllAskedBoundaries(
		ContainerContext container,
		StanzaRule rule,
		GapOwners gapOwners,
		ImmutableArray<int> jumps,
		int guardBoundary,
		IEnumerable<BlankLineGrouping.RunBorder> standing
	)
	{
		var asked = ReadHeldBoundaries(container, rule, gapOwners, guardBoundary).ToBuilder();

		foreach (BlankLineGrouping.RunBorder border in standing)
			asked.Add(border.Boundary);

		// The stanza above a jump ends at every other gap asked for, so these are read last.
		asked.UnionWith(
			BlankLineJumps.ReadAskedJumpBoundaries(
				siblings: container.Siblings,
				jumps,
				gapOwners,
				standing: asked.ToImmutable(),
				cancellationToken: container.CancellationToken
			)
		);

		return asked.ToImmutable();
	}

	/// <summary>Splits the tight borders asked for now into those a fix writes and those held.</summary>
	/// <remarks>
	/// A held border waits for another rule's blank line above it, which ends the run that asks for it.
	/// </remarks>
	private static (ImmutableHashSet<int> Pending, ImmutableHashSet<int> Waiting) ReadPendingBoundaries(
		ImmutableArray<SyntaxNode> siblings,
		ImmutableArray<BlankLineGrouping.RunBorder> standing
	)
	{
		ImmutableHashSet<int>.Builder pending = ImmutableHashSet.CreateBuilder<int>();
		ImmutableHashSet<int>.Builder waiting = ImmutableHashSet.CreateBuilder<int>();

		ILookup<int, BlankLineGrouping.RunBorder>? asked = null;

		foreach (int boundary in standing.Select(static border => border.Boundary).Distinct())
		{
			if (!BlankLineGaps.IsGapMissingBlankLine(siblings[boundary - 1], siblings[boundary]))
				continue;

			asked ??= standing.ToLookup(static border => border.Boundary);

			if (RequiredBlankLineAnalysis.IsHeldForGapAbove(asked, siblings, boundary))
				waiting.Add(boundary);
			else
				pending.Add(boundary);
		}

		return (pending.ToImmutable(), waiting.ToImmutable());
	}

	/// <summary>The boundaries where a rule keeps a blank line, whether the runs stand or are gathered.</summary>
	/// <remarks>
	/// The gaps the owning rules keep blank, and the guard gap.
	/// </remarks>
	public static ImmutableHashSet<int> ReadHeldBoundaries(ContainerContext container, StanzaRule rule, GapOwners gapOwners, int guardBoundary)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		ImmutableHashSet<int>.Builder held = ImmutableHashSet.CreateBuilder<int>();

		for (int boundary = 1; boundary < siblings.Length; boundary++)
		{
			container.CancellationToken.ThrowIfCancellationRequested();

			// The import section rule asks for the blank line below the last import, whatever is gathered below it.
			if (
				BlankLineGrouping.IsGapKeptBlank(siblings, boundary, gapOwners, cancellationToken: container.CancellationToken)
				|| IsReceiverKept(container, rule, boundary)
				|| IsDependentBindingKept(container, rule, boundary)
				|| BlankLineOwnership.IsImportSectionGap(
					context: container.Context,
					options: container.Options,
					previous: siblings[boundary - 1],
					next: siblings[boundary],
					rule
				)
			)
			{
				held.Add(boundary);
			}
		}

		if (guardBoundary >= 0)
			held.Add(guardBoundary);

		return held.ToImmutable();
	}

	private static bool IsDependentBindingKept(ContainerContext container, StanzaRule rule, int boundary)
	{
		return StanzaRuleState.IsDeferralOnAt(
				scope: container,
				options: container.Options,
				rule,
				target: DiagnosticDescriptors.KeepDependentBindingsTogether,
				position: container.Siblings[boundary].SpanStart
			) && BlankLineDependentBindings.GetInner(container).Contains(boundary);
	}

	private static bool IsReceiverKept(ContainerContext container, StanzaRule rule, int boundary)
	{
		return StanzaRuleState.IsDeferralOnAt(
				scope: container,
				options: container.Options,
				rule,
				target: DiagnosticDescriptors.KeepReceiverGroupTogether,
				position: container.Siblings[boundary].SpanStart
			) && BlankLineKeptPairs.Claims(container, boundary, StanzaRules.KeepReceiverGroupTogether);
	}

	/// <summary>The jumps whose blank line the jump rule asks for, in a container that rule judges.</summary>
	/// <remarks>
	/// A rule that does not defer to the jump rule, or a container the jump rule skips, has no jump to wait for.
	/// </remarks>
	private static ImmutableArray<int> ReadDeferredJumps(ContainerContext container, StanzaRule rule)
	{
		StanzaRule? jumpRule = StanzaRuleState.FindJudgingDeferral(rule, DiagnosticDescriptors.SeparateJumpStatement, containerKind: container.Kind);
		return jumpRule is not null ? BlankLineJumps.FindJudgedJumps(container, jumpRule) : [];
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

			for (int boundary = start + 1; boundary < end; boundary++)
			{
				if (removable.Contains(boundary))
				{
					staying.Add(boundary);
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
		for (int boundary = runStart; boundary < runEnd; boundary++)
		{
			if (removable.Contains(boundary))
				return true;
		}

		return false;
	}

	/// <summary>The boundaries between single-line units whose gap holds a blank line and nothing else.</summary>
	private static ImmutableHashSet<int> ReadLooseBoundaries(ContainerContext container, UnitRange unitRange)
	{
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		SourceText sourceText = container.Node.SyntaxTree.GetText(container.CancellationToken);
		ImmutableHashSet<int>.Builder loose = ImmutableHashSet.CreateBuilder<int>();

		for (int boundary = unitRange.First + 1; boundary < unitRange.End; boundary++)
		{
			container.CancellationToken.ThrowIfCancellationRequested();

			SyntaxNode previous = siblings[boundary - 1];
			SyntaxNode next = siblings[boundary];

			if (BlankLineGaps.HasBlankLineBetween(previous, next) && IsPlainGap(sourceText, previous, next))
				loose.Add(boundary);
		}

		return loose.ToImmutable();
	}

	/// <summary>Whether both siblings are single-line and nothing but whitespace stands between them.</summary>
	public static bool IsPlainGap(SourceText sourceText, SyntaxNode previous, SyntaxNode next)
	{
		return !StanzaSyntax.IsMultiLine(previous)
			&& !StanzaSyntax.IsMultiLine(next)
			&& StanzaText.IsWhitespaceOnly(
				sourceText,
				TextSpan.FromBounds(SiblingGaps.GetGapStartToken(previous, next).Span.End, next.GetFirstToken().SpanStart)
			);
	}
}
