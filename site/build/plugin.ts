import { join } from 'node:path';

import { normalizePath, type Plugin } from 'vite';

import type { LanguageId } from '../src/languages/language.ts';
import { compactHtml } from '../src/util/compact.ts';

import { catalogReaders } from './catalogs.ts';

import { highlightCatalog } from './render/highlight.ts';

const virtualPrefix = 'virtual:stanza-catalog/';
const resolvedPrefix = `\0${virtualPrefix}`;
const languageIds = Object.keys(catalogReaders) as LanguageId[];

/** Applies the change to every string of a JSON value, and returns the new value. */
export function mapStrings(value: unknown, change: (text: string) => string): unknown {
	if (typeof value === 'string') {
		return change(value);
	}

	if (Array.isArray(value)) {
		return value.map((item) => mapStrings(item, change));
	}

	if (value !== null && typeof value === 'object') {
		return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, mapStrings(item, change)]));
	}

	return value;
}

/**
 * Writes a JSON value as a call to `JSON.parse` on a string, which V8 parses faster than the same data as an object literal.
 * The string is single quoted, so the quotes of the JSON need no escape; only the backslash and the single quote do.
 */
export function asParsed(value: unknown): string {
	return `JSON.parse('${JSON.stringify(value).replaceAll('\\', '\\\\').replaceAll("'", "\\'")}')`;
}

/**
 * Serves the catalog the pages of each language describe as the module `virtual:stanza-catalog/<language>`.
 * A page that does not parse fails the build with the name of its file.
 */
export function stanzaCatalog(repositoryRoot: string): Plugin {
	const cached = new Map<LanguageId, Promise<string>>();

	const build = async (id: LanguageId): Promise<string> => {
		const { catalog, css } = await highlightCatalog(catalogReaders[id].read(repositoryRoot), id);
		return `export const catalog = ${asParsed(mapStrings(catalog, compactHtml))};\nexport const tokenCss = ${JSON.stringify(css)};\n`;
	};

	return {
		name: 'stanza-catalog',

		resolveId(source) {
			return source.startsWith(virtualPrefix) ? `\0${source}` : null;
		},

		async load(id) {
			const language = id.startsWith(resolvedPrefix) ? (id.slice(resolvedPrefix.length) as LanguageId) : undefined;

			if (language === undefined || !languageIds.includes(language)) {
				return null;
			}

			if (!cached.has(language)) {
				cached.set(language, build(language));
			}

			try {
				return await cached.get(language);
			} catch (error) {
				cached.delete(language);
				throw error;
			}
		},

		configureServer(server) {
			const watched = languageIds.flatMap((id) => catalogReaders[id].paths).map((path) => normalizePath(join(repositoryRoot, path)));

			server.watcher.add(watched);

			const reload = (file: string): void => {
				if (!watched.some((path) => normalizePath(file).startsWith(path))) {
					return;
				}

				cached.clear();

				for (const id of languageIds) {
					const module = server.moduleGraph.getModuleById(`${resolvedPrefix}${id}`);

					if (module) {
						server.moduleGraph.invalidateModule(module);
					}
				}

				server.ws.send({ type: 'full-reload' });
			};

			// A new rule page and a deleted one change the catalog as much as an edited one.
			for (const event of ['add', 'change', 'unlink'] as const) {
				server.watcher.on(event, reload);
			}
		},
	};
}
