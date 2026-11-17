import { createRunBoundaryClaim } from '../analysis/claims.ts';
import { createAddRule } from '../createAddRule.ts';

export default createAddRule({
	name: 'separate-declaration-runs',
	description: 'Separate a run of two or more declarations of one kind from the statements beside it.',
	message: 'Add a blank line between this declaration run and its neighbor.',
	createClaim: () => createRunBoundaryClaim('declaration'),
});
