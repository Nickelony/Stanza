import { isCheckedBindingGap } from '../analysis/checked.ts';
import { isGuard } from '../analysis/syntax.ts';
import { createKeepRule } from '../createKeepRule.ts';

export default createKeepRule({
	name: 'keep-guard-with-binding',
	description: 'Remove the blank line between a statement that binds a name and the guard clause below it that tests that name.',
	message: 'Remove the blank line between this binding and the guard that tests it.',

	claim: (container, gapIndex, context) => {
		if (isCheckedBindingGap(container, gapIndex, context.environment, context.valueReferences, 'guard')) {
			return true;
		}

		const previous = container.units[gapIndex];
		const next = container.units[gapIndex + 1];

		if (container.kind !== 'block' || previous === undefined || next === undefined || container.gaps[gapIndex]?.hasComment !== false) {
			return false;
		}

		// A call of a guard helper is no `if`, so only an `if` that jumps counts.
		if (next.node.type !== 'IfStatement' || !isGuard(next.node, context.environment.guardRoots)) {
			return false;
		}

		return context.valueReferences.readsBinding(previous.node, next.node.test);
	},
});
