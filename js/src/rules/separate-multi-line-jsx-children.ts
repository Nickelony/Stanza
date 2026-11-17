import { createAddRule } from '../createAddRule.ts';

export default createAddRule({
	name: 'separate-multi-line-jsx-children',
	description: 'Separate a child of a JSX element that spans more than one line from its neighbors.',
	message: 'Add a blank line between this multi-line JSX child and its neighbor.',
	opposite: 'keep-multi-line-jsx-children-together',

	createClaim: () => (container, gapIndex) => {
		const previous = container.units[gapIndex];
		const next = container.units[gapIndex + 1];

		if (container.kind !== 'jsx' || previous === undefined || next === undefined) {
			return false;
		}

		return previous.isMultiLine || next.isMultiLine;
	},
});
