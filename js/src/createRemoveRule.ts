import type { JSONSchema4 } from '@typescript-eslint/utils/json-schema';

import { carriesGroupFix, createGroupFix } from './createGroupFix.ts';
import { createRule } from './createRule.ts';

import type { RemoveRuleName } from './analysis/ruleNames.ts';
import { registerClaimant, type Claimant, type Removal, type RunHold } from './analysis/session.ts';

interface RemoveRuleDefinition<Options extends readonly unknown[]> {
	readonly name: RemoveRuleName;
	readonly description: string;
	readonly message: string;
	readonly schema?: JSONSchema4[];
	readonly defaultOptions?: Options;

	/** Builds the reading of the rule from its options. */
	readonly createRemoval: (options: Readonly<Options>) => Removal;
	readonly hold?: RunHold;
	readonly reach?: Claimant['reach'];
}

/** Creates a rule that removes the blank lines of each group of gaps the shared analysis gives it, all of a group in one fix. */
export function createRemoveRule<Options extends readonly unknown[] = []>(definition: RemoveRuleDefinition<Options>) {
	const defaultOptions = (definition.defaultOptions ?? []) as unknown as Options;

	return createRule<Options, 'extraBlankLine' | 'silenced'>({
		name: definition.name,

		meta: {
			type: 'layout',
			docs: { description: definition.description },
			fixable: 'whitespace',

			messages: {
				extraBlankLine: definition.message,

				// A silenced report shows only where inline configuration is off, and its gap may hold a blank line that stays.
				silenced: 'An eslint-disable comment silences this rule here, so the gap stays as written.',
			},

			schema: definition.schema ?? [],
			defaultOptions,
		},

		create(context, options) {
			const session = registerClaimant(context.sourceCode, context.id, context.settings, {
				name: definition.name,
				removal: definition.createRemoval(options),
				hold: definition.hold,
				reach: definition.reach,
			});

			return {
				Program() {
					for (const owned of session.getOwnedGaps(definition.name)) {
						if (owned.silencedAt !== undefined) {
							context.report({ loc: owned.silencedAt, messageId: 'silenced' });
							continue;
						}

						// A report of a small group carries the fix of the group, and a long group gives it to one report, so a long run costs no more than its length.
						context.report({
							node: owned.next.node,
							messageId: 'extraBlankLine',
							fix: carriesGroupFix(owned) ? (fixer) => createGroupFix(fixer, context.sourceCode.text, owned.group ?? [owned.gap]) : null,
						});
					}
				},
			};
		},
	});
}
