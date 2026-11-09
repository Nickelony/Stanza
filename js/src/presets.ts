import type { Linter } from 'eslint';

/** The levels in the order each adds to the one before it. */
const levels = ['none', 'minimal', 'moderate', 'strong'] as const;

export type Level = (typeof levels)[number];

type RuleEntry = Linter.RuleEntry;

/** The rules each level adds to the level before it, with the options the level sets. */
const additions = {
	none: {},

	minimal: {
		'separate-directives': 'error',
		'separate-import-section': 'error',
		'keep-single-line-elements-together': 'error',
		'keep-single-line-switch-cases-together': 'error',
	},

	moderate: {
		'separate-multi-line-constructs': 'error',
		'separate-multi-line-switch-cases': 'error',
		'separate-declaration-runs': 'error',
		'separate-invocation-runs': 'error',
		'separate-assertion-runs': 'error',
		'separate-member-groups': 'error',
		'separate-test-blocks': 'error',
		'keep-two-units-together': 'error',
	},

	strong: {
		'separate-import-groups': 'error',
		'separate-await-runs': 'error',
		'separate-assignment-runs': 'error',
		'separate-guard-clauses': 'error',
		'separate-base-call': 'error',
		'separate-jump-statements': 'error',
		'separate-commented-siblings': 'error',
		'separate-commented-jsx-children': 'error',
		'separate-modifier-groups': 'error',
		'separate-multi-line-elements': 'error',
		'separate-multi-line-jsx-children': 'error',
		'keep-single-line-jsx-children-together': 'error',
		'gather-loose-statements': 'error',
		'gather-loose-members': 'error',
	},
} satisfies Record<Level, Record<string, RuleEntry>>;

/** The rules that belong to no preset, so only an entry of a project's own enables one. */
export const optInRules = [
	'keep-statement-runs-together',
	'keep-member-runs-together',
	'keep-multi-line-switch-cases-together',
	'keep-multi-line-jsx-children-together',
] as const;

/** Reads the rules of a level: those of every level up to it, with the later level's entry winning. */
export function readLevelRules(level: Level): Record<string, RuleEntry> {
	const rules: Record<string, RuleEntry> = {};

	for (const name of levels.slice(0, levels.indexOf(level) + 1)) {
		for (const [rule, entry] of Object.entries<RuleEntry>(additions[name])) {
			rules[`stanza/${rule}`] = entry;
		}
	}

	return rules;
}
