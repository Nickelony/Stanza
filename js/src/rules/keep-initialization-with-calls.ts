import { statementKinds } from '../analysis/kinds.ts';
import type { Claim } from '../analysis/session.ts';
import { readDirectReceiver, unwrapExpression } from '../analysis/syntax.ts';
import { createKeepRule } from '../createKeepRule.ts';

/** Tells whether a single-line initialization stands directly above a single-line direct call on the initialized name. */
const pairs: Claim = (container, gapIndex) => {
	const previous = container.units[gapIndex];
	const next = container.units[gapIndex + 1];

	if (
		!statementKinds.has(container.kind) ||
		previous === undefined ||
		next === undefined ||
		previous.isMultiLine ||
		next.isMultiLine ||
		container.gaps[gapIndex]?.hasComment !== false
	) {
		return false;
	}

	if (previous.node.type !== 'VariableDeclaration' || previous.node.declarations.length !== 1) {
		return false;
	}

	const [declaration] = previous.node.declarations;
	let value = declaration?.init ? unwrapExpression(declaration.init) : undefined;

	if (value?.type === 'AwaitExpression') {
		value = unwrapExpression(value.argument);
	}

	if (declaration?.id.type !== 'Identifier' || (value?.type !== 'CallExpression' && value?.type !== 'NewExpression')) {
		return false;
	}

	const receiver = readDirectReceiver(next.node);

	return receiver?.length === 1 && receiver[0] === declaration.id.name;
};

export default createKeepRule({
	name: 'keep-initialization-with-calls',
	readsOthers: true,
	description: 'Remove the blank line between a single-line initialization that opens a stanza and a direct call on the initialized name.',
	message: 'Remove the blank line between this initialization and its call.',
	pairs,

	claim: (container, gapIndex, context) => {
		const above = container.gaps[gapIndex - 1];

		if (
			above !== undefined &&
			(((!above.isBlank || above.blankRange === undefined) && !context.isPlannedSeparation(gapIndex - 1)) ||
				above.hasComment ||
				context.isKept(gapIndex - 1) ||
				context.isHeldByRun(gapIndex - 1))
		) {
			return false;
		}

		return pairs(container, gapIndex, context);
	},
});
