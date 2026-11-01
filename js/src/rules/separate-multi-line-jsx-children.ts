import { createGapRule } from '../createGapRule.ts';

export default createGapRule({
	name: 'separate-multi-line-jsx-children',
	description: 'Separate a child of a JSX element that spans more than one line from its neighbors.',
	message: 'Add a blank line between this multi-line JSX child and its neighbor.',

	createClaim: () => (container, gapIndex, context) => {
		const previous = container.units[gapIndex];
		const next = container.units[gapIndex + 1];

		// The tight half owns the gaps it judges while it is enabled, so the two halves never ask for opposite things.
		if (container.kind !== 'jsx' || previous === undefined || next === undefined || context.isEnabled('keep-multi-line-jsx-children-together')) {
			return false;
		}

		return previous.isMultiLine || next.isMultiLine;
	},
});
