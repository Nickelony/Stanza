import { readdirSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { type ExtractionSources, type SourceFile, extractCatalog, sourcePaths } from '../build/extract.ts';
import type { Catalog, Option, Rule, Snippet, SourceCatalog } from '../src/model.ts';

export const repositoryRoot = resolve(import.meta.dirname, '..', '..');

export const realCatalog: SourceCatalog = extractCatalog(repositoryRoot);

function folder(directory: string): SourceFile[] {
	return readdirSync(join(repositoryRoot, directory))
		.sort()
		.map((name) => ({ file: `${directory}/${name}`, text: readFileSync(join(repositoryRoot, directory, name), 'utf8') }));
}

function one(file: string): SourceFile {
	return { file, text: readFileSync(join(repositoryRoot, file), 'utf8') };
}

/** The real pages as the extraction reads them, so a test can break one and expect the build to fail on it. */
export function realSources(): ExtractionSources {
	return {
		rules: folder(sourcePaths.rules),
		options: folder(sourcePaths.options),
		readme: one(sourcePaths.readme),
		markupBehavior: one(sourcePaths.markupBehavior),
		markupDiagnostics: one(sourcePaths.markupDiagnostics),
	};
}

/** Replaces the text of one file of a source set. */
export function withText(sources: ExtractionSources, file: string, change: (text: string) => string): ExtractionSources {
	const swap = (entry: SourceFile): SourceFile => (entry.file === file ? { file, text: change(entry.text) } : entry);

	return {
		rules: sources.rules.map(swap),
		options: sources.options.map(swap),
		readme: swap(sources.readme),
		markupBehavior: swap(sources.markupBehavior),
		markupDiagnostics: swap(sources.markupDiagnostics),
	};
}

const snippet: Snippet = { lines: [{ text: 'x', html: 'x' }] };

export function rule(id: string, overrides: Partial<Rule<Snippet>> = {}): Rule<Snippet> {
	return {
		id,
		title: `Title of ${id}`,
		group: 'Group A',
		summary: 'Adds a blank line',
		tier: 'warning',
		onByDefault: true,
		markupOnByDefault: false,
		readByMarkup: false,
		tierText: 'Warning, on by default',
		descriptionHtml: 'Does a thing.',
		examples: [{ beforeLabel: '', before: snippet, afters: [{ label: '', snippet }], unchanged: false }],
		parts: [],
		options: [],
		wikiUrl: `https://example.test/${id}`,
		...overrides,
	};
}

/** An option whose default is the same in both tools, unless the overrides give `markupDefaultValue` of their own. */
export function option(key: string, overrides: Partial<Option<Snippet>> = {}): Option<Snippet> {
	const defaultValue = overrides.defaultValue === undefined ? '100' : overrides.defaultValue;

	return {
		key,
		descriptionHtml: 'Controls a thing.',
		defaultText: '`100`',
		defaultValue,
		markupDefaultValue: defaultValue,
		readByHtml: 'Read by rules.',
		audience: 'analyzer',
		kind: 'number',
		perFile: false,
		choices: [],
		min: 0,
		max: 1000,
		words: [],
		values: [],
		examples: [],
		parts: [],
		rules: [],
		wikiUrl: `https://example.test/${key}`,
		...overrides,
	};
}

export function catalog(rules: Rule<Snippet>[], options: Option<Snippet>[]): Catalog<Snippet> {
	return {
		rules,
		options,
		groups: ['Group A', 'Group B'],
		markupExtensions: ['xml', 'csproj'],
		markupProfiles: ['xml', 'msbuild'],
		repoUrl: 'https://example.test/repo',
		wikiHomeUrl: 'https://example.test/wiki',
	};
}
