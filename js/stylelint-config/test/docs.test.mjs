import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { describe, it } from 'node:test';
import { fileURLToPath, URL } from 'node:url';

import configuration from '../index.mjs';

import { check } from './helpers.mjs';

const packageDirectory = fileURLToPath(new URL('../', import.meta.url));
const pagesDirectory = `${packageDirectory}docs/rules/`;
const fence = '```';

/** The file name a fence is linted as, by the language the fence names. */
const filenames = { css: 'src/example.css', scss: 'src/example.scss', tsx: 'src/example.tsx' };

/** Reads the CSS and TSX fences of a page. */
function readFences(page) {
	return [...page.matchAll(new RegExp(`^${fence}(css|scss|tsx)\n([\\s\\S]*?)\n${fence}$`, 'gm'))].map((match) => ({
		language: match[1],
		text: match[2],
	}));
}

/** Splits a Before and After example into the source to fix and the text the fix writes. */
function splitExample(example) {
	const [before = '', after = ''] = example.split('\n\n/* After */\n');
	return { source: `${before.replace('/* Before */\n', '')}\n`, expected: `${after}\n` };
}

describe('the examples of the pages', () => {
	const pages = [...readdirSync(pagesDirectory).map((name) => `docs/rules/${name}`), 'README.md', 'docs/Syntax.md'];

	for (const page of pages) {
		it(`shows the real fix in each example of ${page}`, async () => {
			const fences = readFences(readFileSync(`${packageDirectory}${page}`, 'utf8'));

			for (const { language, text } of fences) {
				const { source, expected } = splitExample(text);
				assert.equal((await check(source, true, filenames[language])).code, expected, text);
			}
		});
	}
});

describe('the rule pages', () => {
	it('has one page for each rule and a rule for each page', () => {
		const names = readdirSync(pagesDirectory).map((name) => name.replace(/\.md$/, ''));
		assert.deepEqual(names.sort(), Object.keys(configuration.rules).sort());
	});

	for (const [name, setting] of Object.entries(configuration.rules)) {
		it(`shows the option set of ${name} as the config holds it`, () => {
			const page = readFileSync(`${pagesDirectory}${name}.md`, 'utf8');

			assert.ok(page.startsWith(`# ${name}\n`), 'the page opens with the rule name');
			assert.ok(page.includes(formatSetting(name, setting)), 'the page shows the option set');
		});
	}
});

/** Writes the entry of a rule the way the pages do, with the spaces of the source. */
function formatSetting(name, [primary, secondary]) {
	const entries = Object.entries(secondary).map(([key, value]) => `${key}: [${value.map((item) => `'${item}'`).join(', ')}]`);
	return `'${name}': ['${primary}', { ${entries.join(', ')} }]`;
}
