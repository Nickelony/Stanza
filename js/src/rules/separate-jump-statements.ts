import { readJumpAsks } from '../analysis/jumps.ts';
import type { Container } from '../analysis/types.ts';
import { createGapRule } from '../createGapRule.ts';

export default createGapRule({
	name: 'separate-jump-statements',
	description: 'Separate a return, throw, break or continue from a stanza of two or more lines above it.',
	message: 'Add a blank line above this jump.',
	readsOthers: true,

	createClaim: () => {
		const asking = new WeakMap<Container, boolean[]>();

		return (container, gapIndex, context) => {
			let asks = asking.get(container);

			if (asks === undefined) {
				// A blank line a keep rule removes is read as gone already, so the stanza above a jump is the one the fixed text holds.
				asks = readJumpAsks(container, context.isKept, context.isClaimedByOthers);
				asking.set(container, asks);
			}

			return asks[gapIndex + 1] === true;
		};
	},
});
