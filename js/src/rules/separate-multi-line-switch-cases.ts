import { createGapRule } from '../createGapRule.ts';

export default createGapRule({
	name: 'separate-multi-line-switch-cases',
	description: 'Separate a case of a switch statement that spans more than one line from the case beside it.',
	message: 'Add a blank line between this multi-line case and its neighbor.',
	opposite: 'keep-multi-line-switch-cases-together',

	createClaim: () => (container, gapIndex) => {
		const previous = container.units[gapIndex];
		const next = container.units[gapIndex + 1];

		if (container.kind !== 'switch' || previous === undefined || next === undefined) {
			return false;
		}

		return previous.isMultiLine || next.isMultiLine;
	},
});
