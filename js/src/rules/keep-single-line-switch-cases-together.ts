import { readTightGaps } from '../analysis/tightGaps.ts';
import { createRemovalRule } from '../createRemovalRule.ts';

export default createRemovalRule({
	name: 'keep-single-line-switch-cases-together',
	description: 'Remove the blank line between two cases of a switch statement when each spans a single line.',
	message: 'Remove this blank line between two single-line cases.',
	createRemoval: () => (container, context) => (container.kind === 'switch' ? readTightGaps(container, context, 'single-line') : []),
});
