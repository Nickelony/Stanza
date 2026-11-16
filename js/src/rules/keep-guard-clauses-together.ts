import { isGuard } from '../analysis/syntax.ts';
import { createKeepRule } from '../createKeepRule.ts';

export default createKeepRule({
	name: 'keep-guard-clauses-together',
	description: 'Remove the blank line between two neighboring guard clauses, so a chain of guards reads as one.',
	message: 'Remove the blank line inside this chain of guard clauses.',

	claim: (container, gapIndex, context) => {
		const previous = container.units[gapIndex];
		const next = container.units[gapIndex + 1];
		const { guardRoots } = context.environment;

		if (
			container.kind !== 'block' ||
			previous === undefined ||
			next === undefined ||
			(previous.isMultiLine && previous.node.type !== 'IfStatement') ||
			(next.isMultiLine && next.node.type !== 'IfStatement') ||
			container.gaps[gapIndex]?.hasComment !== false
		) {
			return false;
		}

		return isGuard(previous.node, guardRoots) && isGuard(next.node, guardRoots);
	},
});
