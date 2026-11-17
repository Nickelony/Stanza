import { createRunBoundaryClaim } from '../analysis/claims.ts';
import { createAddRule } from '../createAddRule.ts';

export default createAddRule({
	name: 'separate-member-groups',
	description: 'Separate a run of two or more members of one kind from the members beside it.',
	message: 'Add a blank line between this member group and its neighbor.',
	createClaim: () => createRunBoundaryClaim('member'),
});
