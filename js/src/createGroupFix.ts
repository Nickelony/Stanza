import type { TSESLint } from '@typescript-eslint/utils';

import type { Gap } from './analysis/types.ts';

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
