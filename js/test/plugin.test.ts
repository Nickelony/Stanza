import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

import { Linter } from 'eslint';

import {
	defaultAssertionRoots,
	defaultGuardRoots,
	defaultMaxLooseRunSize,
	defaultTestRoots,
	greatestMaxLooseRunSize,
	leastMaxLooseRunSize,
	readSharedSettings,
} from '../src/analysis/environment.ts';
import { addOrder, removeOrder } from '../src/analysis/ruleNames.ts';
import plugin, { configs, rules } from '../src/index.ts';
import { optInRules } from '../src/presets.ts';

import { createConfig, fix, lines, readPresetRules, readRuleLevel } from './helpers.ts';

const read = (path: string): string => readFileSync(fileURLToPath(new URL(path, import.meta.url)), 'utf8');

describe('the presets', () => {
	it('enable no rule at none and more rules at each level', () => {
		const counts = [configs.none, configs.minimal, configs.moderate, configs.strong].map((config) => Object.keys(config.rules ?? {}).length);

		assert.equal(counts[0], 0);

		assert.deepEqual(
			counts,
			[...counts].sort((first, second) => first - second),
		);

		assert.equal(new Set(counts).size, counts.length);
	});

	it('hold every rule of the level before them', () => {
		const levels = [configs.minimal, configs.moderate, configs.strong].map((config) => Object.keys(config.rules ?? {}));

		for (const [index, names] of levels.entries()) {
			for (const earlier of levels.slice(0, index)) {
				assert.deepEqual(
					earlier.filter((name) => !names.includes(name)),
					[],
				);
			}
		}
	});

	it('enable only rules the plugin has, under the stanza prefix', () => {
		for (const config of Object.values(configs)) {
			for (const name of Object.keys(config.rules ?? {})) {
				assert.match(name, /^stanza\//);
				assert.ok(name.slice('stanza/'.length) in rules, name);
			}

			assert.equal(config.plugins?.['stanza'], plugin);
		}
	});

	it('enable the pair rule at the moderate level, and every rule but an opt-in one at the strong level', () => {
		assert.ok('stanza/keep-two-units-together' in (configs.moderate.rules ?? {}));

		assert.deepEqual(
			Object.keys(configs.strong.rules ?? {}).sort(),

			Object.keys(rules)
				.filter((name) => !(optInRules as readonly string[]).includes(name))
				.map((name) => `stanza/${name}`)
				.sort(),
		);
	});

	it('hold an opt-in rule in no preset, though the plugin registers it', () => {
		assert.ok(optInRules.length > 0);

		for (const name of optInRules) {
			assert.ok(name in rules, name);

			for (const [level, config] of Object.entries(configs)) {
				assert.ok(!(`stanza/${name}` in (config.rules ?? {})), `${name} is in the ${level} preset`);
			}
		}
	});
});

describe('the options', () => {
	it('reject a value the schema does not allow', () => {
		const code = lines('run();');

		assert.throws(
			() => new Linter().verify(code, createConfig({ 'separate-multi-line-constructs': ['error', { switchCases: true }] })),
			/should NOT have more than 0 items/,
		);

		assert.throws(
			() => new Linter().verify(code, createConfig({ 'separate-guard-clauses': ['error', { guardRoots: ['assert'] }] })),
			/should NOT have more than 0 items/,
		);

		assert.throws(
			() => new Linter().verify(code, createConfig({ 'separate-declaration-runs': ['error', { splitDeclarationKinds: false }] })),
			/should NOT have more than 0 items/,
		);

		assert.throws(
			() => new Linter().verify(code, createConfig({ 'separate-import-groups': ['error', { typeImports: 'yes' }] })),
			/should be boolean/,
		);

		assert.throws(() => new Linter().verify(code, createConfig({ 'separate-import-groups': ['error', { types: true }] })), /Unexpected property/);

		for (const name of ['gather-loose-statements', 'gather-loose-members', 'keep-statement-runs-together', 'keep-member-runs-together']) {
			assert.throws(
				() => new Linter().verify(code, createConfig({ [name]: ['error', { maxLooseRunSize: 3 }] })),
				/should NOT have more than 0 items/,
				name,
			);
		}

		assert.throws(
			() => new Linter().verify(code, createConfig({ 'separate-multi-line-elements': ['error', { objectMembers: 'yes' }] })),
			/should be boolean/,
		);

		assert.throws(
			() => new Linter().verify(code, createConfig({ 'separate-multi-line-elements': ['error', { elements: true }] })),
			/Unexpected property/,
		);
	});

	it('fill a missing option from the default options of the rule', () => {
		const code = lines('const o = {', '  a: 1,', '  b: {', '    c: 1,', '  },', '};');
		const messages = new Linter().verify(code, createConfig({ 'separate-multi-line-elements': ['error', {}] }));

		assert.equal(messages.length, 1, 'objectMembers stays true');
		assert.deepEqual(rules['separate-import-groups'].meta.defaultOptions, [{ typeImports: false }]);

		assert.deepEqual(rules['separate-multi-line-elements'].meta.defaultOptions, [
			{ objectMembers: true, arrayElements: true, enumMembers: true, callArguments: true, parameters: true },
		]);
	});
});

/** Reads the rule and the level of each row of a table that links the rules, in the order of the rows. */
function readLevels(markdown: string): [string, string][] {
	const row = /^\| (?:[^|]*\| )?\[`([a-z-]+)`\]\([^)]*\) \| (none|minimal|moderate|strong|opt-in) \|/gm;
	return [...markdown.matchAll(row)].map((match) => [match[1] ?? '', match[2] ?? '']);
}

describe('the shared settings', () => {
	it('read the default list of guard roots, or the one a setting gives', () => {
		assert.deepEqual([...readSharedSettings({}).guardRoots], [...defaultGuardRoots]);
		assert.deepEqual([...readSharedSettings({ stanza: { guardRoots: ['check'] } }).guardRoots], ['check']);
	});

	it('read an empty list as no roots, and a key that is left out as its default', () => {
		const empty = readSharedSettings({ stanza: { assertionRoots: [], testRoots: [], guardRoots: [] } });

		assert.deepEqual([...empty.assertionRoots, ...empty.testRoots, ...empty.guardRoots], []);

		for (const settings of [undefined, {}, { stanza: undefined }, { stanza: {} }, { stanza: { guardRoots: undefined } }]) {
			const shared = readSharedSettings(settings);

			assert.deepEqual([...shared.assertionRoots], [...defaultAssertionRoots]);
			assert.deepEqual([...shared.testRoots], [...defaultTestRoots]);
			assert.deepEqual([...shared.guardRoots], [...defaultGuardRoots]);
			assert.equal(shared.maxLooseRunSize, defaultMaxLooseRunSize);
		}
	});

	it('read a full configuration of every setting', () => {
		const shared = readSharedSettings({
			stanza: { assertionRoots: ['should'], testRoots: ['spec', 'scenario'], guardRoots: ['ensure'], maxLooseRunSize: 7 },
		});

		assert.deepEqual([...shared.assertionRoots], ['should']);
		assert.deepEqual([...shared.testRoots], ['spec', 'scenario']);
		assert.deepEqual([...shared.guardRoots], ['ensure']);
		assert.equal(shared.maxLooseRunSize, 7);
	});

	it('read the limit of a loose run as a whole number from the least to the greatest', () => {
		assert.deepEqual([leastMaxLooseRunSize, defaultMaxLooseRunSize, greatestMaxLooseRunSize], [0, 3, 1000]);

		for (const value of [leastMaxLooseRunSize, 1, greatestMaxLooseRunSize]) {
			assert.equal(readSharedSettings({ stanza: { maxLooseRunSize: value } }).maxLooseRunSize, value);
		}
	});

	it('read one settings object once, however many rules and files ask', () => {
		const settings = { stanza: { guardRoots: ['ensure'] } };
		assert.equal(readSharedSettings(settings), readSharedSettings(settings));
	});

	it('reject an unknown key, and name the keys that exist', () => {
		const known = 'the settings are assertionRoots, guardRoots, maxLooseRunSize and testRoots.';

		assert.throws(
			() => readSharedSettings({ stanza: { maxLooseRunsize: 5 } }),
			new Error(`Stanza: unknown setting "maxLooseRunsize" in settings.stanza; ${known}`),
		);

		assert.throws(() => readSharedSettings({ stanza: { guardRoots: ['assert'], extra: true } }), /unknown setting "extra" in settings.stanza/);
	});

	it('reject a list that is no array, and name the key and the value', () => {
		for (const key of ['assertionRoots', 'testRoots', 'guardRoots']) {
			assert.throws(
				() => readSharedSettings({ stanza: { [key]: 'expect' } }),
				new Error(`Stanza: setting "${key}" must be an array of strings, but is "expect".`),
			);

			for (const value of [7, null, true, {}]) {
				assert.throws(() => readSharedSettings({ stanza: { [key]: value } }), /must be an array of strings, but is/, String(value));
			}
		}
	});

	it('reject a list that holds an item that is no string, and name the item', () => {
		assert.throws(
			() => readSharedSettings({ stanza: { testRoots: ['it', 2] } }),
			new Error('Stanza: setting "testRoots" must be an array of strings, but item 1 is 2.'),
		);

		assert.throws(() => readSharedSettings({ stanza: { guardRoots: [null] } }), /but item 0 is null./);
	});

	it('reject a limit that is no whole number from the least to the greatest, and name the value', () => {
		const range = `must be a whole number from ${leastMaxLooseRunSize} to ${greatestMaxLooseRunSize}`;

		assert.throws(
			() => readSharedSettings({ stanza: { maxLooseRunSize: 'five' } }),
			new Error(`Stanza: setting "maxLooseRunSize" ${range}, but is "five".`),
		);

		const invalid: [unknown, string][] = [
			[2.5, '2.5'],
			[-1, '-1'],
			[1001, '1001'],
			[Number.NaN, 'NaN'],
			[Number.POSITIVE_INFINITY, 'Infinity'],
			['4', '"4"'],
			[null, 'null'],
			[true, 'true'],
			[[4], '[4]'],
		];

		for (const [value, shown] of invalid) {
			assert.throws(
				() => readSharedSettings({ stanza: { maxLooseRunSize: value } }),
				new Error(`Stanza: setting "maxLooseRunSize" ${range}, but is ${shown}.`),
			);
		}
	});

	it('reject a settings.stanza that is no object of settings', () => {
		for (const value of ['strict', 3, null, true, ['guardRoots']]) {
			assert.throws(
				() => readSharedSettings({ stanza: value }),
				/^Error: Stanza: settings.stanza must be an object of settings, but is .*; the settings are assertionRoots, guardRoots, maxLooseRunSize and testRoots.$/,
				String(value),
			);
		}
	});

	it('fail the lint run of a rule that is enabled, whichever rule it is, with a message that names the setting', () => {
		const code = lines('run();');

		const message =
			/Stanza: unknown setting "maxLooseRunsize" in settings.stanza; the settings are assertionRoots, guardRoots, maxLooseRunSize and testRoots./;

		for (const name of Object.keys(rules)) {
			assert.throws(() => new Linter().verify(code, createConfig({ [name]: 'error' }, { stanza: { maxLooseRunsize: 5 } })), message, name);
		}

		assert.throws(
			() => new Linter().verify(code, createConfig({ 'separate-guard-clauses': 'error' }, { stanza: { maxLooseRunSize: 'five' } })),
			/Stanza: setting "maxLooseRunSize" must be a whole number from 0 to 1000, but is "five"./,
		);

		assert.throws(
			() => new Linter().verify(code, createConfig(readPresetRules('strong'), { stanza: { assertionRoots: 'expect' } })),
			/Stanza: setting "assertionRoots" must be an array of strings, but is "expect"./,
		);
	});

	it('fail no lint run where no Stanza rule runs, because nothing reads the settings', () => {
		const code = lines('run();');
		const settings = { stanza: { maxLooseRunsize: 5 } };

		assert.deepEqual(new Linter().verify(code, createConfig(readPresetRules('none'), settings)), []);
		assert.deepEqual(new Linter().verify(code, createConfig({ 'separate-guard-clauses': 'off' }, settings)), []);
	});

	it('pass a lint run that has a full valid configuration', () => {
		const code = lines('run();');
		const settings = { stanza: { assertionRoots: ['should'], testRoots: ['spec'], guardRoots: ['ensure'], maxLooseRunSize: 0 } };

		assert.deepEqual(new Linter().verify(code, createConfig(readPresetRules('strong'), settings)), []);
	});

	it('give both rules that gather loose runs the one limit', () => {
		const code = lines(
			'a();',
			'',
			'b();',
			'',
			'c();',
			'',
			'd();',
			'',
			'class A {',
			'  a = 1;',
			'',
			'  b = 2;',
			'',
			'  c = 3;',
			'  d = 4;',
			'',
			'  e = 5;',
			'}',
		);

		const rules = { 'gather-loose-statements': 'error', 'gather-loose-members': 'error' } as const;
		const strict = lines('a();', 'b();', 'c();', 'd();', '', 'class A {', '  a = 1;', '  b = 2;', '', '  c = 3;', '  d = 4;', '', '  e = 5;', '}');

		assert.equal(fix(code, rules, { stanza: { maxLooseRunSize: 1 } }), strict);
		assert.equal(fix(code, rules, { stanza: { maxLooseRunSize: 5 } }), code);
		assert.equal(fix(code, rules, { stanza: { maxLooseRunSize: 0 } }), code);
	});
});

describe('the documentation', () => {
	it('lists each rule, at the level of its preset and in the order of the presets, in the readme', () => {
		const listed = readLevels(read('../README.md'));

		assert.deepEqual(
			listed.map(([name]) => name),
			[...Object.keys(configs.strong.rules ?? {}).map((name) => name.replace('stanza/', '')), ...optInRules],
		);

		for (const [name, level] of listed) {
			assert.equal(level, readRuleLevel(name), `${name} is listed at another level than its preset`);
		}
	});

	it('lists each rule of the plugin at the level of its preset in the rule map', () => {
		const listed = new Map(readLevels(read('../docs/Rule-map.md')));

		assert.deepEqual([...listed.keys()].sort(), Object.keys(rules).sort());

		for (const [name, level] of listed) {
			assert.equal(level, readRuleLevel(name), `${name} is listed at another level than its preset`);
		}
	});

	it('lists the owner order of the rule map in the order of the code', () => {
		const map = read('../docs/Rule-map.md');
		const section = map.slice(map.indexOf('## One owner per gap'));
		const listed = [...section.matchAll(/^\d+\. `([a-z-]+)`$/gm)].map((match) => match[1]);

		assert.deepEqual(listed, [...addOrder]);
	});

	it('lists the order of the rules that remove a gap in the rule map in the order of the code', () => {
		const map = read('../docs/Rule-map.md');
		const section = /^## Rules that remove a gap$([\s\S]*)/m.exec(map)?.[1] ?? '';
		const listed = [...section.matchAll(/`([a-z-]+)`/g)].map((match) => match[1]);

		assert.deepEqual(listed, [...removeOrder]);
	});
});
