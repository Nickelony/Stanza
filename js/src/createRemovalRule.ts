import type { TSESLint } from '@typescript-eslint/utils';
import type { JSONSchema4 } from '@typescript-eslint/utils/json-schema';

import { createOwnedGapListener, createRule } from './createRule.ts';

import type { RemoveRuleName } from './analysis/ruleNames.ts';
import { registerClaimant, type Removal } from './analysis/session.ts';
import type { Gap } from './analysis/types.ts';

/** Builds the one fix that removes the blank lines of a group of gaps, as ESLint would merge a fix for each of them. */
function createGroupFix(fixer: TSESLint.RuleFixer, text: string, gaps: readonly Gap[]): TSESLint.RuleFix | null {
	const ranges = gaps.flatMap((gap) => (gap.blankRange === undefined ? [] : [gap.blankRange])).toSorted((a, b) => a[0] - b[0]);
	const first = ranges[0];

	if (first === undefined) {
		return null;
	}

	let kept = '';
	let cursor = first[0];

	for (const [start, end] of ranges) {
		kept += text.slice(cursor, start);
		cursor = end;
	}

	return fixer.replaceTextRange([first[0], cursor], kept);
}

interface RemovalRuleDefinition<Options extends readonly unknown[]> {
	readonly name: RemoveRuleName;
	readonly description: string;
	readonly message: string;
	readonly schema?: JSONSchema4[];
	readonly defaultOptions?: Options;

	/** Builds the reading of the rule from its options. */
	readonly createRemoval: (options: Readonly<Options>) => Removal;
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
			});

			// Every gap of a group shares its fix, built once, so a run of many blank lines costs no more than its length.
			const fixes = new WeakMap<readonly Gap[], TSESLint.RuleFix | null>();

			return createOwnedGapListener(context, session, definition.name, 'extraBlankLine', ({ gap, group }): TSESLint.ReportFixFunction => {
				const gaps = group ?? [gap];

				return (fixer) => {
					if (!fixes.has(gaps)) {
						fixes.set(gaps, createGroupFix(fixer, context.sourceCode.text, gaps));
					}

					return fixes.get(gaps) ?? null;
				};
			});
		},
	});
}
