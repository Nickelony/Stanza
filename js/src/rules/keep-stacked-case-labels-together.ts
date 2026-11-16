import { readStackedLabelGaps } from '../analysis/tightGaps.ts';
import { createRemovalRule } from '../createRemovalRule.ts';

export default createRemovalRule({
	name: 'keep-stacked-case-labels-together',
	description: 'Remove the blank line between two stacked case labels of a switch statement, where an empty case falls through to the next one.',
	message: 'Remove this blank line between two stacked case labels.',
	createRemoval: () => readStackedLabelGaps,
});
