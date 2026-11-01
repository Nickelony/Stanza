import { readBoundaryFamilies } from '../analysis/classify.ts';
import { memberKinds } from '../analysis/kinds.ts';
import { createGapRule } from '../createGapRule.ts';

export default createGapRule({
	name: 'separate-modifier-groups',
	description: 'Separate a run of two or more members of one kind that share their access, static and readonly modifiers from the members beside it.',
	message: 'Add a blank line between this modifier group and its neighbor.',

	createClaim: () => (container, gapIndex, context) =>
		memberKinds.has(container.kind) && readBoundaryFamilies(context.modifierRunsOf(container), gapIndex).includes('modifier'),
});
