import type { TSESTree } from '@typescript-eslint/utils';

import { range } from './range.ts';
import { isImport, isJump } from './syntax.ts';
import type { Container, ContainerKind } from './types.ts';

const judgedKinds: ReadonlySet<ContainerKind> = new Set(['program', 'block', 'switch-case']);

/** Tells whether a `break` closes a `case`, which ends the section and not the logic above it. */
function closesCase(container: Container, gapIndex: number, node: TSESTree.BreakStatement): boolean {
	if (node.label !== null || gapIndex + 2 !== container.units.length) {
		return false;
	}

	if (container.kind === 'switch-case') {
		return true;
	}

	const parent = container.node.parent;

	return container.kind === 'block' && parent?.type === 'SwitchCase' && parent.consequent.length === 1;
}

/** Tells whether the unit below a gap is a jump that `separate-jump-statements` judges: a labeled jump and the `break` that closes a case are not. */
export function isJudgedJump(container: Container, gapIndex: number): boolean {
	const next = container.units[gapIndex + 1];

	if (!judgedKinds.has(container.kind) || next === undefined || !isJump(next.node)) {
		return false;
	}

	const jump = next.node;
	const isLabeled = (jump.type === 'BreakStatement' || jump.type === 'ContinueStatement') && jump.label !== null;

	return !isLabeled && !(jump.type === 'BreakStatement' && closesCase(container, gapIndex, jump));
}

/**
 * Reads, for each unit, whether it is a judged jump with a stanza of two or more lines above it.
 * A stanza ends at a gap the caller sets apart, at an import and at a jump that asks for a blank line itself.
 * A gap in `gathered` counts as tight, and it puts the unit below it on a line of its own.
 */
export function readAskingJumps(container: Container, isSetApart: (gapIndex: number) => boolean, gathered: ReadonlySet<number>): boolean[] {
	const asks: boolean[] = [];
	const starts: number[] = [];

	container.units.forEach((_, index) => {
		const gapIndex = index - 1;
		const previous = container.units[gapIndex];
		const previousStart = starts[gapIndex];

		asks.push(false);

		if (previous === undefined || previousStart === undefined || isImport(previous.node) || isSetApart(gapIndex)) {
			starts.push(index);
			return;
		}

		const first = container.units[previousStart];

		const spansTwoLines =
			first !== undefined && (previous.lastLine > first.firstLine || range(previousStart, gapIndex).some((gap) => gathered.has(gap)));

		asks[index] = spansTwoLines && isJudgedJump(container, gapIndex);
		starts.push(asks[index] ? index : previousStart);
	});

	return asks;
}

/**
 * Reads the jumps `separate-jump-statements` asks above, as that rule reads them:
 * a blank line a keep rule removes reads as gone already, and a gap another rule asks for ends the stanza.
 */
export function readJumpAsks(container: Container, isKept: (gapIndex: number) => boolean, isClaimed: (gapIndex: number) => boolean): boolean[] {
	const kept = new Set(container.gaps.flatMap((gap, index) => (gap.isBlank && isKept(index) ? [index] : [])));
	return readAskingJumps(container, (index) => (container.gaps[index]?.isBlank === true && !kept.has(index)) || isClaimed(index), kept);
}
