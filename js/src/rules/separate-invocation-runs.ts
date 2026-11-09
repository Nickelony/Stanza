import { createRunBoundaryClaim } from '../analysis/claims.ts';
import { createGapRule } from '../createGapRule.ts';

export default createGapRule({
	name: 'separate-invocation-runs',
	description: 'Separate a run of two or more calls of one helper from the statements beside it.',
	message: 'Add a blank line between this run of calls and its neighbor.',
	createClaim: () => createRunBoundaryClaim('invocation'),
});
