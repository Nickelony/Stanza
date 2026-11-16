import { statementKinds } from '../analysis/kinds.ts';
import type { Claim } from '../analysis/session.ts';
import { createKeepRule } from '../createKeepRule.ts';

/** Tells whether a single-line binding stands directly above a single-line `return` or `throw` that uses what it binds. */
const pairs: Claim = (container, gapIndex, context) => {
	const previous = container.units[gapIndex];
	const next = container.units[gapIndex + 1];

	if (
		!statementKinds.has(container.kind) ||
		previous === undefined ||
		next === undefined ||
		previous.isMultiLine ||
		next.isMultiLine ||
		container.gaps[gapIndex]?.hasComment !== false
	) {
		return false;
	}

	const jump = next.node;

	if ((jump.type !== 'ReturnStatement' && jump.type !== 'ThrowStatement') || jump.argument === null) {
		return false;
	}

	return context.valueReferences.readsBinding(previous.node, jump.argument);
};

export default createKeepRule({
	name: 'keep-jump-with-binding',
	readsOthers: true,
	description: 'Remove the blank line between a lone single-line binding and the return or throw below it that uses what it binds.',
	message: 'Remove the blank line between this binding and the jump that uses it.',
	pairs,

	claim: (container, gapIndex, context) => {
		// The binding is a stanza of its own: it opens the container, or a blank line that stays stands above it.
		const above = container.gaps[gapIndex - 1];

		if (
			above !== undefined &&
			((!above.isBlank && !context.isPlannedSeparation(gapIndex - 1)) || context.isKept(gapIndex - 1) || context.isHeldByRun(gapIndex - 1))
		) {
			return false;
		}

		return pairs(container, gapIndex, context);
	},
});
