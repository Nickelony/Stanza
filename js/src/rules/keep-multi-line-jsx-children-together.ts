import { readTightGaps } from '../analysis/tightGaps.ts';
import { createRemoveRule } from '../createRemoveRule.ts';

export default createRemoveRule({
	name: 'keep-multi-line-jsx-children-together',
	description: 'Remove the blank line between two children of a JSX element when either spans more than one line.',
	message: 'Remove this blank line between two JSX children.',
	createRemoval: () => (container, context) => (container.kind === 'jsx' ? readTightGaps(container, context, 'multi-line') : []),
});
