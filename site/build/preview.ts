import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import type { Plugin } from 'vite';

import type { LanguageId } from '../src/languages/language.ts';

/**
 * The image of each page's link preview: the logo of its product in `icon/`, published under a name that stays,
 * so a card a site cached still finds it after the logo changes. The C# page keeps the name the site first published.
 */
export const previewImages: Record<LanguageId, { source: string; name: string }> = {
	csharp: { source: 'Stanza.png', name: 'stanza.png' },
	markup: { source: 'Stanza-Mu.png', name: 'stanza-markup.png' },
	js: { source: 'Stanza-JS.png', name: 'stanza-js.png' },
	python: { source: 'Stanza-Py.png', name: 'stanza-py.png' },
};

/** Publishes the logo of each product as the image of a link preview, which the tags of its page name. */
export function stanzaPreview(repositoryRoot: string): Plugin {
	return {
		name: 'stanza-preview',
		apply: 'build',

		generateBundle() {
			for (const { source, name } of Object.values(previewImages)) {
				this.emitFile({ type: 'asset', fileName: name, source: readFileSync(join(repositoryRoot, 'icon', source)) });
			}
		},
	};
}
