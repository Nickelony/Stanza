import { isToolDirective } from '../analysis/comments.ts';
import { memberKinds, statementKinds } from '../analysis/kinds.ts';
import { createAddRule } from '../createAddRule.ts';

export default createAddRule({
	name: 'separate-commented-siblings',
	description: 'Separate a comment that leads a single-line statement or member from the sibling above it.',
	message: 'Add a blank line above this comment.',

	createClaim: () => (container, gapIndex) => {
		const previous = container.units[gapIndex];
		const next = container.units[gapIndex + 1];
		const gap = container.gaps[gapIndex];

		if (!statementKinds.has(container.kind) && !memberKinds.has(container.kind)) {
			return false;
		}

		if (previous === undefined || next === undefined || gap === undefined || previous.isMultiLine || next.isMultiLine) {
			return false;
		}

		return gap.leadingComments.some((comment) => !isToolDirective(comment.value));
	},
});
