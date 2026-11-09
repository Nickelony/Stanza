import { runInNewContext } from 'node:vm';

import { describe, expect, it } from 'vitest';

import { asParsed, mapStrings } from '../../build/plugin.ts';
import { highlightCatalog } from '../../build/render/highlight.ts';
import { blobUrl, wikiUrl } from '../../src/address/links.ts';
import { compactHtml, expandHtml } from '../../src/util/compact.ts';
import { realCatalog } from '../fixtures.ts';

const rule = `<a href="${wikiUrl}/ZA0006-Separate-await-runs" target="_blank" rel="noopener" data-select="ZA0006"><code>ZA0006</code></a>`;
const option = `<a href="${wikiUrl}/stanza_assertion_roots#values" target="_blank" rel="noopener" data-select="stanza_assertion_roots">roots</a>`;
const page = `<a href="${wikiUrl}/Terms" target="_blank" rel="noopener">Terms</a>`;
const jsRule = `<a href="${blobUrl}/js/docs/rules/separate-await-runs.md#options" target="_blank" rel="noopener" data-select="separate-await-runs">x</a>`;
const pyRule = `<a href="${blobUrl}/py/docs/rules/ZA3006-separate-await-runs.md" target="_blank" rel="noopener" data-select="ZA3006">x</a>`;
const guidePage = `<a href="${blobUrl}/js/docs/Quirks.md#a" target="_blank" rel="noopener">quirks</a>`;
const elsewhere = '<a href="https://example.test/a?b=1&amp;c=2" target="_blank" rel="noopener">out</a>';

describe('the compact form of a link', () => {
	it.each([
		['a rule', rule, '<a href="~/ZA0006-Separate-await-runs"><code>ZA0006</code></a>'],
		['an option with a fragment', option, '<a href="~/stanza_assertion_roots#values">roots</a>'],
		['another wiki page', page, '<a href="~/Terms">Terms</a>'],
		['a JS rule page', jsRule, `<a href="${blobUrl}/js/docs/rules/separate-await-runs.md#options">x</a>`],
		['a Python rule page', pyRule, `<a href="${blobUrl}/py/docs/rules/ZA3006-separate-await-runs.md">x</a>`],
		['a JS page that is no rule', guidePage, `<a href="${blobUrl}/js/docs/Quirks.md#a">quirks</a>`],
		['a page outside the wiki', elsewhere, '<a href="https://example.test/a?b=1&amp;c=2">out</a>'],
	])('keeps only the address of %s, and writes it back whole', (_name, full, compact) => {
		expect(compactHtml(full)).toBe(compact);
		expect(expandHtml(compact)).toBe(full);
	});

	it('leaves a link that is already whole, and text with none', () => {
		expect(expandHtml(rule)).toBe(rule);
		expect(expandHtml('a <code>b</code> c')).toBe('a <code>b</code> c');
	});

	it('refuses a link it would not write back as it stood', () => {
		const odd = `<a href="${wikiUrl}/ZA0006-Separate-await-runs" target="_blank" rel="noopener" data-select="ZA0007">x</a>`;
		expect(() => compactHtml(odd)).toThrowError(/cannot be written in the compact form/);
	});
});

describe('the compact form of a token', () => {
	it('shortens the span of a token and writes it back whole', () => {
		const full = '<span class="t0">var</span> x = <span class="t12">1</span>;';

		expect(compactHtml(full)).toBe('<t0>var</t> x = <t12>1</t>;');
		expect(expandHtml('<t0>var</t> x = <t12>1</t>;')).toBe(full);
	});

	it('leaves the other spans of the page alone', () => {
		const blank = '<span class="blank-note">blank line</span>';

		expect(compactHtml(blank)).toBe(blank);
		expect(expandHtml(blank)).toBe(blank);
	});
});

describe('the catalog of the page', () => {
	it('is smaller in the compact form, and every string of it comes back as it was', async () => {
		const { catalog } = await highlightCatalog(realCatalog);
		const compact = mapStrings(catalog, compactHtml);

		expect(mapStrings(compact, expandHtml)).toEqual(catalog);

		const full = JSON.stringify(catalog);
		const short = JSON.stringify(compact);

		expect(short).not.toContain('target=');
		expect(short).not.toContain('data-select');
		expect(short).not.toContain('<span class');
		expect(short.length).toBeLessThan(full.length * 0.9);
	}, 60_000);
});

describe('the module of the catalog', () => {
	it('parses back to the value, whatever quotes, backslashes and line separators its strings hold', () => {
		const value = {
			quote: `it's "quoted"`,
			slash: String.raw`a\b\\c`,
			lines: `one${String.fromCharCode(10)}two${String.fromCharCode(0x2028)}three`,
			html: '<a href="x">y</a>',
			list: [1, null, true],
		};

		expect(runInNewContext(asParsed(value))).toEqual(value);
	});
});
