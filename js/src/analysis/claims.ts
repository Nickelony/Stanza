import type { Family } from './classify.ts';
import { readBoundaryFamilies } from './classify.ts';
import { memberKinds, statementKinds } from './kinds.ts';
import type { Claim } from './session.ts';

/** Builds the claim of a rule that sets the runs of two or more units of one family apart from their neighbors. */
export function createRunBoundaryClaim(family: Family): Claim {
	const kinds = family === 'member' ? memberKinds : statementKinds;
	return (container, gapIndex, context) => kinds.has(container.kind) && readBoundaryFamilies(context.runsOf(container), gapIndex).includes(family);
}
