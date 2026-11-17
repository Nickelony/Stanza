import type { TSESLint } from '@typescript-eslint/utils';

import type { OwnedGap } from './analysis/session.ts';
import type { Gap } from './analysis/types.ts';

/** The most blank lines a group may hold for every one of its reports to carry the fix of the group; a longer group gives it to one report. */
export const everyReportGroupSize = 64;

/**
 * Tells whether a report carries the fix of its group, so applying any one report of a small group settles it as `--fix` does.
 * ESLint serializes each fix, and the fix of a group holds the text of the whole group,
 * so a long group gives it to one report, which keeps the cost of a run linear in its length.
 */
export function carriesGroupFix({ group, fixesGroup }: OwnedGap): boolean {
	return group === undefined || group.length <= everyReportGroupSize || fixesGroup === true;
}

/** Builds the one fix that removes the blank lines of a group of gaps, as ESLint would merge a fix for each of them. */
export function createGroupFix(fixer: TSESLint.RuleFixer, text: string, gaps: readonly Gap[]): TSESLint.RuleFix | null {
	const ranges = gaps.flatMap((gap) => (gap.blankRange === undefined ? [] : [gap.blankRange])).toSorted((a, b) => a[0] - b[0]);
	const first = ranges[0];

	if (first === undefined) {
		return null;
	}

	let kept = '';
	let cursor = first[0];

	for (const [start, end] of ranges) {
		kept += text.slice(cursor, start);
		cursor = end;
	}

	return fixer.replaceTextRange([first[0], cursor], kept);
}
