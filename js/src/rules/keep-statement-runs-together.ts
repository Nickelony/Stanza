import { readKeptRuns, readRunGaps } from '../analysis/removals.ts';
import { createRemoveRule } from '../createRemoveRule.ts';

export default createRemoveRule({
	name: 'keep-statement-runs-together',
	description: 'Remove the blank lines inside a run of single-line statements that the enabled run rules read as one.',
	message: 'Remove this blank line: the statements around it are one run.',
	createRemoval: () => (container, context) => readKeptRuns(container, context, 'statements'),
	hold: { read: (container, context) => readRunGaps(container, context, 'statements'), pinsBoundary: true },
});
