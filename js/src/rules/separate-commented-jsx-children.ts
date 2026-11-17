import { isToolDirective } from '../analysis/comments.ts';
import { createAddRule } from '../createAddRule.ts';

export default createAddRule({
	name: 'separate-commented-jsx-children',
	description: 'Separate a comment that leads a single-line child of a JSX element from the child above it.',
	message: 'Add a blank line above this comment.',

	createClaim: () => (container, gapIndex) => {
		const previous = container.units[gapIndex];
		const next = container.units[gapIndex + 1];

		if (container.kind !== 'jsx' || previous === undefined || next === undefined || previous.isMultiLine || next.isMultiLine) {
			return false;
		}

		return next.jsxLeadTexts.some((text) => !isToolDirective(text));
	},
});
