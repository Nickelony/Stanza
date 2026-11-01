import { readdirSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import { resolveLinks } from '../../../build/readers/guides.ts';
import { extractJavascript, readJavascript } from '../../../build/readers/javascript.ts';
import { jsSourcePaths, pythonSourcePaths } from '../../../build/readers/source.ts';
import { blobUrl } from '../../../src/address/links.ts';
import { optionId } from '../../../src/state/model.ts';
import { expandHtml } from '../../../src/util/compact.ts';
import { failsOnJavascript, failsOnPython, realJavascript, realPython, repositoryRoot } from '../../fixtures.ts';

const pagesIn = (directory: string): string[] =>
	readdirSync(join(repositoryRoot, directory))
		.filter((name) => name.endsWith('.md'))
		.map((name) => name.replace(/\.md$/, ''));

describe('the catalog of JS Stanza', () => {
	it('holds a rule for every page, each with an example', () => {
		expect(realJavascript.rules.map((rule) => rule.id).toSorted()).toEqual(pagesIn(jsSourcePaths.rules).toSorted());

		for (const rule of realJavascript.rules) {
			expect(rule.examples.length, rule.id).toBeGreaterThan(0);

			expect(
				rule.examples.every((example) => example.afters.length > 0 === !example.unchanged),
				rule.id,
			).toBe(true);

			expect(rule.tierText, rule.id).toBe(`Level: ${rule.level}`);
			expect(rule.pageUrl, rule.id).toBe(`https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/${rule.id}.md`);
		}
	});

	it('leaves the facts only the C# and Markup pages hold out of the rules and the options of JS and Python', () => {
		for (const catalog of [realJavascript, realPython]) {
			expect(catalog.rules.filter((rule) => rule.csharp !== undefined).map((rule) => rule.id)).toEqual([]);
			expect(catalog.options.filter((option) => option.csharp !== undefined).map(optionId)).toEqual([]);
		}
	});

	it('groups the rules by level, from the readme', () => {
		expect(realJavascript.groups).toEqual(['Minimal', 'Moderate', 'Strong', 'Opt-in']);
		expect(realJavascript.rules.find((rule) => rule.id === 'separate-await-runs')).toMatchObject({ level: 'strong', group: 'Strong', title: '' });
	});

	it('reads the options of the rule pages and the settings the rules share, with the defaults of the code', () => {
		const options = new Map(realJavascript.options.map((option) => [optionId(option), option]));

		expect([...options.keys()]).toEqual([
			'keep-single-line-elements-together.arrayElements',
			'separate-multi-line-elements.arrayElements',
			'assertionRoots',
			'keep-single-line-elements-together.callArguments',
			'separate-multi-line-elements.callArguments',
			'keep-single-line-elements-together.enumMembers',
			'separate-multi-line-elements.enumMembers',
			'guardRoots',
			'maxLooseRunSize',
			'keep-single-line-elements-together.objectMembers',
			'separate-multi-line-elements.objectMembers',
			'keep-single-line-elements-together.parameters',
			'separate-multi-line-elements.parameters',
			'testRoots',
			'separate-import-groups.typeImports',
		]);

		expect(options.get('separate-import-groups.typeImports')).toMatchObject({
			kind: 'enum',
			defaultValue: 'false',
			global: false,
			scope: 'separate-import-groups',
			rules: ['separate-import-groups'],
		});

		expect(options.get('guardRoots')).toMatchObject({
			kind: 'list',
			defaultValue: 'assert, invariant',
			global: true,
			rules: ['separate-guard-clauses', 'separate-base-call'],
		});

		expect(options.get('assertionRoots')).toMatchObject({ kind: 'list', defaultValue: 'assert, expect, expectTypeOf, t', global: true });
		expect(options.get('testRoots')?.defaultValue).toMatch(/^describe, it, test, /);
		expect(options.get('assertionRoots')?.examples).toHaveLength(1);

		expect(options.get('maxLooseRunSize')).toMatchObject({
			kind: 'number',
			defaultValue: '3',
			min: 0,
			max: 1000,
			global: true,
			rules: ['gather-loose-statements', 'gather-loose-members'],
		});

		expect(options.get('maxLooseRunSize')?.scope).toBeUndefined();
		expect(options.get('maxLooseRunSize')?.examples).toHaveLength(1);
	});

	it('lists the rule no preset holds as opt-in, which the page does not apply by default', () => {
		const optIn = realJavascript.rules.filter((rule) => rule.level === 'opt-in');

		expect(optIn.map((rule) => rule.id)).toEqual([
			'keep-statement-runs-together',
			'keep-member-runs-together',
			'keep-multi-line-switch-cases-together',
			'keep-multi-line-jsx-children-together',
		]);

		expect(optIn.every((rule) => rule.group === 'Opt-in' && !rule.onByDefault && rule.tierText === 'Level: opt-in')).toBe(true);

		expect(realJavascript.rules.find((rule) => rule.id === 'keep-single-line-elements-together')).toMatchObject({
			level: 'minimal',
			group: 'Minimal',
		});
	});

	it('fails the build on a preset entry that sets options, which neither the build nor the writer of the config takes', () => {
		failsOnJavascript(
			'presets',

			(text) =>
				text.replace(
					"'separate-await-runs': 'error',",
					"'separate-import-groups': ['error', { typeImports: true }],\n\t\t'separate-await-runs': 'error',",
				),

			/js\/src\/presets\.ts: line \d+: the entry \['error', \{ typeImports: true \}\] of separate-import-groups is not 'error'/,
		);
	});

	it('lists the options a rule takes, its own and the settings its page names', () => {
		const byId = new Map(realJavascript.rules.map((rule) => [rule.id, rule]));

		expect(byId.get('separate-import-groups')?.options).toEqual(['separate-import-groups.typeImports']);
		expect(byId.get('separate-multi-line-constructs')?.options).toEqual([]);
		expect(byId.get('separate-multi-line-jsx-children')?.options).toEqual([]);
		expect(byId.get('separate-declaration-runs')?.options).toEqual([]);
		expect(byId.get('separate-assertion-runs')?.options).toEqual(['assertionRoots']);
		expect(byId.get('separate-guard-clauses')?.options).toEqual(['guardRoots']);
		expect(byId.get('separate-base-call')?.options).toEqual(['guardRoots']);
		expect(byId.get('separate-await-runs')?.options).toEqual([]);
		expect(byId.get('gather-loose-statements')?.options).toEqual(['maxLooseRunSize']);
		expect(byId.get('gather-loose-members')?.options).toEqual(['maxLooseRunSize']);
		expect(byId.get('keep-statement-runs-together')?.options).toEqual([]);
		expect(byId.get('keep-member-runs-together')?.options).toEqual([]);

		for (const name of ['separate-multi-line-elements', 'keep-single-line-elements-together']) {
			expect(byId.get(name)?.options).toEqual(
				['arrayElements', 'callArguments', 'enumMembers', 'objectMembers', 'parameters'].map((key) => `${name}.${key}`),
			);
		}
	});

	it('reads the same option name on the rules of one family as one option for each rule, with the default of its own source', () => {
		const same = realJavascript.options.filter((option) => option.key === 'objectMembers');

		expect(same.map((option) => option.scope)).toEqual(['keep-single-line-elements-together', 'separate-multi-line-elements']);

		for (const option of same) {
			expect(option).toMatchObject({ kind: 'enum', defaultValue: 'true', global: false, rules: [option.scope] });
			expect(option.pageUrl).toBe(`https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/${option.scope}.md#options`);
		}

		expect(new Set(realJavascript.options.map(optionId)).size).toBe(realJavascript.options.length);
	});

	it('turns a relative link into a GitHub address, and a link to a rule page into a selection', () => {
		const htmlOf = (id: string): string =>
			realJavascript.rules
				.find((candidate) => candidate.id === id)!
				.parts.flatMap((part) => (part.kind === 'p' ? [expandHtml(part.html)] : part.kind === 'ul' ? part.items.map(expandHtml) : []))
				.join(' ');

		expect(htmlOf('separate-test-blocks')).toContain(
			'<a href="https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/separate-multi-line-constructs.md" target="_blank" rel="noopener" data-select="separate-multi-line-constructs">',
		);

		expect(htmlOf('separate-multi-line-constructs')).toContain(
			'<a href="https://github.com/Nickelony/Stanza/blob/main/js/docs/Quirks.md#gaps-no-rule-owns" target="_blank" rel="noopener">quirks</a>',
		);
	});

	it('reads a pair from every row of the pair table of the rule map, with the separating half as the default half and the tight half as the owner', () => {
		expect(realJavascript.pairs).toEqual([
			{
				first: 'separate-multi-line-switch-cases',
				second: 'keep-multi-line-switch-cases-together',
				defaultHalf: 'separate-multi-line-switch-cases',
				ownerWhenBothEnabled: 'keep-multi-line-switch-cases-together',
			},

			{
				first: 'separate-multi-line-jsx-children',
				second: 'keep-multi-line-jsx-children-together',
				defaultHalf: 'separate-multi-line-jsx-children',
				ownerWhenBothEnabled: 'keep-multi-line-jsx-children-together',
			},
		]);

		const opposites = realJavascript.rules.filter((rule) => rule.opposite !== undefined).map((rule) => `${rule.id}>${rule.opposite}`);

		expect(opposites.toSorted()).toEqual([
			'keep-multi-line-jsx-children-together>separate-multi-line-jsx-children',
			'keep-multi-line-switch-cases-together>separate-multi-line-switch-cases',
			'separate-multi-line-jsx-children>keep-multi-line-jsx-children-together',
			'separate-multi-line-switch-cases>keep-multi-line-switch-cases-together',
		]);

		expect(realJavascript.rules.find((rule) => rule.id === 'separate-directives')?.opposite).toBeUndefined();
	});

	it('fails with the file when the pair table and the rule sources describe other pairs', () => {
		const row =
			'| Two cases of a `switch` statement, when either spans several lines | `separate-multi-line-switch-cases` | `keep-multi-line-switch-cases-together` |';

		failsOnJavascript(
			'ruleMap',
			(text) => text.replace(row, row.replace('`keep-multi-line-switch-cases-together`', '`keep-single-line-switch-cases-together`')),
			/Rule-map\.md: the default half of separate-multi-line-switch-cases and keep-single-line-switch-cases-together is separate-multi-line-switch-cases, but separate-multi-line-switch-cases and keep-single-line-switch-cases-together is in a level/,
		);

		failsOnJavascript(
			'ruleMap',
			(text) => text.replace(row, row.replace('`separate-multi-line-switch-cases`', '`separate-multi-line-switch`')),
			/Rule-map\.md: the pair table lists separate-multi-line-switch, which has no page under js\/docs\/rules/,
		);

		failsOnJavascript(
			'ruleMap',
			(text) => text.replace(/^\| Two cases of a `switch` statement.*\n/m, ''),
			/Rule-map\.md: separate-multi-line-switch-cases asks context\.isEnabled about keep-multi-line-switch-cases-together in its source, but the pair table has no row that separates with it/,
		);

		failsOnJavascript(
			'ruleCode:separate-multi-line-switch-cases.ts',

			(text) =>
				text.replace("context.isEnabled('keep-multi-line-switch-cases-together')", "context.isEnabled('keep-multi-line-jsx-children-together')"),

			/Rule-map\.md: the pair table pairs separate-multi-line-switch-cases with keep-multi-line-switch-cases-together, but its source asks context\.isEnabled about keep-multi-line-jsx-children-together/,
		);

		failsOnJavascript(
			'ruleCode:separate-multi-line-jsx-children.ts',
			(text) => text.replace(/ \|\| context\.isEnabled\('keep-multi-line-jsx-children-together'\)/, ''),
			/Rule-map\.md: the pair table pairs separate-multi-line-jsx-children with keep-multi-line-jsx-children-together, but its source asks context\.isEnabled about no rule/,
		);
	});

	it('fails with the file when a source disagrees with the others', () => {
		failsOnJavascript(
			'readme',
			(text) => text.replace('separate-await-runs.md) | strong', 'separate-await-runs.md) | moderate'),
			/js\/README\.md: .*separate-await-runs/,
		);

		failsOnJavascript(
			'readme',
			(text) => text.replace(`(${blobUrl}/js/docs/rules/separate-await-runs.md)`, '(docs/rules/separate-await-runs.md)'),
			/js\/README\.md: the rule row .*separate-await-runs.* does not start with a link to https:\/\/github\.com\/.*\/js\/docs\/rules\/<page>\.md/,
		);

		failsOnJavascript(
			'presets',
			(text) => text.replace("\t\t'separate-await-runs': 'error',\n", ''),
			/js\/src\/presets\.ts: separate-await-runs is in no preset/,
		);

		failsOnJavascript(
			'presets',

			(text) =>
				text.replace("\t\t'gather-loose-members': 'error',\n", "\t\t'gather-loose-members': 'error',\n\t\t'keep-member-runs-together': 'error',\n"),

			/js\/src\/presets\.ts: keep-member-runs-together is an opt-in rule, but the strong preset holds it/,
		);

		failsOnJavascript(
			'presets',
			(text) => text.replace("\t'keep-statement-runs-together',\n", ''),
			/js\/src\/presets\.ts: keep-statement-runs-together is in no preset there, but its page says opt-in/,
		);

		failsOnJavascript(
			'presets',
			(text) => text.replace('export const optInRules', 'const optInRules'),
			/js\/src\/presets\.ts: there is no "export const optInRules/,
		);

		failsOnJavascript(
			'ruleCode:separate-import-groups.ts',
			(text) => text.replace('typeImports: false', 'typeImports: true'),
			/separate-import-groups\.md: .*typeImports.*default/,
		);

		failsOnJavascript(
			'environment',
			(text) => text.replace('defaultTestRoots', 'defaultTests'),
			/separate-test-blocks\.md: .*testRoots.*environment\.ts/,
		);

		failsOnJavascript(
			'environment',
			(text) => text.replace('defaultGuardRoots', 'defaultGuards'),
			/separate-guard-clauses\.md: .*guardRoots.*environment\.ts/,
		);

		failsOnJavascript(
			'rules:separate-guard-clauses.md',
			(text) => text.replace('**Level: strong.**', 'Level: strong'),
			/separate-guard-clauses\.md: .*level line/,
		);

		failsOnJavascript(
			'rules:separate-guard-clauses.md',
			(text) => text.replace('**Level: strong.**', '**Level: opt-in.**'),
			/js\/README\.md: the table gives separate-guard-clauses the level strong, but its page says opt-in/,
		);

		failsOnJavascript(
			'environment',
			(text) => text.replace('defaultMaxLooseRunSize = 3', 'defaultMaxLooseRunSize = 4'),
			/gather-loose-statements\.md: the example of maxLooseRunSize shows 3, but environment\.ts gives it the default 4/,
		);

		failsOnJavascript(
			'rules:gather-loose-statements.md',
			(text) => text.replace('maxLooseRunSize: 3', 'maxLooseRunSize: 4'),
			/gather-loose-statements\.md: the example of maxLooseRunSize shows 4, but environment\.ts gives it the default 3/,
		);

		failsOnJavascript(
			'environment',
			(text) => text.replace('greatestMaxLooseRunSize = 1000', 'greatestMaxLooseRunSize = 2000'),
			/gather-loose-statements\.md: the Settings section does not say that maxLooseRunSize is a whole number from 0 to 2000/,
		);

		failsOnJavascript(
			'rules:gather-loose-statements.md',
			(text) => text.replace('from 0 to 1000', 'from 1 to 1000'),
			/gather-loose-statements\.md: the Settings section does not say that maxLooseRunSize is a whole number from 0 to 1000/,
		);

		failsOnJavascript(
			'environment',
			(text) => text.replace('export const leastMaxLooseRunSize = 0;\n', ''),
			/environment\.ts: defaultMaxLooseRunSize has no "export const leastMaxLooseRunSize/,
		);

		failsOnJavascript(
			'environment',
			(text) => text.replace('export const defaultMaxLooseRunSize', 'export const preferredMaxLooseRunSize'),
			/gather-loose-statements\.md: the setting maxLooseRunSize has no default exported as defaultMaxLooseRunSize by environment\.ts/,
		);

		failsOnJavascript(
			'rules:separate-guard-clauses.md',
			(text) => text.replace('```ts\n// Before', '```ts\n// Previously'),
			/separate-guard-clauses\.md/,
		);
	});
});

describe('the catalog of Python Stanza', () => {
	it('holds a rule for every page, named by its code, each with an example', () => {
		expect(realPython.rules.map((rule) => `${rule.id}-${rule.title}`).toSorted()).toEqual(pagesIn(pythonSourcePaths.rules).toSorted());

		for (const rule of realPython.rules) {
			expect(rule.id, rule.title).toMatch(/^ZA3\d{3}$/);
			expect(rule.examples.length, rule.id).toBeGreaterThan(0);

			expect(
				rule.examples.every((example) => example.afters.length > 0 === !example.unchanged),
				rule.id,
			).toBe(true);

			expect(rule.pageUrl, rule.id).toMatch(/^https:\/\/github\.com\/Nickelony\/Stanza\/blob\/main\/py\/docs\/rules\/ZA3\d{3}-/);
		}

		expect(realPython.groups).toEqual(['Minimal', 'Moderate', 'Strong', 'Opt-in']);
	});

	it('reads the keys of the configuration page that the pickers do not set, with the defaults of the code', () => {
		const options = new Map(realPython.options.map((option) => [option.key, option]));

		expect([...options.keys()]).toEqual([
			'assertion-roots',
			'exclude',
			'formatter',
			'guard-roots',
			'max-loose-run-size',
			'named-argument-ignore',
			'named-argument-literals',
			'named-argument-match',
			'named-argument-methods',
			'named-argument-min-arguments',
			'named-argument-sources',
			'source-roots',
			'subscription-roots',
		]);

		expect(options.get('assertion-roots')).toMatchObject({
			kind: 'list',
			defaultValue: 'assert*, expect, pytest.raises, pytest.warns, pytest.deprecated_call',
			global: true,
		});

		expect(options.get('exclude')).toMatchObject({ kind: 'list', defaultValue: null });
		expect(options.get('guard-roots')?.rules).toEqual(['ZA3010', 'ZA3011']);

		expect(options.get('formatter')).toMatchObject({
			kind: 'enum',
			defaultValue: 'auto',
			defaultText: '`"auto"`',
			choices: ['auto', 'ruff', 'black', 'none'],
		});

		expect(options.get('max-loose-run-size')).toMatchObject({
			kind: 'number',
			defaultValue: '3',
			min: 0,
			max: 1000,
			rules: ['ZA3052', 'ZA3053'],
		});
	});

	it('reads the keys of the call rules as a number, three words, a closed list and two free lists', () => {
		const options = new Map(realPython.options.map((option) => [option.key, option]));

		expect(options.get('named-argument-min-arguments')).toMatchObject({ kind: 'number', defaultValue: '4', min: 0, max: 1000 });
		expect(options.get('named-argument-match')).toMatchObject({ kind: 'enum', defaultValue: 'allow', choices: ['allow', 'require'] });
		expect(options.get('named-argument-literals')).toMatchObject({ kind: 'enum', defaultValue: 'keywords', choices: ['bool', 'keywords'] });
		expect(options.get('named-argument-methods')).toMatchObject({ kind: 'enum', defaultValue: 'all', choices: ['all', 'private', 'none'] });

		expect(options.get('named-argument-sources')).toMatchObject({
			kind: 'list',
			defaultValue: 'project, stdlib, installed',
			choices: ['project', 'stdlib', 'installed'],
		});

		expect(options.get('named-argument-ignore')).toMatchObject({ kind: 'list', defaultValue: null, choices: [] });
		expect(options.get('source-roots')).toMatchObject({ kind: 'list', defaultValue: null, choices: [] });
	});

	it('reads the call rules at the strong level, with the keys their pages name', () => {
		const byId = new Map(realPython.rules.map((rule) => [rule.id, rule]));

		for (const id of ['ZA3501', 'ZA3502']) {
			expect(byId.get(id)).toMatchObject({ level: 'strong', group: 'Strong', onByDefault: false });
		}

		expect(byId.get('ZA3501')?.options).toEqual([
			'named-argument-ignore',
			'named-argument-match',
			'named-argument-methods',
			'named-argument-min-arguments',
			'named-argument-sources',
			'source-roots',
		]);

		expect(byId.get('ZA3502')?.options).toEqual([
			'named-argument-ignore',
			'named-argument-literals',
			'named-argument-methods',
			'named-argument-sources',
			'source-roots',
		]);
	});

	it('names the rules that take a key, and the keys a rule takes', () => {
		const byId = new Map(realPython.rules.map((rule) => [rule.id, rule]));

		expect(byId.get('ZA3001')?.options).toEqual([]);
		expect(byId.get('ZA3010')?.options).toEqual(['guard-roots']);
		expect(byId.get('ZA3005')?.options).toEqual(['assertion-roots']);
		expect(byId.get('ZA3017')?.options).toEqual([]);
	});

	it('lists the rules no level enables as opt-in, which the tool does not apply by default', () => {
		const optIn = realPython.rules.filter((rule) => rule.level === 'opt-in');

		expect(optIn.map((rule) => rule.id)).toEqual(['ZA3054', 'ZA3055', 'ZA3112', 'ZA3201']);
		expect(optIn.every((rule) => rule.group === 'Opt-in' && !rule.onByDefault && rule.tierText === 'Level: opt-in')).toBe(true);
	});

	it('reads a pair from every row of the pair table of the rule map, and the opposite from the modules', () => {
		expect(realPython.pairs).toEqual([
			{ first: 'ZA3201', second: 'ZA3202', defaultHalf: 'ZA3202', ownerWhenBothEnabled: 'ZA3202' },
			{ first: 'ZA3111', second: 'ZA3112', defaultHalf: 'ZA3111', ownerWhenBothEnabled: 'ZA3112' },
		]);

		const opposites = realPython.rules.filter((rule) => rule.opposite !== undefined).map((rule) => `${rule.id}>${rule.opposite}`);

		expect(opposites.toSorted()).toEqual(['ZA3111>ZA3112', 'ZA3112>ZA3111', 'ZA3201>ZA3202', 'ZA3202>ZA3201']);
		expect(realPython.rules.find((rule) => rule.id === 'ZA3001')?.opposite).toBeUndefined();
	});

	it('shows the examples whose labels are comments of Python', () => {
		const header = realPython.rules.find((rule) => rule.id === 'ZA3017')!;

		expect(header.examples[0]?.before.code).toContain('#!/usr/bin/env python');
		expect(header.examples[0]?.afters).toHaveLength(1);
	});

	it('fails with the file when a source disagrees with the others', () => {
		failsOnPython(
			'ruleCode:separate_guard_clauses.py',
			(text) => text.replace('level="strong"', 'level="moderate"'),
			/ZA3011 is moderate in its module/,
		);

		failsOnPython('config', (text) => text.replace('"guard-roots",', ''), /Keys table lists guard-roots/);

		failsOnPython(
			'config',
			(text) => text.replace('not 0 <= value <= limit', 'not value <= limit'),
			/config\.py: _read_bounded checks no "not <least> <= value <= limit"/,
		);

		failsOnPython(
			'config',
			(text) => text.replace('def _read_bounded(', 'def _read_limited('),
			/config\.py: there is no function "_read_bounded" that checks the whole numbers/,
		);

		failsOnPython(
			'options',
			(text) => text.replace('DEFAULT_MAX_LOOSE_RUN_SIZE = 3', 'DEFAULT_MAX_LOOSE_RUN_SIZE = 4'),
			/options\.py: the default of max-loose-run-size differs/,
		);

		failsOnPython('options', (text) => text.replace('"expect"', '"verify"'), /options\.py: .*assertion-roots/);

		failsOnPython(
			'configuration',
			(text) => text.replace('| `max-loose-run-size` | `3` |', '| `max-loose-run-size` | `yes` |'),
			/py\/docs\/Configuration\.md: the default `yes` is none of true, false, a whole number, a list of strings and a quoted word/,
		);

		failsOnPython(
			'formatter',
			(text) => text.replace('["auto", "ruff", "black", "none"]', '["auto", "ruff", "black", "yapf", "none"]'),
			/Configuration\.md: the effect of formatter names auto, ruff, black, none, but .*formatter\.py accepts auto, ruff, black, yapf, none/,
		);

		failsOnPython(
			'formatter',
			(text) => text.replace('["auto", "ruff", "black", "none"]', '["auto", "ruff", "none"]'),
			/Configuration\.md: the effect of formatter names auto, ruff, black, none, but .*formatter\.py accepts auto, ruff, none/,
		);

		failsOnPython(
			'formatter',
			(text) => text.replace('DEFAULT_FORMATTER: FormatterName = "auto"', 'DEFAULT_FORMATTER: FormatterName = "black"'),
			/formatter\.py: the default of formatter differs/,
		);

		failsOnPython(
			'formatter',
			(text) => text.replace('DEFAULT_FORMATTER: FormatterName = "auto"', 'DEFAULT_FORMATTER: FormatterName = "yapf"'),
			/formatter\.py: DEFAULT_FORMATTER "yapf" is none of the values auto, ruff, black, none/,
		);

		failsOnPython(
			'formatter',
			(text) => text.replace('FormatterName = Literal', 'FormatterNames = Literal'),
			/formatter\.py: there is no `FormatterName = Literal/,
		);

		failsOnPython(
			'configuration',
			(text) => text.replace('| `formatter` | `"auto"` |', '| `formatter` | `"stanza"` |'),
			/Configuration\.md: the default of formatter is not one of the values/,
		);

		failsOnPython('rules:ZA3009-separate-jump-statements.md', (text) => text.replace('# Before', '# Earlier'), /ZA3009-separate-jump-statements\.md/);
	});

	it('fails with the file when the words of a key in the code and on the page differ', () => {
		failsOnPython(
			'options',
			(text) => text.replace('NamedArgumentMatch = Literal["allow", "require"]', 'NamedArgumentMatch = Literal[allow, "require"]'),
			/options\.py: \[allow, "require"\] is not a list of strings/,
		);

		failsOnPython(
			'options',

			(text) =>
				text.replace('DEFAULT_NAMED_ARGUMENT_MATCH: NamedArgumentMatch = "allow"', 'DEFAULT_NAMED_ARGUMENT_MATCH: NamedArgumentMatch = "deny"'),

			/options\.py: DEFAULT_NAMED_ARGUMENT_MATCH "deny" is none of the values allow, require/,
		);

		failsOnPython(
			'options',
			(text) => text.replace('NamedArgumentLiterals = Literal', 'NamedArgumentLiteral = Literal'),
			/options\.py: there is no `NamedArgumentLiterals = Literal\[\.\.\.\]` for DEFAULT_NAMED_ARGUMENT_LITERALS/,
		);

		failsOnPython(
			'options',

			(text) =>
				text.replace('DEFAULT_NAMED_ARGUMENT_METHODS: NamedArgumentMethods = "all"', 'DEFAULT_NAMED_ARGUMENT_METHODS: NamedArgumentMethods = "none"'),

			/options\.py: the default of named-argument-methods differs from the one .*Configuration\.md gives/,
		);

		failsOnPython(
			'options',
			(text) => text.replace('NamedArgumentMatch = Literal["allow", "require"]', 'NamedArgumentMatch = Literal["allow"]'),
			/Configuration\.md: the effect of named-argument-match names allow, require, but .*options\.py accepts allow$/,
		);

		failsOnPython(
			'options',

			(text) =>
				text.replace(
					'NamedArgumentMethods = Literal["all", "private", "none"]',
					'NamedArgumentMethods = Literal["all", "private", "none", "public"]',
				),

			/Configuration\.md: the effect of named-argument-methods names all, private, none, but .*options\.py accepts all, private, none, public/,
		);

		failsOnPython(
			'configuration',
			(text) => text.replace('| `named-argument-match` | `"allow"` |', '| `named-argument-match` | `"deny"` |'),
			/Configuration\.md: the default of named-argument-match is not one of the values/,
		);

		failsOnPython(
			'configuration',
			(text) => text.replace('`allow` or `require`.', '`allow`, `require` or `deny`.'),
			/Configuration\.md: the effect of named-argument-match names allow, require, deny, but .*options\.py accepts allow, require/,
		);
	});

	it('fails with the file when the sources of a list in the code and on the page differ', () => {
		failsOnPython(
			'options',

			(text) =>
				text.replace(
					'NamedArgumentSource = Literal["project", "stdlib", "installed"]',
					'NamedArgumentSource = Literal["project", "stdlib", "installed", "vendored"]',
				),

			/Configuration\.md: the effect of named-argument-sources names project, stdlib, installed, but .*options\.py accepts project, stdlib, installed, vendored/,
		);

		failsOnPython(
			'options',
			(text) => text.replace('NamedArgumentSource = Literal', 'NamedArgumentSources = Literal'),
			/options\.py: there is no `NamedArgumentSource = Literal\[\.\.\.\]` for NAMED_ARGUMENT_SOURCES/,
		);

		failsOnPython(
			'configuration',
			(text) => text.replace('| `["project", "stdlib", "installed"]` |', '| `["project", "vendored"]` |'),
			/Configuration\.md: the default of named-argument-sources names vendored, which .*options\.py does not accept/,
		);
	});

	it('fails with the file when the bounds of a number differ between the page and the code', () => {
		failsOnPython(
			'options',
			(text) => text.replace('NAMED_ARGUMENT_MIN_ARGUMENTS_LIMIT = 1000', 'NAMED_ARGUMENT_MIN_ARGUMENTS_LIMIT = 2000'),
			/Configuration\.md: the effect of named-argument-min-arguments says from 0 to 1000, but the code checks it from 0 to 2000/,
		);

		failsOnPython(
			'config',
			(text) => text.replaceAll('=_read_bounded(', '=_read_clamped('),
			/config\.py: there is no call '_read_bounded\(table, "<key>", DEFAULT_<NAME>, <NAME>_LIMIT\)'/,
		);

		failsOnPython(
			'config',
			(text) => text.replace(', MAX_LOOSE_RUN_SIZE_LIMIT', ', NAMED_ARGUMENT_MIN_ARGUMENTS_LIMIT'),
			/config\.py: the call that reads max-loose-run-size names DEFAULT_MAX_LOOSE_RUN_SIZE and NAMED_ARGUMENT_MIN_ARGUMENTS_LIMIT/,
		);

		failsOnPython(
			'config',
			(text) => text.replace(/"named-argument-min-arguments",(\s+DEFAULT_)/, '"named-argument-min-args",$1'),
			/config\.py: the call that reads named-argument-min-args names DEFAULT_NAMED_ARGUMENT_MIN_ARGUMENTS/,
		);

		failsOnPython(
			'options',
			(text) => text.replace('NAMED_ARGUMENT_MIN_ARGUMENTS_LIMIT = 1000', 'NAMED_ARGUMENT_MIN_LIMIT = 1000'),
			/options\.py: .*config\.py reads named-argument-min-arguments with DEFAULT_NAMED_ARGUMENT_MIN_ARGUMENTS and NAMED_ARGUMENT_MIN_ARGUMENTS_LIMIT/,
		);

		failsOnPython(
			'options',
			(text) => text.replace('DEFAULT_MAX_LOOSE_RUN_SIZE = 3', 'DEFAULT_MAX_LOOSE_RUN = 3'),
			/options\.py: .*config\.py reads max-loose-run-size with DEFAULT_MAX_LOOSE_RUN_SIZE and MAX_LOOSE_RUN_SIZE_LIMIT/,
		);

		failsOnPython(
			'options',
			(text) => text.replace('DEFAULT_NAMED_ARGUMENT_MIN_ARGUMENTS = 4', 'DEFAULT_NAMED_ARGUMENT_MIN_ARGUMENTS = 5'),
			/options\.py: the default of named-argument-min-arguments differs/,
		);
	});

	it('fails with the file when the pair table and the modules describe other pairs', () => {
		const row =
			'| [`ZA3111`](rules/ZA3111-separate-multi-line-cases.md) and [`ZA3112`](rules/ZA3112-keep-multi-line-cases-together.md) | `ZA3111` | `ZA3112` |';

		const swapped = row.replace('| `ZA3111` | `ZA3112` |', '| `ZA3112` | `ZA3112` |');

		failsOnPython(
			'ruleMap',
			(text) => text.replace(row, swapped),
			/Rule-map\.md: the default half of ZA3111 and ZA3112 is ZA3112, but ZA3111 is in a level/,
		);

		failsOnPython(
			'ruleMap',
			(text) => text.replace(row, row.replace('| `ZA3112` |', '| `ZA3111` |')),
			/Rule-map\.md: ZA3111 and ZA3112 are owned by ZA3111 when both are enabled, but ZA3112 removes the blank line/,
		);

		failsOnPython(
			'ruleMap',
			(text) => text.replace(/^\| The blank line between two `case` clauses.*\n/m, ''),
			/Rule-map\.md: ZA3112 names the opposite ZA3111 in its module, but the pair table has no row for it/,
		);

		failsOnPython(
			'ruleCode:keep_multi_line_cases_together.py',
			(text) => text.replace('opposite="ZA3111"', 'opposite="ZA3001"'),
			/Rule-map\.md: the pair table pairs ZA3112 with ZA3111, but its module names ZA3001/,
		);

		failsOnPython(
			'ruleCode:separate_clause_keywords.py',
			(text) => text.replace('    opposite="ZA3202",\n', ''),
			/Rule-map\.md: the pair table pairs ZA3201 with ZA3202, but its module names no opposite/,
		);

		failsOnPython(
			'ruleCode:keep_clause_keywords_adjacent.py',
			(text) => text.replace('role="keep"', 'role="separate"'),
			/Rule-map\.md: ZA3201 and ZA3202 are owned by ZA3202 when both are enabled, but no half removes the blank line/,
		);
	});

	it('names the configuration page when a Keys cell holds Markdown the renderer refuses', () => {
		failsOnPython(
			'configuration',
			(text) => text.replace('The most statements', 'The __most__ statements'),
			/py\/docs\/Configuration\.md: .*bold with underscores/,
		);
	});

	it('names the configuration page when a row of the Keys table has another number of cells than its header', () => {
		failsOnPython(
			'configuration',
			(text) => text.replace('| `max-loose-run-size` | `3` |', '| `max-loose-run-size` | `3` | extra | cells |'),
			/py\/docs\/Configuration\.md: .*another number of cells/,
		);
	});

	it('names the page of a rule whose module gives it another name', () => {
		failsOnPython(
			'ruleCode:separate_guard_clauses.py',
			(text) => text.replace('name="separate-guard-clauses"', 'name="guard-clauses"'),
			/^py\/docs\/rules\/ZA3011-separate-guard-clauses\.md: ZA3011 is named guard-clauses/,
		);
	});
});

describe('a relative link of a guide page', () => {
	it('is resolved in the text and left alone in a code fence and a code span', () => {
		const page = [
			'See [the quirks](../Quirks.md#gaps) and `items[i](event)`.',
			'',
			'```ts',
			'handlers[i](event);',
			'```',
			'',
			'After [it](Other.md).',
		].join('\n');

		const lines = resolveLinks('js/docs/rules/a.md', page).split('\n');

		expect(lines[0]).toBe('See [the quirks](https://github.com/Nickelony/Stanza/blob/main/js/docs/Quirks.md#gaps) and `items[i](event)`.');
		expect(lines[3]).toBe('handlers[i](event);');
		expect(lines[6]).toBe('After [it](https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/Other.md).');
	});

	it('keeps a call in an example as the page writes it', () => {
		const sources = readJavascript(repositoryRoot);
		const page = sources.rules.find((source) => source.file.endsWith('/separate-guard-clauses.md'))!;

		page.text = page.text.replace('// Before\n', '// Before\nhandlers[i](event);\n');

		const rule = extractJavascript(sources).rules.find((candidate) => candidate.id === 'separate-guard-clauses')!;

		expect(rule.examples[0]!.before.code).toContain('handlers[i](event);');
	});
});

describe('a code fence of a guide page', () => {
	it('fails the build under a section that nothing reads', () => {
		failsOnJavascript(
			'rules:separate-guard-clauses.md',
			(text) => `${text.trimEnd()}\n\n## Notes\n\nA sentence.\n\n\`\`\`ts\nconst a = 1;\n\`\`\`\n`,
			/separate-guard-clauses\.md: the code fence under "## Notes"/,
		);
	});
});
