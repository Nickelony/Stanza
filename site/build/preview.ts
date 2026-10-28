import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { HtmlTagDescriptor, Plugin } from 'vite';

/** The address the Pages workflow publishes the site at; a link preview needs every address whole. */
export const siteUrl = 'https://nickelony.github.io/Stanza/';

/** The preview image keeps one name, so a card a site cached still finds it after the logo changes. */
const imageName = 'stanza.png';

function readHead(html: string, pattern: RegExp, what: string): string {
	const match = pattern.exec(html);

	if (!match?.[1]) {
		throw new Error(`index.html has no ${what}, which the link preview repeats.`);
	}

	return match[1];
}

/**
 * Writes the canonical address and the Open Graph and Twitter card tags, from the title and description of index.html,
 * and publishes the logo as the preview image.
 */
export function stanzaPreview(repositoryRoot: string): Plugin {
	return {
		name: 'stanza-preview',
		apply: 'build',

		generateBundle() {
			this.emitFile({ type: 'asset', fileName: imageName, source: readFileSync(join(repositoryRoot, 'icon', 'Stanza.png')) });
		},

		transformIndexHtml(html): HtmlTagDescriptor[] {
			const title = readHead(html, /<title>([^<]+)<\/title>/, 'title');

			// Prettier wraps a long tag, so the attributes may sit on lines of their own.
			const description = readHead(html, /<meta\s+name="description"\s+content="([^"]+)"/, 'description');

			const meta = (key: 'property' | 'name', name: string, content: string): HtmlTagDescriptor => ({
				tag: 'meta',
				attrs: { [key]: name, content },
				injectTo: 'head',
			});

			return [
				{ tag: 'link', attrs: { rel: 'canonical', href: siteUrl }, injectTo: 'head' },
				meta('property', 'og:type', 'website'),
				meta('property', 'og:site_name', 'Stanza'),
				meta('property', 'og:title', title),
				meta('property', 'og:description', description),
				meta('property', 'og:url', siteUrl),
				meta('property', 'og:image', `${siteUrl}${imageName}`),
				meta('property', 'og:image:width', '256'),
				meta('property', 'og:image:height', '256'),
				meta('property', 'og:image:alt', 'The Stanza logo'),
				meta('name', 'twitter:card', 'summary'),
			];
		},
	};
}
