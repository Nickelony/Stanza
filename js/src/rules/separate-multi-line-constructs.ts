import { memberKinds, statementKinds } from '../analysis/kinds.ts';
import { createGapRule } from '../createGapRule.ts';

export default createGapRule({
	name: 'separate-multi-line-constructs',
	description: 'Separate a statement or member that spans more than one line from its neighbors.',
	message: 'Add a blank line between this multi-line construct and its neighbor.',

	createClaim: () => (container, gapIndex) => {
		const { kind } = container;
		const previous = container.units[gapIndex];
		const next = container.units[gapIndex + 1];

		if (
			!(statementKinds.has(kind) || memberKinds.has(kind)) ||
			previous === undefined ||
			next === undefined ||
			(!previous.isMultiLine && !next.isMultiLine)
		) {
			return false;
		}

		return !(previous.node.type === 'ImportDeclaration' && next.node.type === 'ImportDeclaration');
	},
});
