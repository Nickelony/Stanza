import { countLeadingUnits } from '../analysis/leading.ts';
import { readOncePerContainer } from '../analysis/memo.ts';
import { isDirective } from '../analysis/syntax.ts';
import type { Container } from '../analysis/types.ts';
import { createGapRule } from '../createGapRule.ts';

/** Reads the index of the last unit of the header: the shebang or the last directive of the prologue. */
function findLastHeaderUnit(container: Container): number {
	const start = container.units[0]?.isShebang ? 1 : 0;
	return start + countLeadingUnits(container, start, (unit) => isDirective(unit.node)) - 1;
}

export default createGapRule({
	name: 'separate-directives',
	description: 'Separate the shebang and the directive prologue of a file or function from the statements below them.',
	message: 'Add a blank line below the shebang or the directives.',

	createClaim: () => {
		const readLast = readOncePerContainer(findLastHeaderUnit);

		return (container, gapIndex) => {
			if (container.kind !== 'program' && container.kind !== 'block') {
				return false;
			}

			return gapIndex === readLast(container) || (gapIndex === 0 && container.units[0]?.isShebang === true);
		};
	},
});
