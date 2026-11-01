import { createHash } from 'node:crypto';
import { join } from 'node:path';

import { normalizePath, type Plugin } from 'vite';

import { languages } from '../../src/languages/index.ts';
import type { Language } from '../../src/languages/language.ts';
import { pages } from '../../src/languages/pages.ts';
import { previewImages } from '../preview.ts';

import { escapeHtml } from './markdown.ts';

/** The address the Pages workflow publishes the site at; a link preview needs every address whole. */
const siteUrl = 'https://nickelony.github.io/Stanza/';

/** Sets the theme before the first paint, so a dark visitor sees no light flash; the policy of each page allows it by its hash. */
const themeScript = `
			try {
				var saved = window.localStorage.getItem('stanza-theme');
				var dark = saved === 'light' || saved === 'dark' ? saved === 'dark' : window.matchMedia('(prefers-color-scheme: dark)').matches;
				document.documentElement.dataset.theme = dark ? 'dark' : 'light';
			} catch (error) {
				document.documentElement.dataset.theme = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
			}
		`;

/** The policy of a page: its own files, the styles Emotion adds as style elements, and the theme script by its hash. */
function policy(): string {
	const hash = createHash('sha256').update(themeScript).digest('base64');
	return `default-src 'self'; script-src 'self' 'sha256-${hash}'; style-src 'self' 'unsafe-inline'; object-src 'none'; base-uri 'none'; form-action 'none'`;
}

/** The directory of the page of a language under the site base. */
const pathOf = (language: Language): string => pages.find((page) => page.id === language.id)!.path;

/** The address of the page of a language. */
export const pageAddress = (language: Language): string => `${siteUrl}${pathOf(language)}`;

/** The HTML of the page of a language; every page is written from this one template. */
export function renderPage(language: Language): string {
	const address = pageAddress(language);
	const image = `${siteUrl}${previewImages[language.id].name}`;
	const meta = (key: 'property' | 'name', name: string, content: string): string => `<meta ${key}="${name}" content="${escapeHtml(content)}" />`;

	return `<!doctype html>
<html lang="en">
	<head>
		<meta charset="utf-8" />
		<meta name="viewport" content="width=device-width, initial-scale=1" />
		<meta name="color-scheme" content="light dark" />
		<meta http-equiv="Content-Security-Policy" content="${policy()}" />
		<link rel="icon" type="image/svg+xml" href="/src/icons/${language.id}.svg" />
		<link rel="apple-touch-icon" href="${image}" />
		${meta('name', 'description', language.description)}
		<title>${escapeHtml(language.title)}</title>
		<link rel="canonical" href="${address}" />
		${meta('property', 'og:type', 'website')}
		${meta('property', 'og:site_name', 'Stanza')}
		${meta('property', 'og:title', language.title)}
		${meta('property', 'og:description', language.description)}
		${meta('property', 'og:url', address)}
		${meta('property', 'og:image', image)}
		${meta('property', 'og:image:width', '256')}
		${meta('property', 'og:image:height', '256')}
		${meta('property', 'og:image:alt', 'The Stanza logo')}
		${meta('name', 'twitter:card', 'summary')}
		<script>${themeScript}</script>
	</head>
	<body>
		<div id="app"></div>
		<noscript
			><p class="no-script">
				The configurator needs JavaScript. ${escapeHtml(language.docs.lead)} <a href="${escapeHtml(language.docs.href)}">${escapeHtml(language.docs.name)}</a>.
			</p></noscript
		>
		<script type="module" src="/src/entries/${language.id}.ts"></script>
	</body>
</html>
`;
}

/**
 * Writes the HTML entry of each language from the template, so no page of the site is a file on disk.
 * The build takes each as an input, and the dev server answers each address with it.
 */
export function stanzaPages(root: string): Plugin {
	const entries = languages.map((language) => ({ language, file: normalizePath(join(root, pathOf(language), 'index.html')) }));

	return {
		name: 'stanza-pages',
		enforce: 'pre',

		config() {
			return { build: { rollupOptions: { input: Object.fromEntries(entries.map(({ language, file }) => [language.id, file])) } } };
		},

		resolveId(source) {
			return entries.some(({ file }) => file === normalizePath(source)) ? normalizePath(source) : null;
		},

		load(id) {
			const entry = entries.find(({ file }) => file === normalizePath(id));
			return entry === undefined ? null : renderPage(entry.language);
		},

		configureServer(server) {
			const base = server.config.base;

			server.middlewares.use(async (request, response, next) => {
				const url = (request.url ?? '').split(/[?#]/)[0]!;
				const path = url.startsWith(base) ? url.slice(base.length) : null;
				const language = languages.find((candidate) => pathOf(candidate) === path);

				if (language === undefined) {
					next();
					return;
				}

				response.setHeader('content-type', 'text/html');
				response.end(await server.transformIndexHtml(url, renderPage(language)));
			});
		},
	};
}
