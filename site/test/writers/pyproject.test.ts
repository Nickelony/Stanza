import { describe, expect, it } from 'vitest';

import { emptyConfig, type Config } from '../../src/state/config.ts';
import { generatePyproject, pyprojectOptionLines, pyprojectRuleLines } from '../../src/writers/pyproject.ts';
import { realPython } from '../fixtures.ts';

const written = (changes: Partial<Config>): string => generatePyproject(realPython, { ...emptyConfig('error'), ...changes });
const rule = (id: string) => realPython.rules.find((candidate) => candidate.id === id)!;
const option = (key: string) => realPython.options.find((candidate) => candidate.key === key)!;

describe('the [tool.stanza] table the page writes', () => {
	it('is an empty table while the form follows the level', () => {
		expect(written({})).toBe('[tool.stanza]\n');
	});

	it('writes the level unless it is the default', () => {
		expect(written({ level: 'strong' })).toBe('[tool.stanza]\nlevel = "strong"\n');
		expect(written({ level: 'none' })).toBe('[tool.stanza]\nlevel = "none"\n');
		expect(written({ level: 'moderate' })).toBe('[tool.stanza]\n');
	});

	it('lists the rules it adds to the level in enable and the rules it removes in disable, by code', () => {
		const text = written({ level: 'minimal', rules: { ZA3008: 'error', ZA3001: 'none', ZA3011: 'error', ZA3015: 'none' } });
		expect(text).toBe(['[tool.stanza]', 'level = "minimal"', 'enable = ["ZA3008", "ZA3011"]', 'disable = ["ZA3015"]', ''].join('\n'));
	});

	it('writes no entry for a rule set to what the level gives, or disabled where the level leaves it off', () => {
		expect(written({ rules: { ZA3001: 'error', ZA3012: 'none' } })).toBe('[tool.stanza]\n');
	});

	it('writes the keys that differ from their default, as TOML', () => {
		const options = { 'max-loose-run-size': '5', exclude: 'migrations/**, build/', 'guard-roots': 'check_argument' };

		expect(written({ options })).toBe(
			['[tool.stanza]', 'exclude = ["migrations/**", "build/"]', 'guard-roots = ["check_argument"]', 'max-loose-run-size = 5', ''].join('\n'),
		);
	});

	it('enables an opt-in rule at every level, because no level holds it', () => {
		for (const level of ['none', 'minimal', 'moderate', 'strong'] as const) {
			expect(written({ level, rules: { ZA3054: 'error' } })).toContain('enable = ["ZA3054"]');
		}

		expect(written({ level: 'strong', rules: { ZA3054: 'none' } })).toBe(['[tool.stanza]', 'level = "strong"', ''].join('\n'));
	});

	it('writes a number key as a number, only while it differs from its default', () => {
		expect(written({ options: { 'max-loose-run-size': '0' } })).toBe(['[tool.stanza]', 'max-loose-run-size = 0', ''].join('\n'));
		expect(written({ options: { 'max-loose-run-size': '3' } })).toBe('[tool.stanza]\n');
	});

	it('writes the formatter as a string, only while it differs from its default', () => {
		expect(written({ options: { formatter: 'black' } })).toBe(['[tool.stanza]', 'formatter = "black"', ''].join('\n'));
		expect(written({ options: { formatter: 'none' } })).toBe(['[tool.stanza]', 'formatter = "none"', ''].join('\n'));
		expect(written({ options: { formatter: 'ruff' } })).toBe(['[tool.stanza]', 'formatter = "ruff"', ''].join('\n'));
		expect(written({ options: { formatter: 'auto' } })).toBe('[tool.stanza]\n');
	});

	it('writes the default list of a key only when the visitor changes it', () => {
		expect(written({ options: { 'assertion-roots': option('assertion-roots').defaultValue! } })).toBe('[tool.stanza]\n');
		expect(written({ options: { 'assertion-roots': 'expect' } })).toBe('[tool.stanza]\nassertion-roots = ["expect"]\n');
	});

	it('writes the number of the call rules as a number, only while it differs from its default', () => {
		expect(written({ options: { 'named-argument-min-arguments': '3' } })).toBe('[tool.stanza]\nnamed-argument-min-arguments = 3\n');
		expect(written({ options: { 'named-argument-min-arguments': '0' } })).toBe('[tool.stanza]\nnamed-argument-min-arguments = 0\n');
		expect(written({ options: { 'named-argument-min-arguments': '4' } })).toBe('[tool.stanza]\n');
	});

	it('writes the words of the call rules as strings, only while they differ from their defaults', () => {
		const options = {
			'named-argument-match': 'require',
			'named-argument-literals': 'bool',
			'named-argument-methods': 'private',
		};

		expect(written({ options })).toBe(
			['[tool.stanza]', 'named-argument-literals = "bool"', 'named-argument-match = "require"', 'named-argument-methods = "private"', ''].join('\n'),
		);

		expect(written({ options: { 'named-argument-match': 'allow', 'named-argument-literals': 'keywords', 'named-argument-methods': 'all' } })).toBe(
			'[tool.stanza]\n',
		);
	});

	it('writes the sources as a list of strings, only while they differ from the three of the default', () => {
		expect(option('named-argument-sources').defaultValue).toBe('project, stdlib, installed');
		expect(written({ options: { 'named-argument-sources': 'project, stdlib, installed' } })).toBe('[tool.stanza]\n');
		expect(written({ options: { 'named-argument-sources': 'project' } })).toBe('[tool.stanza]\nnamed-argument-sources = ["project"]\n');

		expect(written({ options: { 'named-argument-sources': 'stdlib, installed' } })).toBe(
			'[tool.stanza]\nnamed-argument-sources = ["stdlib", "installed"]\n',
		);
	});

	it('writes the lists of globs and directories as strings', () => {
		expect(written({ options: { 'named-argument-ignore': 'log.*, print' } })).toBe('[tool.stanza]\nnamed-argument-ignore = ["log.*", "print"]\n');
		expect(written({ options: { 'source-roots': 'src, lib' } })).toBe('[tool.stanza]\nsource-roots = ["src", "lib"]\n');
	});

	it('escapes a quote and a backslash in a string, as a TOML basic string needs', () => {
		const backslash = String.fromCharCode(92);
		const text = written({ options: { exclude: `a"b, c${backslash}d` } });

		expect(text).toBe(`[tool.stanza]\nexclude = ["a${backslash}"b", "c${backslash}${backslash}d"]\n`);
	});
});

describe('the lines the preview shows for Python', () => {
	it('gives the list a rule joins, and the line of a key', () => {
		const config: Config = {
			...emptyConfig('error'),
			level: 'strong',
			rules: { ZA3011: 'none', ZA3012: 'none' },
			options: { 'guard-roots': 'check_argument' },
		};

		expect(pyprojectRuleLines(realPython, rule('ZA3011'), config)).toEqual(['disable = ["ZA3011", "ZA3012"]']);
		expect(pyprojectRuleLines(realPython, rule('ZA3001'), config)).toEqual([]);
		expect(pyprojectOptionLines(option('guard-roots'), config)).toEqual(['guard-roots = ["check_argument"]']);
		expect(pyprojectOptionLines(option('exclude'), config)).toEqual([]);
		expect(pyprojectOptionLines(option('formatter'), { ...config, options: { formatter: 'black' } })).toEqual(['formatter = "black"']);
	});
});
