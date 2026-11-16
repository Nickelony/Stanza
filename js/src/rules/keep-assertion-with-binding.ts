import { isAssertion, readNarrowedTest } from '../analysis/assertions.ts';
import { narrowsAnyName, readBoundNames } from '../analysis/bindings.ts';
import { isCheckedBindingGap } from '../analysis/checked.ts';
import { statementKinds } from '../analysis/kinds.ts';
import { createKeepRule } from '../createKeepRule.ts';

export default createKeepRule({
	name: 'keep-assertion-with-binding',
	description: 'Remove the blank line between a statement that binds a name and the single-line assertion below it that narrows that name.',
	message: 'Remove the blank line between this binding and the assertion that narrows it.',

	claim: (container, gapIndex, context) => {
		if (isCheckedBindingGap(container, gapIndex, context.environment, context.valueReferences, 'assertion')) {
			return true;
		}

		const previous = container.units[gapIndex];
		const next = container.units[gapIndex + 1];
		const { environment } = context;

		if (
			!statementKinds.has(container.kind) ||
			previous === undefined ||
			next === undefined ||
			next.isMultiLine ||
			container.gaps[gapIndex]?.hasComment !== false
		) {
			return false;
		}

		const test = readNarrowedTest(next.node, environment);

		if (test === undefined) {
			return false;
		}

		// An assertion with another below it leads the assert phase of a test, so it stays with that run.
		const after = container.units[gapIndex + 2];

		if (after !== undefined && !after.isMultiLine && container.gaps[gapIndex + 1]?.hasComment === false && isAssertion(after.node, environment)) {
			return false;
		}

		const bound = readBoundNames(previous.node);

		return bound.size > 0 && narrowsAnyName(test, bound);
	},
});
