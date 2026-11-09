import { createHash } from 'node:crypto';

import { describe, expect, it } from 'vitest';

import { previewImages } from '../../../build/preview.ts';
import { pageAddress, renderPage } from '../../../build/render/pages.ts';
import { languages } from '../../../src/languages/index.ts';

/** The `'sha256-…'` source that allows the text of an inline script, which the browser hashes byte for byte. */
const hashSource = (script: string): string => `'sha256-${createHash('sha256').update(script).digest('base64')}'`;

describe.each(languages.map((language) => [language.id, renderPage(language)] as const))('the page of %s', (id, page) => {
	const policy = /<meta\s+http-equiv="Content-Security-Policy"\s+content="([^"]+)"/.exec(page)?.[1] ?? '';
	const scriptSources = (/(?:^|;)\s*script-src\s+([^;]+)/.exec(policy)?.[1] ?? '').split(/\s+/);

	/** The text of each script of the page that has no `src`. */
	const inlineScripts = [...page.matchAll(/<script(?![^>]*\ssrc=)[^>]*>([\s\S]*?)<\/script>/g)].map((match) => match[1]!);

	it('allows each inline script by its hash, so editing the theme script keeps the page working', () => {
		expect(inlineScripts.length).toBeGreaterThan(0);

		for (const script of inlineScripts) {
			expect(scriptSources, `The policy of the ${id} page must list ${hashSource(script)} in script-src.`).toContain(hashSource(script));
		}
	});

	it('allows no script that the page does not carry', () => {
		expect(scriptSources).not.toContain("'unsafe-inline'");
		expect(scriptSources).not.toContain("'unsafe-eval'");
		expect(scriptSources.filter((source) => source.startsWith("'sha256-"))).toHaveLength(inlineScripts.length);
	});

	it('names its own address and entry, and loads the catalog of its language only', () => {
		expect(page).toContain(`<link rel="canonical" href="${pageAddress(languages.find((language) => language.id === id)!)}" />`);
		expect(page).toContain(`src="/src/entries/${id}.ts"`);
		expect(page.match(/<script type="module"/g)).toHaveLength(1);
	});

	it('shows its own logo in the tab, and its own image in a link preview and on a home screen', () => {
		const image = /property="og:image" content="([^"]+)"/.exec(page)?.[1];

		expect(page).toContain(`<link rel="icon" type="image/svg+xml" href="/src/icons/${id}.svg" />`);
		expect(image?.split('/').at(-1)).toBe(previewImages[id].name);
		expect(page).toContain(`<link rel="apple-touch-icon" href="${image}" />`);
	});
});

describe('the pages of the site', () => {
	it('differ in title, description, canonical address, og:url, tab icon and preview image, and share the rest of the template', () => {
		const rendered = languages.map((language) => renderPage(language));
		const unique = (pattern: RegExp): Set<string | undefined> => new Set(rendered.map((page) => pattern.exec(page)?.[1]));

		for (const pattern of [
			/<title>([^<]+)</,
			/name="description" content="([^"]+)"/,
			/rel="canonical" href="([^"]+)"/,
			/property="og:url" content="([^"]+)"/,
			/rel="icon" type="image\/svg\+xml" href="([^"]+)"/,
			/property="og:image" content="([^"]+)"/,
		]) {
			expect(unique(pattern).size, String(pattern)).toBe(languages.length);
		}

		expect(unique(/(Content-Security-Policy" content="[^"]+")/).size).toBe(1);
	});
});

describe('the page of Markup', () => {
	const language = languages.find((candidate) => candidate.id === 'markup')!;
	const page = renderPage(language);

	it('answers under its own directory, with its own title and description', () => {
		expect(pageAddress(language)).toBe('https://nickelony.github.io/Stanza/markup/');
		expect(page).toContain('<title>Markup Stanza configurator</title>');
		expect(page).toContain('<meta property="og:url" content="https://nickelony.github.io/Stanza/markup/" />');
		expect(page).toContain('<meta property="og:title" content="Markup Stanza configurator" />');
		expect(page).toContain('stanza-markup');
	});

	it('links the wiki for the rule pages, as the C# page does', () => {
		expect(page).toContain('href="https://github.com/Nickelony/Stanza/wiki"');
	});
});
