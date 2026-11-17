import { readKeptRuns } from '../analysis/removals.ts';
import { createRemoveRule } from '../createRemoveRule.ts';

export default createRemoveRule({
	name: 'keep-member-runs-together',
	description: 'Remove the blank lines inside a run of single-line members that the enabled run rules read as one.',
	message: 'Remove this blank line: the members around it are one run.',
	createRemoval: () => (container, context) => readKeptRuns(container, context, 'members'),
});
