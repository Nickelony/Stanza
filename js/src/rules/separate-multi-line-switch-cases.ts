import { createGapRule } from '../createGapRule.ts';

export default createGapRule({
	name: 'separate-multi-line-switch-cases',
	description: 'Separate a case of a switch statement that spans more than one line from the case beside it.',
	message: 'Add a blank line between this multi-line case and its neighbor.',

	createClaim: () => (container, gapIndex, context) => {
		const previous = container.units[gapIndex];
		const next = container.units[gapIndex + 1];

		// The tight half owns the gaps it judges while it is enabled, so the two halves never ask for opposite things.
		if (container.kind !== 'switch' || previous === undefined || next === undefined || context.isEnabled('keep-multi-line-switch-cases-together')) {
			return false;
		}

		return previous.isMultiLine || next.isMultiLine;
	},
});
