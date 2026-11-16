import { isLoop, readSeededName } from '../analysis/bindings.ts';
import { statementKinds } from '../analysis/kinds.ts';
import type { ClaimContext } from '../analysis/session.ts';
import type { Container } from '../analysis/types.ts';
import { createKeepRule } from '../createKeepRule.ts';

const seedGaps = new WeakMap<Container, ReadonlySet<number>>();

/** Reads the gaps of each run of single-line seeds directly above a loop that builds every one of them, the gap above the loop included. */
function readSeedGaps(container: Container, context: ClaimContext): ReadonlySet<number> {
	const gaps = new Set<number>();

	if (!statementKinds.has(container.kind)) {
		return gaps;
	}

	container.units.forEach((loop, loopIndex) => {
		if (!isLoop(loop.node)) {
			return;
		}

		// A loop is no seed, so each walk stops at the loop above and every unit is read once.
		for (let index = loopIndex - 1; index >= 0; index -= 1) {
			const seed = container.units[index];
			const name = seed === undefined || seed.isMultiLine ? undefined : readSeededName(seed.node);

			if (
				seed === undefined ||
				name === undefined ||
				container.gaps[index]?.hasComment !== false ||
				!context.buildsSeed(seed.node, loop.node, name)
			) {
				break;
			}

			gaps.add(index);
		}
	});

	return gaps;
}

export default createKeepRule({
	name: 'keep-accumulator-with-loop',
	description: 'Remove the blank lines between the statements that seed names and the loop below them that builds those names.',
	message: 'Remove the blank line between this accumulator and the loop that builds it.',

	claim: (container, gapIndex, context) => {
		let gaps = seedGaps.get(container);

		if (gaps === undefined) {
			gaps = readSeedGaps(container, context);
			seedGaps.set(container, gaps);
		}

		return gaps.has(gapIndex);
	},
});
