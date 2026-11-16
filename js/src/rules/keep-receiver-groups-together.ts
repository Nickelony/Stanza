import type { Claim } from '../analysis/session.ts';
import { createKeepRule } from '../createKeepRule.ts';

/** Tells whether two operations of one receiver group stand at the gap, with nothing but whitespace between them. */
const pairs: Claim = (container, gapIndex, context) => {
	const groups = context.receiverGroupsOf(container);
	const start = groups.starts[gapIndex];
	const end = groups.ends[gapIndex];
	const gap = container.gaps[gapIndex];

	return (
		groups.keys[gapIndex] !== undefined && start !== undefined && end !== undefined && gapIndex < end && gap?.hasComment === false && !gap.isJoined
	);
};

export default createKeepRule({
	name: 'keep-receiver-groups-together',
	fixTogether: true,
	protects: (container, reading) => reading.receiverGroupsOf(container).sections,
	description: 'Remove eligible blank lines inside a group of ordinary operations on one selected receiver.',
	message: 'Remove the blank line inside this receiver group.',
	pairs,
	claim: (container, gapIndex, context) => pairs(container, gapIndex, context) && !context.receiverGroupsOf(container).sections.has(gapIndex),
});
