import type { JSONSchema4 } from '@typescript-eslint/utils/json-schema';

import { createOwnedGapListener, createRule } from './createRule.ts';

import type { AddRuleName, RemoveRuleName } from './analysis/ruleNames.ts';
import { registerClaimant, type Claim, type Plan } from './analysis/session.ts';

interface GapRuleDefinition<Options extends readonly unknown[]> {
	readonly name: AddRuleName;
	readonly description: string;
	readonly message: string;
	readonly schema?: JSONSchema4[];
	readonly defaultOptions?: Options;
	readonly readsOthers?: boolean;
	readonly opposite?: RemoveRuleName;
	readonly judgesTwoUnits?: boolean;

	/** Builds the claim of the rule from its options. */
	readonly createClaim?: (options: Readonly<Options>) => Claim;

	/** Reads the gaps of a container the rule asks for at once, in place of a claim, so keepers read them as planned separations. */
	readonly plan?: Plan;
}

/** Creates a rule that adds a blank line at each gap the shared analysis gives it. */
export function createGapRule<Options extends readonly unknown[] = []>(definition: GapRuleDefinition<Options>) {
	const defaultOptions = (definition.defaultOptions ?? []) as unknown as Options;

	return createRule<Options, 'missingBlankLine'>({
		name: definition.name,

		meta: {
			type: 'layout',
			docs: { description: definition.description },
			fixable: 'whitespace',
			messages: { missingBlankLine: definition.message },
			schema: definition.schema ?? [],
			defaultOptions,
		},

		create(context, options) {
			const claim = definition.createClaim?.(options);
			const { opposite } = definition;

			const session = registerClaimant(context.sourceCode, context.id, context.settings, {
				name: definition.name,

				claim:
					claim === undefined
						? undefined
						: (container, gapIndex, reading) => (opposite === undefined || !reading.isEnabled(opposite)) && claim(container, gapIndex, reading),

				plan: definition.plan,
				readsOthers: definition.readsOthers ?? false,
				judgesTwoUnits: definition.judgesTwoUnits ?? false,
			});

			return createOwnedGapListener(
				context,
				session,
				definition.name,
				'missingBlankLine',

				({ gap }) =>
					(fixer) =>
						fixer.insertTextBeforeRange([gap.insertAt, gap.insertAt], gap.eol),
			);
		},
	});
}
