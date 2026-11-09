import { countLeadingGuards } from '../analysis/leading.ts';
import { isSuperCall } from '../analysis/syntax.ts';
import type { Container } from '../analysis/types.ts';
import { createGapRule } from '../createGapRule.ts';

/** Tells whether a block is the body of a constructor, a method, a getter or a setter, which a `super` call extends. */
function isMemberBody(container: Container): boolean {
	if (container.kind !== 'block') {
		return false;
	}

	const fn = container.node.parent;
	const owner = fn?.parent;

	if (fn?.type !== 'FunctionExpression' || owner === undefined) {
		return false;
	}

	return owner.type === 'MethodDefinition' || (owner.type === 'Property' && owner.value === fn && (owner.method || owner.kind !== 'init'));
}

export default createGapRule({
	name: 'separate-base-call',
	description: 'Separate a call of super that opens or closes a member body from the statements beside it.',
	message: 'Add a blank line beside this call of super.',

	createClaim: () => (container, gapIndex, context) => {
		if (!isMemberBody(container)) {
			return false;
		}

		const isOpening = gapIndex === countLeadingGuards(container, context.environment.guardRoots) && isSuperCall(container.units[gapIndex]?.node);
		const isClosing = gapIndex + 2 === container.units.length && isSuperCall(container.units[gapIndex + 1]?.node);

		return isOpening || isClosing;
	},
});
