import { readKeptRuns } from '../analysis/removals.ts';
import { createRemovalRule } from '../createRemovalRule.ts';

export default createRemovalRule({
	name: 'keep-statement-runs-together',
	description: 'Remove the blank lines inside a run of single-line statements that the enabled run rules read as one.',
	message: 'Remove this blank line: the statements around it are one run.',
	createRemoval: () => (container, context) => readKeptRuns(container, context, 'statements'),
});
