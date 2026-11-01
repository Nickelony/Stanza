import { describe, expect, it } from 'vitest';

import { emptyConfig, type Config } from '../../src/state/config.ts';
import { optionId } from '../../src/state/model.ts';
import { eslintOptionLines, eslintRuleLines, generateEslintConfig } from '../../src/writers/eslint-config.ts';
import { realJavascript } from '../fixtures.ts';

const header = [
	"import prettier from 'eslint-config-prettier';",
	"import stanza from 'eslint-plugin-stanza';",
	'',
	'export default [',
	'\tprettier,',
];

const configFor = (changes: Partial<Config>): Config => ({ ...emptyConfig('error'), ...changes });
const written = (changes: Partial<Config>): string => generateEslintConfig(realJavascript, configFor(changes));
const rule = (id: string) => realJavascript.rules.find((candidate) => candidate.id === id)!;
const option = (id: string) => realJavascript.options.find((candidate) => optionId(candidate) === id)!;

/** The identity of the option that the entry of a rule takes under the key. */
const own = (ruleId: string, key: string): string => `${ruleId}.${key}`;
const typeImports = own('separate-import-groups', 'typeImports');
const maxLooseRunSize = 'maxLooseRunSize';

describe('the eslint.config.mjs the page writes', () => {
	it('loads the preset of the level after eslint-config-prettier, and nothing else while the form follows the level', () => {
		expect(written({})).toBe([...header, '\tstanza.configs.moderate,', '];', ''].join('\n'));

		for (const level of ['none', 'minimal', 'strong'] as const) {
			expect(written({ level })).toContain(`\tstanza.configs.${level},\n];`);
		}
	});

	it('writes a rule the visitor sets apart from the level', () => {
		const text = written({ level: 'strong', rules: { 'separate-guard-clauses': 'none', 'separate-directives': 'warning' } });

		expect(text).toBe(
			[
				...header,
				'\tstanza.configs.strong,',
				'\t{',
				'\t\trules: {',
				"\t\t\t'stanza/separate-directives': 'warn',",
				"\t\t\t'stanza/separate-guard-clauses': 'off',",
				'\t\t},',
				'\t},',
				'];',
				'',
			].join('\n'),
		);
	});

	it('enables a rule the level leaves off, and gives the preset no entry for a rule set to what the level gives', () => {
		const text = written({ level: 'minimal', rules: { 'separate-await-runs': 'error', 'separate-directives': 'error' } });

		expect(text).toContain("\t\t\t'stanza/separate-await-runs': 'error',");
		expect(text).not.toContain('separate-directives');
		expect(written({ level: 'none', rules: { 'separate-jump-statements': 'none' } })).toBe(written({ level: 'none' }));
	});

	it('enables an opt-in rule with an entry of its own at every level, because no preset holds it', () => {
		for (const id of ['keep-statement-runs-together', 'keep-member-runs-together']) {
			for (const level of ['none', 'minimal', 'moderate', 'strong'] as const) {
				expect(written({ level, rules: { [id]: 'error' } })).toContain(`\t\t\t'stanza/${id}': 'error',`);
			}

			expect(written({ level: 'strong', rules: { [id]: 'warning' } })).toContain(`'stanza/${id}': 'warn',`);
			expect(written({ level: 'strong', rules: { [id]: 'none' } })).toBe(written({ level: 'strong' }));
			expect(written({ level: 'strong' })).not.toContain(id);
		}

		const both = written({ level: 'strong', rules: { 'keep-statement-runs-together': 'error', 'keep-member-runs-together': 'error' } });

		expect(both).toContain("\t\t\t'stanza/keep-statement-runs-together': 'error',\n\t\t\t'stanza/keep-member-runs-together': 'error',");
	});

	it('puts keep-single-line-elements-together in the minimal preset, so the minimal level writes no entry for it', () => {
		expect(written({ level: 'minimal' })).not.toContain('keep-single-line-elements-together');

		expect(written({ level: 'none', rules: { 'keep-single-line-elements-together': 'error' } })).toContain(
			"'stanza/keep-single-line-elements-together': 'error',",
		);
	});

	it('writes every rule of the level at warn while the severity is warning, because each preset rule is an error', () => {
		const lines = written({ level: 'minimal', severity: 'warning' }).split('\n');

		expect(lines).toContain("\t\t\t'stanza/separate-directives': 'warn',");
		expect(lines).toContain("\t\t\t'stanza/separate-import-section': 'warn',");
		expect(lines).toContain("\t\t\t'stanza/keep-single-line-elements-together': 'warn',");
		expect(lines).toContain("\t\t\t'stanza/keep-single-line-switch-cases-together': 'warn',");
		expect(lines.filter((line) => line.includes("'stanza/"))).toHaveLength(4);
	});

	it('writes the tight half of a pair alone while the preset holds the separating half, because the plugin gives the gap to the tight half', () => {
		const tight = 'keep-multi-line-switch-cases-together';

		expect(rule('separate-multi-line-switch-cases').opposite).toBe(tight);

		expect(written({ rules: { [tight]: 'error' } })).toBe(
			[...header, '\tstanza.configs.moderate,', '\t{', '\t\trules: {', `\t\t\t'stanza/${tight}': 'error',`, '\t\t},', '\t},', '];', ''].join('\n'),
		);

		expect(eslintRuleLines(realJavascript, rule('separate-multi-line-switch-cases'), configFor({ rules: { [tight]: 'error' } }))).toEqual([]);
	});

	it('writes the separating half of a pair as the level gives it while the tight half is enabled, whatever the severity', () => {
		const lines = written({ level: 'minimal', severity: 'warning', rules: { 'keep-multi-line-switch-cases-together': 'warning' } }).split('\n');

		expect(lines).toContain("\t\t\t'stanza/keep-multi-line-switch-cases-together': 'warn',");
		expect(lines).toContain("\t\t\t'stanza/keep-single-line-switch-cases-together': 'warn',");

		const strong = written({ level: 'strong', severity: 'warning', rules: { 'keep-multi-line-jsx-children-together': 'error' } });

		expect(strong).toContain("\t\t\t'stanza/separate-multi-line-jsx-children': 'warn',");
		expect(strong).toContain("\t\t\t'stanza/keep-multi-line-jsx-children-together': 'error',");
	});

	it('writes a separating half the visitor sets, and the entry that turns it off, as it does without the tight half', () => {
		const tight = { 'keep-multi-line-switch-cases-together': 'error' } as const;

		expect(written({ rules: { ...tight, 'separate-multi-line-switch-cases': 'error' } })).toBe(written({ rules: tight }));

		expect(written({ rules: { ...tight, 'separate-multi-line-switch-cases': 'none' } })).toContain(
			"\t\t\t'stanza/separate-multi-line-switch-cases': 'off',",
		);
	});

	it('writes the options of a rule that differ from its default', () => {
		const text = written({ level: 'strong', options: { [typeImports]: 'true' } });

		expect(text).toContain("\t\t\t'stanza/separate-import-groups': ['error', { typeImports: true }],");
		expect(written({ level: 'strong', options: { [typeImports]: 'false' } })).toBe(written({ level: 'strong' }));
	});

	it('writes an option at the strong level in the entry of its rule, with the severity of the rule', () => {
		expect(written({ level: 'strong', options: { [typeImports]: 'true' } })).toContain("['error', { typeImports: true }],");
		expect(written({ level: 'strong', severity: 'warning', options: { [typeImports]: 'true' } })).toContain("['warn', { typeImports: true }],");
	});

	it('leaves the options of a rule that is off out', () => {
		const text = written({ level: 'strong', rules: { 'separate-import-groups': 'none' }, options: { [typeImports]: 'true' } });

		expect(text).toContain("'stanza/separate-import-groups': 'off',");
		expect(text).not.toContain('typeImports');
	});

	it('writes the settings the rules share under settings.stanza, as lists of strings', () => {
		const text = written({ options: { assertionRoots: 'expect, should', testRoots: 'describe, it' } });

		const block = [
			'\t\tsettings: {',
			'\t\t\tstanza: {',
			"\t\t\t\tassertionRoots: ['expect', 'should'],",
			"\t\t\t\ttestRoots: ['describe', 'it'],",
			'\t\t\t},',
			'\t\t},',
		];

		expect(text).toContain(block.join('\n'));
		expect(written({ options: { assertionRoots: option('assertionRoots').defaultValue! } })).toBe(written({}));
	});

	it('writes the guard roots under settings.stanza, once, whatever rules read them', () => {
		const text = written({ level: 'strong', options: { guardRoots: 'check, invariant' } });

		expect(text.match(/guardRoots/g)).toHaveLength(1);
		expect(text).toContain("\t\t\tstanza: {\n\t\t\t\tguardRoots: ['check', 'invariant'],\n\t\t\t},");
		expect(text).not.toContain("'stanza/separate-guard-clauses'");
		expect(text).not.toContain("'stanza/separate-base-call'");
		expect(written({ level: 'strong', options: { guardRoots: option('guardRoots').defaultValue! } })).toBe(written({ level: 'strong' }));
	});

	it('writes the limit of a loose run under settings.stanza, once, and not in the entry of a rule', () => {
		const text = written({ level: 'strong', options: { [maxLooseRunSize]: '5' } });

		expect(text).toContain('\t\tsettings: {\n\t\t\tstanza: {\n\t\t\t\tmaxLooseRunSize: 5,\n\t\t\t},\n\t\t},');
		expect(text.match(/maxLooseRunSize/g)).toHaveLength(1);
		expect(text).not.toContain('rules:');
		expect(written({ level: 'strong', options: { [maxLooseRunSize]: '0' } })).toContain('maxLooseRunSize: 0,');
		expect(written({ level: 'strong', severity: 'warning', options: { [maxLooseRunSize]: '5' } })).toContain('maxLooseRunSize: 5,');
	});

	it('writes the limit only while it differs from the default', () => {
		expect(written({ level: 'strong', options: { [maxLooseRunSize]: option(maxLooseRunSize).defaultValue! } })).toBe(written({ level: 'strong' }));
		expect(written({ level: 'strong' })).not.toContain('maxLooseRunSize');
	});

	it('writes the limit whether or not a rule that gathers loose runs is enabled, because the setting belongs to no rule', () => {
		const options = { [maxLooseRunSize]: '5' };

		expect(written({ level: 'moderate', options })).toContain('maxLooseRunSize: 5,');

		expect(written({ level: 'strong', rules: { 'gather-loose-statements': 'none', 'gather-loose-members': 'none' }, options })).toContain(
			'maxLooseRunSize: 5,',
		);
	});

	it('writes the settings in the order of their keys, the limit beside the roots', () => {
		const text = written({ level: 'strong', options: { [maxLooseRunSize]: '5', guardRoots: 'check', assertionRoots: 'expect' } });
		expect(text).toContain("\t\t\t\tassertionRoots: ['expect'],\n\t\t\t\tguardRoots: ['check'],\n\t\t\t\tmaxLooseRunSize: 5,\n");
	});

	it('writes a boolean option of a list rule in its entry', () => {
		const text = written({ level: 'strong', options: { [own('separate-multi-line-elements', 'arrayElements')]: 'false' } });
		expect(text).toContain("\t\t\t'stanza/separate-multi-line-elements': ['error', { arrayElements: false }],");
	});

	it('writes an option of one family rule in the entry of that rule alone, though the other takes the same name', () => {
		const keep = own('keep-single-line-elements-together', 'objectMembers');
		const separate = own('separate-multi-line-elements', 'objectMembers');
		const minimal = written({ level: 'minimal', options: { [keep]: 'false' } });

		expect(minimal).toContain("\t\t\t'stanza/keep-single-line-elements-together': ['error', { objectMembers: false }],");
		expect(minimal).not.toContain('separate-multi-line-elements');

		const strong = written({ level: 'strong', options: { [separate]: 'false' } });

		expect(strong).toContain("\t\t\t'stanza/separate-multi-line-elements': ['error', { objectMembers: false }],");
		expect(strong).not.toContain('keep-single-line-elements-together');
	});

	it('writes each rule of the family its own object when both turn a kind off, and only the kinds that are off', () => {
		const text = written({
			level: 'strong',

			options: {
				[own('keep-single-line-elements-together', 'objectMembers')]: 'false',
				[own('keep-single-line-elements-together', 'parameters')]: 'false',
				[own('separate-multi-line-elements', 'callArguments')]: 'false',
			},
		});

		expect(text).toContain("			'stanza/keep-single-line-elements-together': ['error', { objectMembers: false, parameters: false }],");
		expect(text).toContain("			'stanza/separate-multi-line-elements': ['error', { callArguments: false }],");
	});

	it('writes no entry while every option of a family rule is at its default, and none for an option set to the default', () => {
		const keep = own('keep-single-line-elements-together', 'objectMembers');

		expect(written({ level: 'minimal', options: { [keep]: 'true' } })).toBe(written({ level: 'minimal' }));
		expect(written({ level: 'strong', options: { [keep]: 'true' } })).toBe(written({ level: 'strong' }));
	});

	it('keeps the options of a rule at the severity of the entry, and drops them while the rule is off', () => {
		const keep = own('keep-single-line-elements-together', 'objectMembers');

		expect(written({ level: 'minimal', severity: 'warning', options: { [keep]: 'false' } })).toContain(
			"'stanza/keep-single-line-elements-together': ['warn', { objectMembers: false }],",
		);

		const off = written({ level: 'minimal', rules: { 'keep-single-line-elements-together': 'none' }, options: { [keep]: 'false' } });

		expect(off).toContain("'stanza/keep-single-line-elements-together': 'off',");
		expect(off).not.toContain('objectMembers');
	});

	it('escapes a quote and a backslash in a value, so the file stays valid JavaScript', () => {
		const backslash = String.fromCharCode(92);
		const text = written({ options: { assertionRoots: `it's, a${backslash}b` } });

		expect(text).toContain(`assertionRoots: ['it${backslash}'s', 'a${backslash}${backslash}b'],`);
	});

	it('is a module of two imports and an array of entries', () => {
		const text = written({ level: 'strong', severity: 'warning', options: { assertionRoots: 'expect' } });
		const body = text.replace(/^import .*;\n/gm, '').replace('export default', 'return');

		expect(text.startsWith("import prettier from 'eslint-config-prettier';\nimport stanza from 'eslint-plugin-stanza';\n")).toBe(true);

		const entries = new Function('prettier', 'stanza', body)({}, { configs: { strong: {} } }) as unknown[];

		expect(entries).toHaveLength(3);
	});
});

describe('the lines the preview shows for JS', () => {
	it('gives the entry of the rule for the rule and for the option it takes', () => {
		const config = configFor({ level: 'strong', options: { [typeImports]: 'true' } });
		const entry = "'stanza/separate-import-groups': ['error', { typeImports: true }],";

		expect(eslintRuleLines(realJavascript, rule('separate-import-groups'), config)).toEqual([entry]);
		expect(eslintOptionLines(realJavascript, option(typeImports), config)).toEqual([entry]);
		expect(eslintOptionLines(realJavascript, option(maxLooseRunSize), config)).toEqual([]);
	});

	it('gives the limit of a loose run and the guard roots as settings, and nothing for the rules that read them', () => {
		const limit = configFor({ level: 'strong', options: { [maxLooseRunSize]: '5' } });

		expect(eslintOptionLines(realJavascript, option(maxLooseRunSize), limit)).toEqual(['maxLooseRunSize: 5,']);
		expect(eslintRuleLines(realJavascript, rule('gather-loose-statements'), limit)).toEqual([]);
		expect(eslintRuleLines(realJavascript, rule('gather-loose-members'), limit)).toEqual([]);

		expect(eslintOptionLines(realJavascript, option('guardRoots'), configFor({ options: { guardRoots: 'check' } }))).toEqual([
			"guardRoots: ['check'],",
		]);
	});

	it('gives the entry of a rule for the option of that rule alone when two rules take the same name', () => {
		const keep = option(own('keep-single-line-elements-together', 'objectMembers'));
		const separate = option(own('separate-multi-line-elements', 'objectMembers'));
		const config = configFor({ level: 'strong', options: { [optionId(keep)]: 'false' } });
		const entry = "'stanza/keep-single-line-elements-together': ['error', { objectMembers: false }],";

		expect(keep).not.toBe(separate);
		expect(eslintOptionLines(realJavascript, keep, config)).toEqual([entry]);
		expect(eslintOptionLines(realJavascript, separate, config)).toEqual([]);
		expect(eslintRuleLines(realJavascript, rule('keep-single-line-elements-together'), config)).toEqual([entry]);
		expect(eslintRuleLines(realJavascript, rule('separate-multi-line-elements'), config)).toEqual([]);
	});

	it('gives the setting for a setting, and nothing for a rule the level gives as written', () => {
		const config = configFor({ options: { assertionRoots: 'expect' } });

		expect(eslintOptionLines(realJavascript, option('assertionRoots'), config)).toEqual(["assertionRoots: ['expect'],"]);
		expect(eslintRuleLines(realJavascript, rule('separate-directives'), configFor({}))).toEqual([]);
	});
});
