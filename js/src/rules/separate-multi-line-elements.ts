import { judgesList, listOptionsSchema, type ListOptions } from '../analysis/lists.ts';
import { createAddRule } from '../createAddRule.ts';

export default createAddRule<[ListOptions]>({
	name: 'separate-multi-line-elements',
	description: 'Separate an element of a list that spans more than one line from the element beside it.',
	message: 'Add a blank line between this multi-line element and its neighbor.',
	schema: [listOptionsSchema],
	defaultOptions: [{ objectMembers: true, arrayElements: true, enumMembers: true, callArguments: true, parameters: true }],

	createClaim:
		([options]) =>
		(container, gapIndex) =>
			judgesList(options, container.kind) && (container.units[gapIndex]?.isMultiLine === true || container.units[gapIndex + 1]?.isMultiLine === true),
});
