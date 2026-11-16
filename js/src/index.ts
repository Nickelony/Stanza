import { createRequire } from 'node:module';

import type { ESLint, Linter } from 'eslint';

import { readLevelRules, type Level } from './presets.ts';

import gatherLooseMembers from './rules/gather-loose-members.ts';
import gatherLooseStatements from './rules/gather-loose-statements.ts';
import keepAccumulatorWithLoop from './rules/keep-accumulator-with-loop.ts';
import keepAssertionWithBinding from './rules/keep-assertion-with-binding.ts';
import keepDependentBindingsTogether from './rules/keep-dependent-bindings-together.ts';
import keepGuardClausesTogether from './rules/keep-guard-clauses-together.ts';
import keepGuardWithBinding from './rules/keep-guard-with-binding.ts';
import keepInitializationWithCalls from './rules/keep-initialization-with-calls.ts';
import keepJumpWithBinding from './rules/keep-jump-with-binding.ts';
import keepMemberRunsTogether from './rules/keep-member-runs-together.ts';
import keepMultiLineJsxChildrenTogether from './rules/keep-multi-line-jsx-children-together.ts';
import keepMultiLineSwitchCasesTogether from './rules/keep-multi-line-switch-cases-together.ts';
import keepReceiverGroupsTogether from './rules/keep-receiver-groups-together.ts';
import keepSingleLineElementsTogether from './rules/keep-single-line-elements-together.ts';
import keepSingleLineJsxChildrenTogether from './rules/keep-single-line-jsx-children-together.ts';
import keepSingleLineSwitchCasesTogether from './rules/keep-single-line-switch-cases-together.ts';
import keepStackedCaseLabelsTogether from './rules/keep-stacked-case-labels-together.ts';
import keepStatementRunsTogether from './rules/keep-statement-runs-together.ts';
import keepTwoUnitsTogether from './rules/keep-two-units-together.ts';
import separateAssertionRuns from './rules/separate-assertion-runs.ts';
import separateAssignmentRuns from './rules/separate-assignment-runs.ts';
import separateAwaitRuns from './rules/separate-await-runs.ts';
import separateBaseCall from './rules/separate-base-call.ts';
import separateCheckedStanzas from './rules/separate-checked-stanzas.ts';
import separateCommentedJsxChildren from './rules/separate-commented-jsx-children.ts';
import separateCommentedSiblings from './rules/separate-commented-siblings.ts';
import separateDeclarationRuns from './rules/separate-declaration-runs.ts';
import separateDirectives from './rules/separate-directives.ts';
import separateGuardClauses from './rules/separate-guard-clauses.ts';
import separateImportGroups from './rules/separate-import-groups.ts';
import separateImportSection from './rules/separate-import-section.ts';
import separateInvocationRuns from './rules/separate-invocation-runs.ts';
import separateJumpStatements from './rules/separate-jump-statements.ts';
import separateMemberGroups from './rules/separate-member-groups.ts';
import separateModifierGroups from './rules/separate-modifier-groups.ts';
import separateMultiLineConstructs from './rules/separate-multi-line-constructs.ts';
import separateMultiLineElements from './rules/separate-multi-line-elements.ts';
import separateMultiLineJsxChildren from './rules/separate-multi-line-jsx-children.ts';
import separateMultiLineSwitchCases from './rules/separate-multi-line-switch-cases.ts';
import separateReceiverGroups from './rules/separate-receiver-groups.ts';
import separateTestBlocks from './rules/separate-test-blocks.ts';
import separateYieldRuns from './rules/separate-yield-runs.ts';

const packageInfo = createRequire(import.meta.url)('../package.json') as { name: string; version: string };

export const rules = {
	'gather-loose-members': gatherLooseMembers,
	'gather-loose-statements': gatherLooseStatements,
	'keep-accumulator-with-loop': keepAccumulatorWithLoop,
	'keep-assertion-with-binding': keepAssertionWithBinding,
	'keep-dependent-bindings-together': keepDependentBindingsTogether,
	'keep-guard-clauses-together': keepGuardClausesTogether,
	'keep-guard-with-binding': keepGuardWithBinding,
	'keep-initialization-with-calls': keepInitializationWithCalls,
	'keep-jump-with-binding': keepJumpWithBinding,
	'keep-member-runs-together': keepMemberRunsTogether,
	'keep-multi-line-jsx-children-together': keepMultiLineJsxChildrenTogether,
	'keep-multi-line-switch-cases-together': keepMultiLineSwitchCasesTogether,
	'keep-receiver-groups-together': keepReceiverGroupsTogether,
	'keep-single-line-elements-together': keepSingleLineElementsTogether,
	'keep-single-line-jsx-children-together': keepSingleLineJsxChildrenTogether,
	'keep-single-line-switch-cases-together': keepSingleLineSwitchCasesTogether,
	'keep-stacked-case-labels-together': keepStackedCaseLabelsTogether,
	'keep-statement-runs-together': keepStatementRunsTogether,
	'keep-two-units-together': keepTwoUnitsTogether,
	'separate-assertion-runs': separateAssertionRuns,
	'separate-assignment-runs': separateAssignmentRuns,
	'separate-await-runs': separateAwaitRuns,
	'separate-base-call': separateBaseCall,
	'separate-checked-stanzas': separateCheckedStanzas,
	'separate-commented-jsx-children': separateCommentedJsxChildren,
	'separate-commented-siblings': separateCommentedSiblings,
	'separate-declaration-runs': separateDeclarationRuns,
	'separate-directives': separateDirectives,
	'separate-guard-clauses': separateGuardClauses,
	'separate-import-groups': separateImportGroups,
	'separate-import-section': separateImportSection,
	'separate-invocation-runs': separateInvocationRuns,
	'separate-jump-statements': separateJumpStatements,
	'separate-member-groups': separateMemberGroups,
	'separate-modifier-groups': separateModifierGroups,
	'separate-multi-line-constructs': separateMultiLineConstructs,
	'separate-multi-line-elements': separateMultiLineElements,
	'separate-multi-line-jsx-children': separateMultiLineJsxChildren,
	'separate-multi-line-switch-cases': separateMultiLineSwitchCases,
	'separate-receiver-groups': separateReceiverGroups,
	'separate-test-blocks': separateTestBlocks,
	'separate-yield-runs': separateYieldRuns,
};

const plugin: ESLint.Plugin = {
	meta: { name: packageInfo.name, version: packageInfo.version },
	rules: rules as unknown as ESLint.Plugin['rules'],
};

function createPreset(level: Level): Linter.Config {
	return { name: `stanza/${level}`, plugins: { stanza: plugin }, rules: readLevelRules(level) };
}

/** The flat-config presets; load one after `eslint-config-prettier`, and `moderate` is the one to start from. */
export const configs = {
	none: createPreset('none'),
	minimal: createPreset('minimal'),
	moderate: createPreset('moderate'),
	strong: createPreset('strong'),
} satisfies Record<Level, Linter.Config>;

plugin.configs = configs;

export type { Level };

export default plugin as ESLint.Plugin & { configs: typeof configs; rules: typeof rules };
