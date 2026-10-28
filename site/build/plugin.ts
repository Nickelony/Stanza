import { join } from 'node:path';
import { type Plugin, normalizePath } from 'vite';
import { extractCatalog, sourcePaths } from './extract.ts';
import { highlightCatalog } from './highlight.ts';

const virtualId = 'virtual:stanza-catalog';
const resolvedId = `\0${virtualId}`;

/**
 * Serves the catalog the pages under docs/ describe as the module `virtual:stanza-catalog`.
 * A page that does not parse fails the build with the name of its file.
 */
export function stanzaCatalog(repositoryRoot: string): Plugin {
	let cached: Promise<string> | null = null;

	const build = async (): Promise<string> => {
		const { catalog, css } = await highlightCatalog(extractCatalog(repositoryRoot));
		return `export const catalog = ${JSON.stringify(catalog)};\nexport const tokenCss = ${JSON.stringify(css)};\n`;
	};

	return {
		name: 'stanza-catalog',

		resolveId(id) {
			return id === virtualId ? resolvedId : null;
		},

		async load(id) {
			if (id !== resolvedId) {
				return null;
			}

			cached ??= build();

			try {
				return await cached;
			} catch (error) {
				cached = null;
				throw error;
			}
		},

		configureServer(server) {
			const watched = [sourcePaths.rules, sourcePaths.options, sourcePaths.readme, sourcePaths.markupBehavior, sourcePaths.markupDiagnostics].map(
				(path) => normalizePath(join(repositoryRoot, path)),
			);

			server.watcher.add(watched);

			const reload = (file: string): void => {
				if (!watched.some((path) => normalizePath(file).startsWith(path))) {
					return;
				}

				cached = null;

				const module = server.moduleGraph.getModuleById(resolvedId);

				if (module) {
					server.moduleGraph.invalidateModule(module);
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
