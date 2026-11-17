import { createRunBoundaryClaim } from '../analysis/claims.ts';
import { createAddRule } from '../createAddRule.ts';

export default createAddRule({
	name: 'separate-assertion-runs',
	description: 'Separate a run of two or more assertions from the statements beside it.',
	message: 'Add a blank line between this assertion run and its neighbor.',
	createClaim: () => createRunBoundaryClaim('assertion'),
});
