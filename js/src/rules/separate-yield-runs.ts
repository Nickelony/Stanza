import { createRunBoundaryClaim } from '../analysis/claims.ts';
import { createGapRule } from '../createGapRule.ts';

export default createGapRule({
	name: 'separate-yield-runs',
	description: 'Separate a run of two or more yield statements from the statements beside it.',
	message: 'Add a blank line between this yield run and its neighbor.',
	createClaim: () => createRunBoundaryClaim('yield'),
});
