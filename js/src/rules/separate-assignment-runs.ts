import { createRunBoundaryClaim } from '../analysis/claims.ts';
import { createGapRule } from '../createGapRule.ts';

export default createGapRule({
	name: 'separate-assignment-runs',
	description: 'Separate a run of two or more assignments from the statements beside it.',
	message: 'Add a blank line between this assignment run and its neighbor.',
	createClaim: () => createRunBoundaryClaim('assignment'),
});
