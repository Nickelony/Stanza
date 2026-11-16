/** The rules that add a blank line, in the order that decides which of them reports a gap several of them ask for. */
export const addOrder = [
	'separate-directives',
	'separate-import-section',
	'separate-import-groups',
	'separate-multi-line-constructs',
	'separate-multi-line-switch-cases',
	'separate-multi-line-jsx-children',
	'separate-multi-line-elements',
	'separate-commented-siblings',
	'separate-commented-jsx-children',
	'separate-test-blocks',
	'separate-base-call',
	'separate-guard-clauses',
	'separate-checked-stanzas',
	'separate-jump-statements',
	'separate-member-groups',
	'separate-modifier-groups',
	'separate-declaration-runs',
	'separate-invocation-runs',
	'separate-assertion-runs',
	'separate-await-runs',
	'separate-assignment-runs',
	'separate-receiver-groups',
	'separate-yield-runs',
] as const;

/** The rules that remove a blank line a rule of the other list would not write back, in the order that decides which of them reports a gap. */
export const removeOrder = [
	'keep-single-line-elements-together',
	'keep-single-line-jsx-children-together',
	'keep-multi-line-jsx-children-together',
	'keep-single-line-switch-cases-together',
	'keep-multi-line-switch-cases-together',
	'keep-stacked-case-labels-together',
	'keep-statement-runs-together',
	'keep-member-runs-together',
	'gather-loose-statements',
	'gather-loose-members',
] as const;

/**
 * The rules that keep a pair of statements together, in the order that decides which of them reports a gap several of them ask for.
 * Each removes the blank line of a gap it asks for, and no rule of the add order asks for that gap.
 */
export const keepOrder = [
	'keep-guard-clauses-together',
	'keep-guard-with-binding',
	'keep-accumulator-with-loop',
	'keep-jump-with-binding',
	'keep-assertion-with-binding',
	'keep-initialization-with-calls',
	'keep-receiver-groups-together',
	'keep-dependent-bindings-together',
] as const;

/** The rule that removes the blank line of a container whose two units are both single-line. */
export const pairRuleName = 'keep-two-units-together';

export type AddRuleName = (typeof addOrder)[number];
export type RemoveRuleName = (typeof removeOrder)[number];
export type KeepRuleName = (typeof keepOrder)[number];
export type RuleName = AddRuleName | RemoveRuleName | KeepRuleName | typeof pairRuleName;
