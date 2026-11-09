import { statementKinds } from '../analysis/kinds.ts';
import { createGapRule } from '../createGapRule.ts';

export default createGapRule({
	name: 'separate-test-blocks',
	description: 'Separate the describe, it, test and hook calls of a test file from one another.',
	message: 'Add a blank line between these test blocks.',

	createClaim: () => (container, gapIndex, context) => {
		const { keys } = context.runsOf(container);
		return statementKinds.has(container.kind) && keys[gapIndex]?.family === 'test' && keys[gapIndex + 1]?.family === 'test';
	},
});
