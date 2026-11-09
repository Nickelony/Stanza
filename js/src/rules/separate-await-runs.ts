import { createRunBoundaryClaim } from '../analysis/claims.ts';
import { createGapRule } from '../createGapRule.ts';

export default createGapRule({
	name: 'separate-await-runs',
	description: 'Separate a run of two or more await statements from the statements beside it.',
	message: 'Add a blank line between this await run and its neighbor.',
	createClaim: () => createRunBoundaryClaim('await'),
});
