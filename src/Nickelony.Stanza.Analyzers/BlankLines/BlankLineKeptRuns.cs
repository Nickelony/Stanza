using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Nickelony.Stanza.Analyzers.BlankLines;

/// <summary>Reads the runs of single-line siblings that the enabled grouping rules read as one run.</summary>
internal static class BlankLineKeptRuns
{
	/// <summary>The runs to keep together, and the boundaries that must then hold a blank line.</summary>
	/// <remarks>
	/// Every run carries the same written boundaries, so a fix of any run writes them all.
	/// </remarks>
	public readonly record struct KeptRuns(ImmutableArray<BlankLineLooseRuns.LooseRun> Runs, ImmutableArray<int> Written);

	/// <summary>The kept gaps that go once joined, and the boundaries that then get a blank line.</summary>
	private readonly record struct Settlement(ImmutableHashSet<int> Kept, ImmutableHashSet<int> Written);

	/// <summary>What a read needs: its owners, the held boundaries but the guard's, and the standing asks.</summary>
	private readonly record struct Reading(
		ContainerContext Container,
		StanzaRule Rule,
		GapOwners GapOwners,
		ImmutableHashSet<int> HeldExceptGuard,
		ImmutableHashSet<int> Standing
	);

	/// <summary>What the judgment of a section reads of its container.</summary>
	private readonly record struct SectionReading(
		ContainerContext Container,
		SourceText Text,
		BlankLineLooseRuns.UnitRange UnitRange,
		ImmutableHashSet<int> Held,
		ImmutableHashSet<int> Inside,
		Judge Judge
	);

	private static readonly KeptRuns s_none = new(Runs: [], Written: []);

	/// <summary>The boundaries beyond the written ones that a container may try, each costing a read of it.</summary>
	private const int MaxCrossings = 8;

	/// <summary>The rounds that decline the runs which only move a blank line.</summary>
	private const int MaxRounds = 4;

	/// <summary>The units a run holds on each side of a gap for the gap to set two sections apart.</summary>
	private const int MinSectionUnits = 2;

	/// <summary>Reads the runs of a container, a gap being inside one when no rule asks for its blank line.</summary>
	/// <remarks>
	/// Joining a run can regroup its units, so the boundaries are read with the kept gaps gathered.
	/// </remarks>
	public static KeptRuns ReadRuns(ContainerContext container, StanzaRule rule, bool judgesMembers)
	{
		// A run is at least two units.
		if (BlankLineLooseRuns.FindLooseUnits(container, rule, judgesMembers, limit: 1) is not { } read)
			return s_none;

		BlankLineLooseRuns.UnitRange unitRange = read.UnitRange;
		ImmutableHashSet<int> loose = read.Loose;

		GapOwners gapOwners = BlankLineOwnership.ReadGapOwners(container, rule);
		int guardBoundary = BlankLineGuards.FindAskedGuardBoundary(container, askingRule: rule);
		ImmutableHashSet<int> held = BlankLineLooseRuns.ReadHeldBoundaries(container, rule, gapOwners, guardBoundary);

		var judge = new Judge(container, rule, gapOwners, judgesMembers);
		ImmutableHashSet<int> inside = [.. loose.Where(boundary => !held.Contains(boundary) && judge.IsInsideRun(boundary))];

		// A blank line between two blocks of units is a section break, which the rule leaves as written.
		var sectionReading = new SectionReading(
			Container: container,
			Text: container.Node.SyntaxTree.GetText(container.CancellationToken),
			UnitRange: unitRange,
			Held: held,
			Inside: inside,
			Judge: judge
		);

		ImmutableHashSet<int> sectionBreaks = [.. inside.Where(boundary => IsSectionBreak(sectionReading, boundary))];
		ImmutableHashSet<int> kept = inside.Except(sectionBreaks);

		if (kept.IsEmpty)
			return s_none;

		// The grouping rules ask for some blank lines as the text stands, which no join of a run adds.
		ImmutableHashSet<int> standing =
		[
			.. BlankLineOwnership.ReadGatheredGroupingBorders(container, rule, gapOwners, gathered: null).Select(static border => border.Boundary),
		];

		var reading = new Reading(
			Container: container,
			Rule: rule,
			GapOwners: gapOwners,
			// The guard rule hands its gap to a run that asks for it once joined, so the blank line there is the keep fix's to write.
			HeldExceptGuard: guardBoundary >= 0 ? held.Remove(guardBoundary) : held,
			Standing: standing
		);

		// A run that writes as many blank lines as it removes only moves one, and the other runs are then read without it.
		// What a run adds is what the others leave out, so two runs that share a blank line they write each keep their gain.
		// Each of the first rounds declines a moving run unless its window reaches into one declined this round, which reads again next round;
		// the last round declines every moving run.
		for (int round = 1; ; round++)
		{
			Settlement settled = ReadSettlement(reading, kept, window: null);

			if (settled.Kept.IsEmpty)
				return s_none;

			ImmutableArray<BlankLineLooseRuns.LooseRun> runs = BlankLineLooseRuns.ReadRunsOfBoundaries(unitRange, boundaries: settled.Kept, limit: 1);
			ImmutableHashSet<int> moving = round <= MaxRounds ? ReadMovingBoundaries(reading, settled, runs, alone: round < MaxRounds) : [];

			if (moving.IsEmpty)
				return new KeptRuns(Runs: runs, Written: [.. settled.Written.OrderBy(static boundary => boundary)]);

			kept = kept.Except(moving);
		}
	}

	/// <summary>Finds the boundaries of the runs that write as many blank lines as they remove.</summary>
	/// <remarks>
	/// A run writes beside itself, so its window holds the runs it can change; one reaching a declined run waits.
	/// </remarks>
	private static ImmutableHashSet<int> ReadMovingBoundaries(
		Reading reading,
		Settlement settled,
		ImmutableArray<BlankLineLooseRuns.LooseRun> runs,
		bool alone
	)
	{
		ImmutableHashSet<int>.Builder moving = ImmutableHashSet.CreateBuilder<int>();
		int declinedEnd = -1;

		foreach (BlankLineLooseRuns.LooseRun run in runs)
		{
			(int Start, int End) window = ReadWindow(reading, settled.Kept, run);

			// A run beside a declined one reads again once that one stays blank, as it may then keep its gain.
			if (alone && window.Start < declinedEnd)
				continue;

			ImmutableHashSet<int> inWindow = [.. Enumerable.Range(window.Start + 1, window.End - window.Start - 1).Where(settled.Kept.Contains)];
			ImmutableHashSet<int> boundaries = [.. Enumerable.Range(run.Start + 1, run.End - run.Start - 1)];

			// The run is charged only for the blank lines it adds beyond those the grouping rules already ask for.
			Settlement with = ReadSettlement(reading, kept: inWindow, window);
			Settlement without = ReadSettlement(reading, kept: inWindow.Except(boundaries), window);

			int added = with.Written.Except(reading.Standing).Count - without.Written.Except(reading.Standing).Count;

			if (added >= with.Kept.Count - without.Kept.Count)
			{
				moving.UnionWith(boundaries);
				declinedEnd = window.End;
			}
		}

		return moving.ToImmutable();
	}

	/// <summary>Reads the siblings a run's blank lines can change, widened by the runs beside it.</summary>
	/// <remarks>
	/// Gathering only its own gaps first keeps a chain of tight siblings from joining every run into one window.
	/// </remarks>
	private static (int Start, int End) ReadWindow(Reading reading, ImmutableHashSet<int> kept, BlankLineLooseRuns.LooseRun run)
	{
		ImmutableHashSet<int> own = [.. Enumerable.Range(run.Start + 1, run.End - run.Start - 1)];
		(int start, int end) = ReadStretch(reading, gathered: own, run);

		// A kept gap at the edge of the stretch belongs to the run beside it, whose blank lines can write into the stretch too.
		ImmutableHashSet<int> beside = [.. Enumerable.Range(start, end - start + 1).Where(kept.Contains)];

		return ReadStretch(reading, gathered: beside, run);
	}

	/// <summary>Reads the stretch that the gaps of a run reach, with the given gaps gathered.</summary>
	private static (int Start, int End) ReadStretch(Reading reading, ImmutableHashSet<int> gathered, BlankLineLooseRuns.LooseRun run)
	{
		BlankLineGrouping.GatheredStretch above = BlankLineGrouping.ReadStretchAround(
			siblings: reading.Container.Siblings,
			options: reading.Container.Options,
			gapOwners: reading.GapOwners,
			gathered,
			boundary: run.Start + 1,
			cancellationToken: reading.Container.CancellationToken
		);

		BlankLineGrouping.GatheredStretch below = BlankLineGrouping.ReadStretchAround(
			siblings: reading.Container.Siblings,
			options: reading.Container.Options,
			gapOwners: reading.GapOwners,
			gathered,
			boundary: run.End - 1,
			cancellationToken: reading.Container.CancellationToken
		);

		return (above.Start, below.End);
	}

	/// <summary>Reads which kept gaps go once joined, and which boundaries then get a blank line.</summary>
	/// <remarks>
	/// A window reads only its siblings, as a stretch of the grouping read does.
	/// </remarks>
	private static Settlement ReadSettlement(Reading reading, ImmutableHashSet<int> kept, (int Start, int End)? window)
	{
		ContainerContext container = reading.Container;
		ImmutableArray<SyntaxNode> siblings = container.Siblings;
		(int start, int end) = window ?? (0, siblings.Length);
		ImmutableHashSet<int> written = [];

		bool readsWritten = false;
		ImmutableHashSet<int> tried = [];

		// A rule that asks for a gap once the units are joined keeps its blank line, and the others are read again without it.
		// The boundaries are read with the kept gaps gathered and the boundaries written, as the fixed text has them, so one fix settles the run.
		// A written boundary can end a run that asked for another, so the written ones shrink until each is asked for with the others written.
		// Crossing runs can then ask for one beyond them, so the lowest is tried; each round shrinks the gaps or the written ones, or tries a new one.
		while (!kept.IsEmpty)
		{
			ImmutableArray<BlankLineGrouping.RunBorder> grouping = BlankLineOwnership.ReadGatheredGroupingBorders(
				container,
				rule: reading.Rule,
				gapOwners: reading.GapOwners,
				gathered: new BlankLineGrouping.GatheredStretch(Boundaries: kept, Start: start, End: end, Written: written)
			);

			ImmutableHashSet<int> remaining = kept.Except(grouping.Select(static border => border.Boundary));
			ImmutableArray<int> boundaries = ReadBoundariesToWrite(siblings, held: reading.HeldExceptGuard, kept: remaining, grouping);

			// A kept gap that stays blank changes the runs, so the boundaries to write are read again from none.
			if (remaining.Count != kept.Count)
			{
				kept = remaining;
				written = [];
				readsWritten = false;
				tried = [];

				continue;
			}

			ImmutableHashSet<int> next = readsWritten ? written.Intersect(boundaries) : [.. boundaries];

			if (next.SetEquals(written))
			{
				int crossing = tried.Count < MaxCrossings ? FindUntriedBoundary(boundaries, written, tried) : -1;

				if (crossing < 0)
					break;

				tried = tried.Add(crossing);
				next = written.Add(crossing);
			}

			written = next;
			readsWritten = true;
		}

		// The loop ends only once the last read asks for every written boundary, or on a first read that asks for none,
		// so the fix writes those alone.
		// A boundary it reads beyond them is left to the rules that ask for it, so no blank line the fix writes rests on another it cut.
		return new Settlement(kept, written);
	}

	/// <summary>Whether a kept gap sets sections apart, a lone unit between two sections being one.</summary>
	/// <remarks>
	/// A section is the units of one run tight against each other; a lone unit beside one is absorbed.
	/// </remarks>
	private static bool IsSectionBreak(SectionReading reading, int boundary)
	{
		ImmutableArray<SyntaxNode> siblings = reading.Container.Siblings;

		bool IsTightlyJoined(int candidate)
		{
			return reading.UnitRange.HoldsBoundary(candidate)
				&& !reading.Held.Contains(candidate)
				&& !BlankLineGaps.HasBlankLineBetween(siblings[candidate - 1], siblings[candidate])
				&& BlankLineLooseRuns.IsPlainGap(reading.Text, previous: siblings[candidate - 1], next: siblings[candidate])
				&& reading.Judge.IsInsideRun(candidate);
		}

		// The units tight against the unit, itself included, up to the smallest section, counting away from it by the step.
		int CountBlock(int unit, int step)
		{
			int count = 1;

			for (int candidate = step < 0 ? unit : unit + 1; count < MinSectionUnits && IsTightlyJoined(candidate); candidate += step)
				count++;

			return count;
		}

		int before = CountBlock(boundary - 1, step: -1);
		int after = CountBlock(boundary, step: 1);

		if (before >= MinSectionUnits && after >= MinSectionUnits)
			return true;

		// The lone unit has a blank line on its other side too, which sets it apart from a second section.
		if (before >= MinSectionUnits && after == 1)
			return reading.Inside.Contains(boundary + 1) && CountBlock(boundary + 1, step: 1) >= MinSectionUnits;

		return after >= MinSectionUnits
			&& before == 1
			&& reading.Inside.Contains(boundary - 1)
			&& CountBlock(boundary - 2, step: -1) >= MinSectionUnits;
	}

	/// <summary>Finds the lowest boundary the read asks for that is neither written nor tried, or -1.</summary>
	private static int FindUntriedBoundary(ImmutableArray<int> boundaries, ImmutableHashSet<int> written, ImmutableHashSet<int> tried)
	{
		foreach (int boundary in boundaries)
		{
			if (!written.Contains(boundary) && !tried.Contains(boundary))
				return boundary;
		}

		return -1;
	}

	/// <summary>Reads the gaps without a blank line where a rule asks for one once the kept gaps are tight.</summary>
	/// <remarks>
	/// A gap another rule keeps blank is that rule's, except the guard gap, which a joined run can ask for.
	/// </remarks>
	private static ImmutableArray<int> ReadBoundariesToWrite(
		ImmutableArray<SyntaxNode> siblings,
		ImmutableHashSet<int> held,
		ImmutableHashSet<int> kept,
		ImmutableArray<BlankLineGrouping.RunBorder> grouping
	)
	{
		return
		[
			.. grouping
				.Select(static border => border.Boundary)
				.Distinct()
				.Where(boundary =>
					!kept.Contains(boundary)
					&& !held.Contains(boundary)
					&& BlankLineGaps.IsGapMissingBlankLine(siblings[boundary - 1], siblings[boundary])
				)
				.OrderBy(static boundary => boundary),
		];
	}

	/// <summary>Judges whether a gap lies inside a run, by the keys of the rules that are on at it.</summary>
	/// <remarks>
	/// A gap is inside a run when a rule reads both siblings as one run and no rule that names both reads them apart.
	/// </remarks>
	private sealed class Judge(ContainerContext container, StanzaRule rule, GapOwners gapOwners, bool judgesMembers)
	{
		/// <summary>The target reader, none for a member or unless ZA0008 is on and reads `split`.</summary>
		private readonly Func<SyntaxNode, BlankLineGrouping.AssignmentTarget>? _readTarget =
			!judgesMembers
			&& StanzaRuleState.IsDeferralOn(scope: container, options: container.Options, rule, target: DiagnosticDescriptors.SeparateAssignmentRuns)
				? BlankLineAssignmentTargets.FindReader(container)
				: null;

		private readonly Func<SyntaxNode, bool> _isSubscription = judgesMembers ? static _ => false : BlankLineSubscriptions.GetReader(container);

		public bool IsInsideRun(int boundary)
		{
			container.CancellationToken.ThrowIfCancellationRequested();

			bool together = false;

			foreach (BlankLineGrouping.GapReading reading in ReadVerdicts(boundary))
			{
				if (reading == BlankLineGrouping.GapReading.Apart)
					return false;

				together |= reading == BlankLineGrouping.GapReading.Together;
			}

			return together;
		}

		/// <summary>Reads what each rule that is on says about the gap.</summary>
		private IEnumerable<BlankLineGrouping.GapReading> ReadVerdicts(int boundary)
		{
			ImmutableArray<SyntaxNode> siblings = container.Siblings;
			CancellationToken cancellationToken = container.CancellationToken;
			int position = siblings[boundary].SpanStart;

			if (judgesMembers)
			{
				if (IsOn(DiagnosticDescriptors.SeparateMemberGroups, position))
					yield return BlankLineGrouping.ReadMemberGroupGap(siblings, boundary, gapOwners, cancellationToken).Reading;

				if (IsOn(DiagnosticDescriptors.SeparateModifierGroups, position))
					yield return BlankLineGrouping.ReadModifierGroupGap(siblings, boundary, gapOwners, cancellationToken).Reading;
			}

			// The fields of a type are declarations too, so the declaration and same-type rules read them as they read locals.
			if (IsOn(DiagnosticDescriptors.SeparateSameTypeDeclarationRuns, position))
				yield return BlankLineGrouping.ReadSameTypeGap(siblings, boundary, gapOwners, cancellationToken).Reading;

			// A subscription belongs to its own rule where that rule is on, so the assignment rule has no say beside one.
			bool subscribes = IsOn(DiagnosticDescriptors.SeparateEventSubscriptionRuns, position);
			bool involvesSubscription = subscribes && (_isSubscription(siblings[boundary - 1]) || _isSubscription(siblings[boundary]));

			if (involvesSubscription)
			{
				yield return ReadSubscriptionVerdict(boundary);
				yield break;
			}

			BlankLineGrouping.GapRead read = BlankLineGrouping.ReadStatementGap(
				siblings,
				boundary,
				options: container.Options,
				gapOwners,
				readTarget: _readTarget,
				cancellationToken
			);

			// A pair that the families which are on read apart is no run, even where another key joins it.
			if (read.Reading == BlankLineGrouping.GapReading.Together && IsFamilyOn(read.Previous, position))
				yield return BlankLineGrouping.GapReading.Together;
			else if (read.Reading == BlankLineGrouping.GapReading.Apart && IsFamilyOn(read.Previous, position) && IsFamilyOn(read.Next, position))
				yield return BlankLineGrouping.GapReading.Apart;
		}

		private BlankLineGrouping.GapReading ReadSubscriptionVerdict(int boundary)
		{
			BlankLineGrouping.GapRead read = BlankLineGrouping.ReadSubscriptionGap(
				siblings: container.Siblings,
				boundary,
				options: container.Options,
				gapOwners,
				isSubscription: _isSubscription,
				cancellationToken: container.CancellationToken
			);

			bool bothSubscribe = read.Previous == BlankLineGrouping.RunFamily.Subscription && read.Next == BlankLineGrouping.RunFamily.Subscription;

			if (read.Reading == BlankLineGrouping.GapReading.Unnamed)
				return read.Reading;

			return bothSubscribe ? read.Reading : BlankLineGrouping.GapReading.Apart;
		}

		private bool IsFamilyOn(BlankLineGrouping.RunFamily? family, int position) =>
			family is { } runFamily && IsOn(BlankLineOwnership.GetRunRule(runFamily), position);

		private bool IsOn(DiagnosticDescriptor target, int position) =>
			StanzaRuleState.IsDeferralOnAt(scope: container, options: container.Options, rule, target, position);
	}
}
