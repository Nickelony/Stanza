import type { Claim } from '../analysis/session.ts';
import { createKeepRule } from '../createKeepRule.ts';

/** Tells whether a binding stands directly above a binding whose initializer reads it, with nothing but whitespace between them. */
const pairs: Claim = (container, gapIndex, context) => context.dependentBindingsOf(container).links[gapIndex] === true;

export default createKeepRule({
	name: 'keep-dependent-bindings-together',
	fixTogether: true,
	protects: (container, reading) => reading.dependentBindingsOf(container).sections,
	description: 'Remove eligible blank lines between adjacent single-line bindings that consume the preceding value.',
	message: 'Remove the blank line inside this dependent binding chain.',
	pairs,
	claim: (container, gapIndex, context) => pairs(container, gapIndex, context) && !context.dependentBindingsOf(container).sections.has(gapIndex),
});
