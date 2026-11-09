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
	'separate-jump-statements',
	'separate-member-groups',
	'separate-modifier-groups',
	'separate-declaration-runs',
	'separate-invocation-runs',
	'separate-assertion-runs',
	'separate-await-runs',
	'separate-assignment-runs',
] as const;

/** The rules that remove a blank line a rule of the other list would not write back, in the order that decides which of them reports a gap. */
export const removeOrder = [
	'keep-single-line-elements-together',
	'keep-single-line-jsx-children-together',
	'keep-multi-line-jsx-children-together',
	'keep-single-line-switch-cases-together',
	'keep-multi-line-switch-cases-together',
	'keep-statement-runs-together',
	'keep-member-runs-together',
	'gather-loose-statements',
	'gather-loose-members',
] as const;

/** The rule that removes the blank line of a container whose two units are both single-line. */
export const pairRuleName = 'keep-two-units-together';

export type AddRuleName = (typeof addOrder)[number];
export type RemoveRuleName = (typeof removeOrder)[number];
export type RuleName = AddRuleName | RemoveRuleName | typeof pairRuleName;
