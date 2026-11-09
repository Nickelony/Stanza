import { judgesList, listOptionsSchema, type ListOptions } from '../analysis/lists.ts';
import { readTightGaps } from '../analysis/tightGaps.ts';
import { createRemovalRule } from '../createRemovalRule.ts';

export default createRemovalRule<[ListOptions]>({
	name: 'keep-single-line-elements-together',
	description: 'Remove the blank line between two elements of a list when each spans a single line.',
	message: 'Remove this blank line between two single-line elements.',
	schema: [listOptionsSchema],
	defaultOptions: [{ objectMembers: true, arrayElements: true, enumMembers: true, callArguments: true, parameters: true }],

	createRemoval:
		([options]) =>
		(container, context) =>
			judgesList(options, container.kind) ? readTightGaps(container, context, 'single-line') : [],
});
