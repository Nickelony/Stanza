import type { LanguageId } from '../src/languages/language.ts';
import type { SourceCatalog } from '../src/state/model.ts';

import { extractCatalog } from './readers/extract.ts';
import { extractJavascript, readJavascript } from './readers/javascript.ts';
import { splitMarkup } from './readers/markup.ts';
import { extractPython, readPython } from './readers/python.ts';
import { jsSourcePaths, pythonSourcePaths, sourcePaths } from './readers/source.ts';

/** What the build needs to turn the pages of one language into its catalog. */
export interface CatalogReader {
	/** Reads the pages under a repository root; fails with the name of a file it cannot parse. */
	read(root: string): SourceCatalog;

	/** The files and directories the catalog depends on, relative to the repository root. */
	paths: readonly string[];
}

export const catalogReaders: Record<LanguageId, CatalogReader> = {
	csharp: { read: (root) => splitMarkup(extractCatalog(root)).csharp, paths: Object.values(sourcePaths) },
	markup: { read: (root) => splitMarkup(extractCatalog(root)).markup, paths: Object.values(sourcePaths) },
	js: { read: (root) => extractJavascript(readJavascript(root)), paths: Object.values(jsSourcePaths) },
	python: { read: (root) => extractPython(readPython(root)), paths: Object.values(pythonSourcePaths) },
};
