import { createAddRule } from '../createAddRule.ts';

export default createAddRule({
	name: 'separate-receiver-groups',
	description: 'Separate consecutive operations on selected receiver roots or paths from neighboring groups.',
	message: 'Add a blank line beside this receiver group.',
	judgesTwoUnits: true,
	createClaim: () => (container, gapIndex, context) => context.receiverGroupsOf(container).edges.has(gapIndex),
});
