import { countLeadingGuards } from '../analysis/leading.ts';
import { createGapRule } from '../createGapRule.ts';

export default createGapRule({
	name: 'separate-guard-clauses',
	description: 'Separate the guard clauses that open a block from the statements below them.',
	message: 'Add a blank line below the guard clauses.',

	createClaim: () => (container, gapIndex, context) =>
		container.kind === 'block' && gapIndex + 1 === countLeadingGuards(container, context.environment.guardRoots),
});
