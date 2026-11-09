import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

import type { Linter } from 'eslint';

import {
	defaultAssertionRoots,
	defaultGuardRoots,
	defaultMaxLooseRunSize,
	defaultTestRoots,
	greatestMaxLooseRunSize,
	leastMaxLooseRunSize,
	readSharedSettings,
} from '../src/analysis/environment.ts';
import { rules } from '../src/index.ts';

import { fix, readRuleLevel } from './helpers.ts';

const pagesDirectory = fileURLToPath(new URL('../docs/rules/', import.meta.url));
const fence = '```';

/** The rules a page enables beside its own, because the rule reads the runs of the others. */
const companions: Record<string, Record<string, Linter.RuleEntry>> = {
	'keep-statement-runs-together': { 'separate-invocation-runs': 'error' },
	'keep-member-runs-together': { 'separate-member-groups': 'error' },
};

/** The file name a page's examples are parsed as, for a page whose examples hold JSX. */
const filenames: Record<string, string> = {
	'separate-multi-line-jsx-children': 'file.tsx',
	'keep-single-line-jsx-children-together': 'file.tsx',
	'keep-multi-line-jsx-children-together': 'file.tsx',
	'separate-commented-jsx-children': 'file.tsx',
};

/** The entry of the rule for the examples of a page that show an option, by the position of the example; the others run with the defaults. */
const fenceEntries: Record<string, Linter.RuleEntry[]> = {
	'separate-import-groups': ['error', ['error', { typeImports: true }]],
};

/** Reads the TypeScript fences of a page. */
function readFences(page: string): string[] {
	return [...page.matchAll(new RegExp(`^${fence}ts\n([\\s\\S]*?)\n${fence}$`, 'gm'))].map((match) => match[1] ?? '');
}

/** Splits a Before and After example into the source to fix, the text the fix writes and the texts it leaves as written. */
function splitExample(example: string): { source: string; expected: string; unchanged: string[] } {
	const [before = '', rest = ''] = example.split('\n\n// After\n');
	const [after = '', ...unchanged] = rest.split(/\n\n\/\/ Unchanged[^\n]*\n/);

	return { source: `${before.replace('// Before\n', '')}\n`, expected: `${after}\n`, unchanged: unchanged.map((text) => `${text}\n`) };
}

describe('the rule pages', () => {
	it('has one page for each rule and a rule for each page', () => {
		const pages = readdirSync(pagesDirectory).map((name) => name.replace(/\.md$/, ''));
		assert.deepEqual(pages.sort(), Object.keys(rules).sort());
	});

	for (const name of Object.keys(rules)) {
		it(`shows the real fix of ${name} in each of its TypeScript examples`, () => {
			const fences = readFences(readFileSync(`${pagesDirectory}${name}.md`, 'utf8'));

			assert.ok(fences.length > 0, `${name} has no example`);

			for (const [index, example] of fences.entries()) {
				const { source, expected, unchanged } = splitExample(example);
				const enabled: Record<string, Linter.RuleEntry> = { [name]: fenceEntries[name]?.[index] ?? 'error', ...companions[name] };

				assert.equal(fix(source, enabled, {}, filenames[name]), expected);

				for (const text of unchanged) {
					assert.ok(text.trim() !== '', `${name} has an empty Unchanged example`);
					assert.equal(fix(text, enabled, {}, filenames[name]), text);
				}
			}
		});
	}

	it('names on each page the level of the preset that holds the rule, or opt-in for a rule no preset holds', () => {
		for (const name of Object.keys(rules)) {
			const page = readFileSync(`${pagesDirectory}${name}.md`, 'utf8');
			assert.equal(/^\*\*Level: ([a-z-]+)\.\*\*$/m.exec(page)?.[1], readRuleLevel(name), name);
		}
	});

	it('links the documentation of each rule from its metadata', () => {
		for (const [name, rule] of Object.entries(rules)) {
			assert.equal(rule.meta.docs?.url, `https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/${name}.md`);
		}
	});
});

describe('the option tables', () => {
	for (const [name, rule] of Object.entries(rules)) {
		const [defaults] = rule.meta.defaultOptions as unknown[];

		if (typeof defaults !== 'object' || defaults === null) {
			continue;
		}

		it(`lists the options of ${name} with the defaults the rule has`, () => {
			const page = readFileSync(`${pagesDirectory}${name}.md`, 'utf8');
			const rows = [...page.matchAll(/^\| `(\w+)` \| `([^`]+)` \|/gm)];

			assert.deepEqual(Object.fromEntries(rows.map((row) => [row[1], JSON.parse(row[2] ?? '')])), defaults);
		});
	}
});

describe('the loose run limit', () => {
	it('is documented with the bounds the settings reader allows', () => {
		const page = readFileSync(`${pagesDirectory}gather-loose-statements.md`, 'utf8');

		assert.ok(page.includes(`from ${leastMaxLooseRunSize} to ${greatestMaxLooseRunSize}`));
		assert.ok(readSettingsSection(page).includes(`maxLooseRunSize: ${defaultMaxLooseRunSize}`));
	});

	it('is linked from the page of the sibling rule, which owns no section of its own for it', () => {
		const page = readFileSync(`${pagesDirectory}gather-loose-members.md`, 'utf8');

		assert.ok(page.includes('(gather-loose-statements.md#settings)'));
		assert.equal(readSettingsSection(page), '');
	});
});

describe('the readme', () => {
	it('shows the real fix of separate-jump-statements in its example', () => {
		const fences = readFences(readFileSync(fileURLToPath(new URL('../README.md', import.meta.url)), 'utf8'));
		const { source, expected } = splitExample(fences[0] ?? '');

		assert.equal(fences.length, 1);
		assert.equal(fix(source, { 'separate-jump-statements': 'error' }), expected);
	});
});

/** Reads the `## Settings` section of a page, or an empty text. */
function readSettingsSection(page: string): string {
	return /^## Settings$([\s\S]*?)(?=^## |(?![\s\S]))/m.exec(page)?.[1] ?? '';
}

describe('the settings', () => {
	const keys = Object.keys(readSharedSettings({})).sort();

	const defaults: Record<string, readonly string[] | number> = {
		assertionRoots: defaultAssertionRoots,
		testRoots: defaultTestRoots,
		guardRoots: defaultGuardRoots,
		maxLooseRunSize: defaultMaxLooseRunSize,
	};

	/** Reads the keys a text shows as `key: ['name', ...]` or `key: 3`, with the names or the number each gives. */
	function readSettingKeys(markdown: string): Map<string, string[] | number> {
		const settings = [...markdown.matchAll(/\b(\w+): (?:\[([^\]]*)\]|(\d+))/g)];

		return new Map(
			settings.map((match) => [
				match[1] ?? '',
				match[3] === undefined ? [...(match[2] ?? '').matchAll(/'([^']+)'/g)].map((name) => name[1] ?? '') : Number(match[3]),
			]),
		);
	}

	it('are the keys the code reads, each with a default the code holds', () => {
		assert.deepEqual(Object.keys(defaults).sort(), keys);
	});

	it('are shown by the settings page, once each, as the keys the code reads', () => {
		const page = readFileSync(fileURLToPath(new URL('../docs/Settings.md', import.meta.url)), 'utf8');
		assert.deepEqual([...readSettingKeys(page).keys()].sort(), keys);
	});

	it('are shown on the page of one rule each, and the guard page shows the default of the code', () => {
		const shown = new Map<string, string>();

		for (const name of Object.keys(rules)) {
			const section = readSettingsSection(readFileSync(`${pagesDirectory}${name}.md`, 'utf8'));

			for (const [key, names] of readSettingKeys(section)) {
				assert.ok(keys.includes(key), `${name} shows the setting ${key}, which the code does not read`);
				assert.ok(!shown.has(key), `${key} is shown by ${shown.get(key)} and ${name}`);

				if (key === 'guardRoots') {
					assert.deepEqual(names, [...defaultGuardRoots], `${name} shows another default than the code`);
				}

				if (key === 'maxLooseRunSize') {
					assert.equal(names, defaultMaxLooseRunSize, `${name} shows another default than the code`);
				}

				shown.set(key, name);
			}
		}

		assert.deepEqual([...shown.keys()].sort(), keys);
	});

	it('are listed with their defaults on the pages that own them', () => {
		const pages: [string, readonly string[]][] = [
			['separate-assertion-runs', defaultAssertionRoots],
			['separate-test-blocks', defaultTestRoots],
		];

		for (const [name, roots] of pages) {
			const page = readFileSync(`${pagesDirectory}${name}.md`, 'utf8');

			for (const root of roots) {
				assert.ok(page.includes(`\`${root}\``), `${name} does not list the default root ${root}`);
			}
		}
	});
});
