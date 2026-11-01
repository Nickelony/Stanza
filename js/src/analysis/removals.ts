import type { Family } from './classify.ts';
import { isJudgedJump, readAskingJumps } from './jumps.ts';
import { memberKinds, statementKinds } from './kinds.ts';
import type { AddRuleName } from './ruleNames.ts';
import type { RemovalContext } from './session.ts';
import { isDirective, isImport } from './syntax.ts';
import type { Container, ContainerKind, Unit } from './types.ts';

/** The two kinds of unit a pair of removal rules splits between its halves: the statements of a block and the members of a type. */
export type RunUnits = 'statements' | 'members';

const containersOf: Record<RunUnits, ReadonlySet<ContainerKind>> = { statements: statementKinds, members: memberKinds };

/** The rule whose runs a family reads, so a run counts only where that rule is enabled. */
const runRules: Partial<Record<Family, AddRuleName>> = {
	declaration: 'separate-declaration-runs',
	invocation: 'separate-invocation-runs',
	assertion: 'separate-assertion-runs',
	await: 'separate-await-runs',
	assignment: 'separate-assignment-runs',
	member: 'separate-member-groups',
	modifier: 'separate-modifier-groups',
};

/** Tells whether a unit is a statement or member that a single line holds and that no other rule owns the gaps of: not a shebang, a directive or an import. */
function isLooseUnit(unit: Unit): boolean {
	return !unit.isMultiLine && !unit.isShebang && !isDirective(unit.node) && !isImport(unit.node);
}

/** Tells whether the blank line at a gap between two statements or members may go: nothing else stands there and no rule would write it back. */
function isLooseGap(container: Container, gapIndex: number, context: RemovalContext): boolean {
	const gap = container.gaps[gapIndex];
	const previous = container.units[gapIndex];
	const next = container.units[gapIndex + 1];

	if (gap === undefined || previous === undefined || next === undefined) {
		return false;
	}

	if (!gap.isBlank || gap.blankRange === undefined || gap.hasComment || gap.isJoined || !isLooseUnit(previous) || !isLooseUnit(next)) {
		return false;
	}

	return !context.isClaimed(gapIndex);
}

/** Tells whether a gap holds no blank line and no rule asks for one, so the unit below it is tight against the unit above. */
function isTightGap(container: Container, gapIndex: number, context: RemovalContext): boolean {
	const gap = container.gaps[gapIndex];
	return gap !== undefined && !gap.isBlank && (gap.isJoined || !context.isClaimed(gapIndex));
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

		const gathered = new Set(groups.flat());

		const asks = readAskingJumps(
			container,
			(index) => (container.gaps[index]?.isBlank === true && !gathered.has(index)) || context.isClaimed(index),
			gathered,
		);

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

	return !gap.hasComment && isLooseUnit(previous) && isLooseUnit(next) && isTightGap(container, gapIndex, context);
}

/** Tells whether the blank line of a gap sets two sections apart, a section being the tight units of one run and a lone unit between two being one. */
function isSectionBreak(container: Container, span: RunSpan, gapIndex: number, context: RemovalContext): boolean {
	const isTight = (index: number): boolean => isTightInRun(container, span, index, context);
	const before = isTight(gapIndex - 1);
	const after = isTight(gapIndex + 1);

	if (before && after) {
		return true;
	}

	// A lone unit has a blank line on its other side too, which sets it apart from a second section.
	if (before) {
		return isInsideRun(container, span, gapIndex + 1, context) && isTight(gapIndex + 2);
	}

	return after && isInsideRun(container, span, gapIndex - 1, context) && isTight(gapIndex - 2);
}

/** Reads the gaps inside the runs that the enabled run rules read as one, as one group for each run, for the keep rules of one kind of unit. */
export function readKeptRuns(container: Container, context: RemovalContext, units: RunUnits): readonly (readonly number[])[] {
	if (!containersOf[units].has(container.kind)) {
		return [];
	}

	// A modifier group is the finer reading of a member run, so it wins where both rules are enabled.
	const runs = units === 'members' && context.isEnabled('separate-modifier-groups') ? context.modifierRunsOf(container) : context.runsOf(container);
	const groups: number[][] = [];

	for (let start = 0; start < container.units.length; start = (runs.ends[start] ?? start) + 1) {
		const end = runs.ends[start] ?? start;
		const family = runs.keys[start]?.family;
		const rule = family === undefined ? undefined : runRules[family];

		if (rule === undefined || !context.isEnabled(rule) || end === start) {
			continue;
		}

		const span: RunSpan = { start, end };

		const gaps = Array.from({ length: end - start }, (_, offset) => start + offset).filter(
			(index) => isLooseGap(container, index, context) && !isSectionBreak(container, span, index, context),
		);

		if (gaps.length > 0) {
			groups.push(gaps);
		}
	}

	return groups;
}
