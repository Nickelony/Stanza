import { pairRuleName } from '../analysis/ruleNames.ts';
import { registerClaimant } from '../analysis/session.ts';
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

		return createOwnedGapListener(context, session, pairRuleName, 'extraBlankLine', ({ container, next }) => {
			const start = container.units[0]?.node.range[1] ?? 0;
			const end = next.node.range[0];
			const between = sourceCode.text.slice(start, end);

			return /^\s*$/.test(between)
				? (fixer) => fixer.replaceTextRange([start, end], `${/\r?\n/.exec(between)?.[0] ?? '\n'}${between.slice(between.lastIndexOf('\n') + 1)}`)
				: null;
		});
	},
});
