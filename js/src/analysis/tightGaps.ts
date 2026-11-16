import type { RemovalContext } from './session.ts';
import type { Container } from './types.ts';

/** Which gaps a rule that removes blank lines judges: those between two single-line units, or those with a multi-line unit beside them. */
export type TightScope = 'single-line' | 'multi-line';

/**
 * Reads the gaps of a container whose blank line goes, each as a group of its own: a gap that holds nothing but the blank line,
 * has no comment above the unit below it, is not joined and is asked for by no enabled rule.
 */
export function readTightGaps(container: Container, context: RemovalContext, scope: TightScope): readonly (readonly number[])[] {
	return container.gaps.flatMap((gap, index) => {
		const previous = container.units[index];
		const next = container.units[index + 1];

		if (previous === undefined || next === undefined || !gap.isBlank || gap.blankRange === undefined || gap.hasComment || gap.isJoined) {
			return [];
		}

		// A `{/* note */}` comment of a JSX child leads the child like a comment in the gap, so the blank line above it stays.
		if (next.jsxLeadLine !== undefined) {
			return [];
		}

		const isMultiLine = previous.isMultiLine || next.isMultiLine;

		return isMultiLine === (scope === 'multi-line') && !context.isClaimed(index) ? [[index]] : [];
	});
}

/**
 * Reads the gaps whose blank line stands between two stacked labels of a switch statement: an empty case above another case, which share one body.
 * Each is a group of its own, and a gap that holds a comment keeps its blank line.
 */
export function readStackedLabelGaps(container: Container): readonly (readonly number[])[] {
	if (container.kind !== 'switch') {
		return [];
	}

	return container.gaps.flatMap((gap, index) => {
		const previous = container.units[index]?.node;

		if (previous?.type !== 'SwitchCase' || previous.consequent.length > 0 || !gap.isBlank || gap.blankRange === undefined || gap.hasComment) {
			return [];
		}

		// The gap is joined, so no rule can write a blank line there, whatever a claim of a case rule says.
		return [[index]];
	});
}
