import { carriesGroupFix, createGroupFix } from './createGroupFix.ts';
import { createRule } from './createRule.ts';

import type { KeepRuleName } from './analysis/ruleNames.ts';
import { registerClaimant, type Claim, type Claimant } from './analysis/session.ts';

interface KeepRuleDefinition {
	readonly name: KeepRuleName;
	readonly description: string;
	readonly message: string;

	/** Tells whether the rule keeps the units around a gap together, whatever the gap holds now. */
	readonly claim: Claim;
	readonly pairs?: Claim;
	readonly fixTogether?: boolean;
	readonly readsOthers?: boolean;
	readonly protects?: Claimant['protects'];
}

/** Creates a rule that keeps a pair of units together: it removes the blank line of each gap it claims, and no rule that adds one asks for that gap. */
export function createKeepRule(definition: KeepRuleDefinition) {
	return createRule<[], 'extraBlankLine' | 'silenced'>({
		name: definition.name,

		meta: {
			type: 'layout',
			docs: { description: definition.description },
			fixable: 'whitespace',

			messages: {
				extraBlankLine: definition.message,

				// A silenced report shows only where inline configuration is off, and its gap may hold no blank line.
				silenced: 'An eslint-disable comment silences this rule here, so the gap stays as written.',
			},

			schema: [],
			defaultOptions: [],
		},

		create(context) {
			const session = registerClaimant(context.sourceCode, context.id, context.settings, {
				name: definition.name,
				claim: definition.claim,
				pairs: definition.pairs,
				fixTogether: definition.fixTogether ?? false,
				readsOthers: definition.readsOthers ?? false,
				judgesTwoUnits: true,
				protects: definition.protects,
			});

			return {
				Program() {
					for (const owned of session.getOwnedGaps(definition.name)) {
						const { gap, group, silencedAt, next } = owned;

						if (silencedAt !== undefined) {
							context.report({ loc: silencedAt, messageId: 'silenced' });
							continue;
						}

						context.report({
							node: next.node,
							messageId: 'extraBlankLine',
							fix: carriesGroupFix(owned) ? (fixer) => createGroupFix(fixer, context.sourceCode.text, group ?? [gap]) : null,
						});
					}
				},
			};
		},
	});
}
