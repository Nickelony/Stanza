import { readLooseRuns } from '../analysis/removals.ts';
import { createRemovalRule } from '../createRemovalRule.ts';

export default createRemovalRule({
	name: 'gather-loose-statements',
	description: 'Remove the blank lines between the single-line statements of a run that holds more than the limit allows.',
	message: 'Remove this blank line: the run it stands in holds too many statements to keep them apart.',
	createRemoval: () => (container, context) => readLooseRuns(container, context, 'statements'),
});
