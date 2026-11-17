import type { Family } from './classify.ts';
import { isJudgedJump, readJumpAsks } from './jumps.ts';
import { memberKinds, statementKinds } from './kinds.ts';
import { isReceiverEdge } from './receivers.ts';
import type { AddRuleName } from './ruleNames.ts';
import { isSectionBoundary } from './sections.ts';
import type { RemovalContext } from './session.ts';
import { isDirective, isImport } from './syntax.ts';
import type { Container, ContainerKind, Gap, Unit } from './types.ts';

/** The two kinds of unit a pair of removal rules splits between its halves: the statements of a block and the members of a type. */
export type RunUnits = 'statements' | 'members';

const containersOf: Record<RunUnits, ReadonlySet<ContainerKind>> = { statements: statementKinds, members: memberKinds };

/** The rule whose runs a family reads, so a run counts only where that rule is enabled. */
const runRules: Partial<Record<Family, AddRuleName>> = {
	declaration: 'separate-declaration-runs',
	invocation: 'separate-invocation-runs',
	assertion: 'separate-assertion-runs',
	await: 'separate-await-runs',
	yield: 'separate-yield-runs',
	assignment: 'separate-assignment-runs',
	member: 'separate-member-groups',
	modifier: 'separate-modifier-groups',
};

/** Tells whether a unit is a statement or member that a single line holds and that no other rule owns the gaps of: not a shebang, a directive or an import. */
function isLooseUnit(unit: Unit): boolean {
	return !unit.isMultiLine && !unit.isShebang && !isDirective(unit.node) && !isImport(unit.node);
}

/** Tells whether a gap holds a comment that ends a run: an `eslint-disable` comment does not, since ESLint deletes it where no report uses it. */
function holdsRunBreakingComment(gap: Gap, context: RemovalContext): boolean {
	return gap.hasComment && !gap.comments.every(context.isDisableDirective);
}

/** Tells whether the blank line at a gap between two statements or members may go: nothing else stands there and no rule would write it back. */
function isLooseGap(container: Container, gapIndex: number, context: RemovalContext): boolean {
	const gap = container.gaps[gapIndex];
	const previous = container.units[gapIndex];
	const next = container.units[gapIndex + 1];

	if (gap === undefined || previous === undefined || next === undefined) {
		return false;
	}

	if (
		!gap.isBlank ||
		gap.blankRange === undefined ||
		holdsRunBreakingComment(gap, context) ||
		gap.isJoined ||
		!isLooseUnit(previous) ||
		!isLooseUnit(next)
	) {
		return false;
	}

	// A kept gap reads as tight already, since its keeper removes the blank line whatever order the fixes run in.
	return (
		!context.isKept(gapIndex) &&
		!context.isClaimed(gapIndex) &&
		!isReceiverEdge(container, gapIndex, context.environment, context.receiverGroupsOf) &&
		!context.protectedSectionsOf(container).has(gapIndex)
	);
}

/** Tells whether a gap holds no blank line, or one a keeper removes, and no rule asks for one, so the unit below it is tight against the unit above. */
function isTightGap(container: Container, gapIndex: number, context: RemovalContext): boolean {
	const gap = container.gaps[gapIndex];

	if (gap === undefined) {
		return false;
	}

	return (
		context.isKept(gapIndex) ||
		(!gap.isBlank &&
			!isReceiverEdge(container, gapIndex, context.environment, context.receiverGroupsOf) &&
			(gap.isJoined || !context.isClaimed(gapIndex)))
	);
}

/**
 * Reads the gaps whose blank line goes, keeping the blank line above each jump that `separate-jump-statements` would write back.
 * That rule asks once the stanza above the jump spans two lines in the text the removal leaves, so a gap it holds blank ends the run it stood in.
 * `read` returns the groups of gaps to remove, given a test for the gaps that stay.
 */
function settleJumpGaps(
	container: Container,
	context: RemovalContext,
	read: (isHeld: (gapIndex: number) => boolean) => readonly (readonly number[])[],
): readonly (readonly number[])[] {
	const held = new Set<number>();

	for (;;) {
		const groups = read((gapIndex) => held.has(gapIndex));

		if (!context.isEnabled('separate-jump-statements')) {
			return groups;
		}

		// A blank line a keep rule removes goes in the same pass, so it counts as gathered too.
		const kept = container.gaps.flatMap((gap, index) => (gap.isBlank && context.isKept(index) ? [index] : []));
		const gathered = new Set([...groups.flat(), ...kept]);
		const asks = readJumpAsks(container, (index) => gathered.has(index), context.isClaimed);
		const staying = [...gathered].filter((gapIndex) => isJudgedJump(container, gapIndex) && asks[gapIndex + 1] === true);

		if (staying.length === 0) {
			return groups;
		}

		staying.forEach((gapIndex) => held.add(gapIndex));
	}
}

/** Reads the gaps of the loose runs a container holds past the limit, as one group for each run, for the gather rules of one kind of unit. */
export function readLooseRuns(container: Container, context: RemovalContext, units: RunUnits): readonly (readonly number[])[] {
	const limit = context.environment.maxLooseRunSize;

	if (limit === 0 || !containersOf[units].has(container.kind)) {
		return [];
	}

	return settleJumpGaps(container, context, (isHeld) => {
		const groups: number[][] = [];
		const last = container.units.length - 1;

		for (let start = 0; start <= last;) {
			let end = start;

			while (end < last && !isHeld(end) && isLooseGap(container, end, context)) {
				end += 1;
			}

			let first = start;
			let final = end;

			// A unit that sits tight against its other neighbor stays out of the run, so the blank line that sets that group apart stays.
			if (first > 0 && isTightGap(container, first - 1, context)) {
				first += 1;
			}

			if (final < last && isTightGap(container, final, context)) {
				final -= 1;
			}

			if (final - first + 1 > limit) {
				groups.push(Array.from({ length: final - first }, (_, offset) => first + offset));
			}

			start = end + 1;
		}

		return groups;
	});
}

/** The units a kept run spans, as the indexes of its first and last unit. */
interface RunSpan {
	readonly start: number;
	readonly end: number;
}

/** Tells whether a gap of a run holds a blank line that the keep rule may remove. */
function isInsideRun(container: Container, span: RunSpan, gapIndex: number, context: RemovalContext): boolean {
	return gapIndex >= span.start && gapIndex < span.end && isLooseGap(container, gapIndex, context);
}

/** Tells whether a gap of a run holds no blank line and nothing else, so the units around it stand tight against each other. */
function isTightInRun(container: Container, span: RunSpan, gapIndex: number, context: RemovalContext): boolean {
	const gap = container.gaps[gapIndex];
	const previous = container.units[gapIndex];
	const next = container.units[gapIndex + 1];

	if (gapIndex < span.start || gapIndex >= span.end || gap === undefined || previous === undefined || next === undefined) {
		return false;
	}

	return !holdsRunBreakingComment(gap, context) && isLooseUnit(previous) && isLooseUnit(next) && isTightGap(container, gapIndex, context);
}

/** Tells whether the blank line of a gap sets two sections apart, a section being the tight units of one run and a lone unit between two being one. */
function isSectionBreak(container: Container, span: RunSpan, gapIndex: number, context: RemovalContext): boolean {
	return isSectionBoundary(
		gapIndex,
		(index) => isInsideRun(container, span, index, context),
		(index) => isTightInRun(container, span, index, context),
	);
}

/** Reads the runs of two or more units that an enabled run rule reads as one. */
function readEnabledRunSpans(container: Container, context: RemovalContext, units: RunUnits): RunSpan[] {
	if (!containersOf[units].has(container.kind)) {
		return [];
	}

	// A modifier group is the finer reading of a member run, so it wins where both rules are enabled.
	const runs = units === 'members' && context.isEnabled('separate-modifier-groups') ? context.modifierRunsOf(container) : context.runsOf(container);
	const spans: RunSpan[] = [];

	for (let start = 0; start < container.units.length; start = (runs.ends[start] ?? start) + 1) {
		const end = runs.ends[start] ?? start;
		const family = runs.keys[start]?.family;
		const rule = family === undefined ? undefined : runRules[family];

		if (rule !== undefined && context.isEnabled(rule) && end > start) {
			spans.push({ start, end });
		}
	}

	return spans;
}

/** Reads the gaps inside the runs that the enabled run rules read as one, as one group for each run, for the keep rules of one kind of unit. */
export function readKeptRuns(container: Container, context: RemovalContext, units: RunUnits): readonly (readonly number[])[] {
	return readEnabledRunSpans(container, context, units).flatMap((span) => {
		const gaps = Array.from({ length: span.end - span.start }, (_, offset) => span.start + offset).filter(
			(index) => isLooseGap(container, index, context) && !isSectionBreak(container, span, index, context),
		);

		return gaps.length > 0 ? [gaps] : [];
	});
}

/** Reads the gaps inside the runs that the enabled run rules read as one whatever blank lines stand, so no section break splits a run yet. */
export function readRunGaps(container: Container, context: RemovalContext, units: RunUnits): readonly number[] {
	return readEnabledRunSpans(container, context, units).flatMap((span) =>
		Array.from({ length: span.end - span.start }, (_, offset) => span.start + offset).filter((index) => {
			const gap = container.gaps[index];
			const previous = container.units[index];
			const next = container.units[index + 1];

			return (
				gap !== undefined &&
				previous !== undefined &&
				next !== undefined &&
				!holdsRunBreakingComment(gap, context) &&
				!gap.isJoined &&
				isLooseUnit(previous) &&
				isLooseUnit(next) &&
				!context.isClaimed(index) &&
				!isReceiverEdge(container, index, context.environment, context.receiverGroupsOf) &&
				!context.protectedSectionsOf(container).has(index)
			);
		}),
	);
}
