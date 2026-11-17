import type { JSONSchema4 } from '@typescript-eslint/utils/json-schema';

import { carriesGroupFix, createGroupFix } from './createGroupFix.ts';
import { createOwnedGapListener, createRule } from './createRule.ts';

import type { RemoveRuleName } from './analysis/ruleNames.ts';
import { registerClaimant, type Removal, type RunHold } from './analysis/session.ts';

interface RemovalRuleDefinition<Options extends readonly unknown[]> {
	readonly name: RemoveRuleName;
	readonly description: string;
	readonly message: string;
	readonly schema?: JSONSchema4[];
	readonly defaultOptions?: Options;

	/** Builds the reading of the rule from its options. */
	readonly createRemoval: (options: Readonly<Options>) => Removal;
	readonly hold?: RunHold;
}

/** Creates a rule that removes the blank lines of each group of gaps the shared analysis gives it, all of a group in one fix. */
export function createRemovalRule<Options extends readonly unknown[] = []>(definition: RemovalRuleDefinition<Options>) {
	const defaultOptions = (definition.defaultOptions ?? []) as unknown as Options;

	return createRule<Options, 'extraBlankLine'>({
		name: definition.name,

		meta: {
			type: 'layout',
			docs: { description: definition.description },
			fixable: 'whitespace',
			messages: { extraBlankLine: definition.message },
			schema: definition.schema ?? [],
			defaultOptions,
		},

		create(context, options) {
			const session = registerClaimant(context.sourceCode, context.id, context.settings, {
				name: definition.name,
				removal: definition.createRemoval(options),
				hold: definition.hold,
			});

			// A report of a small group carries the fix of the group, and a long group gives it to one report, so a long run costs no more than its length.
			return createOwnedGapListener(context, session, definition.name, 'extraBlankLine', (owned) =>
				carriesGroupFix(owned) ? (fixer) => createGroupFix(fixer, context.sourceCode.text, owned.group ?? [owned.gap]) : null,
			);
		},
	});
}
