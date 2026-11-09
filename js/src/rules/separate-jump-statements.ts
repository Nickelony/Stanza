import { readAskingJumps } from '../analysis/jumps.ts';
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
				asks = readAskingJumps(container, (index) => container.gaps[index]?.isBlank === true || context.isClaimedByOthers(index), new Set());
				asking.set(container, asks);
			}

			return asks[gapIndex + 1] === true;
		};
	},
});
