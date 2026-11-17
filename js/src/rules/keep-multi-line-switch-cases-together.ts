import { readTightGaps } from '../analysis/tightGaps.ts';
import { createRemoveRule } from '../createRemoveRule.ts';

export default createRemoveRule({
	name: 'keep-multi-line-switch-cases-together',
	description: 'Remove the blank line between two cases of a switch statement when either spans more than one line.',
	message: 'Remove this blank line between two cases.',
	createRemoval: () => (container, context) => (container.kind === 'switch' ? readTightGaps(container, context, 'multi-line') : []),
});
