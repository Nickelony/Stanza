import { countLeadingUnits } from '../analysis/leading.ts';
import { readOncePerContainer } from '../analysis/memo.ts';
import { isDirective, isImport } from '../analysis/syntax.ts';
import type { Container } from '../analysis/types.ts';
import { createGapRule } from '../createGapRule.ts';

/** Reads the index of the last import of the imports that open a container, or -1 when none do. */
function findLastLeadingImport(container: Container): number {
	const first = countLeadingUnits(container, 0, (unit) => unit.isShebang || isDirective(unit.node));
	const imports = countLeadingUnits(container, first, (unit) => isImport(unit.node));

	return imports > 0 ? first + imports - 1 : -1;
}

export default createGapRule({
	name: 'separate-import-section',
	description: 'Separate the imports that open a file from the first statement below them.',
	message: 'Add a blank line below the imports.',

	createClaim: () => {
		const readLast = readOncePerContainer(findLastLeadingImport);

		return (container, gapIndex) => {
			if (container.kind !== 'program' && container.kind !== 'module-block') {
				return false;
			}

			return gapIndex === readLast(container);
		};
	},
});
