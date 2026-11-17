import { readTightGaps } from '../analysis/tightGaps.ts';
import { createRemoveRule } from '../createRemoveRule.ts';

export default createRemoveRule({
	name: 'keep-single-line-jsx-children-together',
	description: 'Remove the blank line between two children of a JSX element when each spans a single line.',
	message: 'Remove this blank line between two single-line JSX children.',
	createRemoval: () => (container, context) => (container.kind === 'jsx' ? readTightGaps(container, context, 'single-line') : []),
});
