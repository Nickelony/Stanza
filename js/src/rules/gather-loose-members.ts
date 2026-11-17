import { readLooseGaps, readLooseRuns } from '../analysis/removals.ts';
import { createRemoveRule } from '../createRemoveRule.ts';

export default createRemoveRule({
	name: 'gather-loose-members',
	description: 'Remove the blank lines between the single-line members of a run that holds more than the limit allows.',
	message: 'Remove this blank line: the run it stands in holds too many members to keep them apart.',
	createRemoval: () => (container, context) => readLooseRuns(container, context, 'members'),
	reach: (container, context) => readLooseGaps(container, context, 'members'),
});
