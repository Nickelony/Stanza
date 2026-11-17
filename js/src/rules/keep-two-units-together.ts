import { pairRuleName } from '../analysis/ruleNames.ts';
import { registerClaimant } from '../analysis/session.ts';
import { createGroupFix } from '../createGroupFix.ts';
import { createOwnedGapListener, createRule } from '../createRule.ts';

export default createRule({
	name: pairRuleName,

	meta: {
		type: 'layout',
		docs: { description: 'Remove the blank line between the only two units of a container when each spans a single line.' },
		fixable: 'whitespace',
		messages: { extraBlankLine: 'Remove the blank line between these two lines.' },
		schema: [],
		defaultOptions: [],
	},

	create(context) {
		const { sourceCode } = context;
		const session = registerClaimant(sourceCode, context.id, context.settings, { name: pairRuleName });

		return createOwnedGapListener(context, session, pairRuleName, 'extraBlankLine', ({ gap }) =>
			gap.blankRange === undefined ? null : (fixer) => createGroupFix(fixer, sourceCode.text, [gap]),
		);
	},
});
