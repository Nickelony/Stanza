import { readdirSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

import { expect } from 'vitest';

import { extractCatalog, extractFromSources } from '../build/readers/extract.ts';
import { extractJavascript, readJavascript, type JsSources } from '../build/readers/javascript.ts';
import { splitMarkup } from '../build/readers/markup.ts';
import { extractPython, readPython, type PythonSources } from '../build/readers/python.ts';
import { ExtractionError, sourcePaths, type ExtractionSources, type SourceFile } from '../build/readers/source.ts';
import { emptyConfig, type Config } from '../src/state/config.ts';
import type { Catalog, CSharpOption, CSharpRule, Option, Rule, RulePair, Snippet, SourceCatalog } from '../src/state/model.ts';

export const repositoryRoot = resolve(import.meta.dirname, '..', '..');

/** Everything the pages describe, before the Markup page takes its rules and keys; the extraction tests read this one. */
export const realCombined: SourceCatalog = extractCatalog(repositoryRoot);

const split = splitMarkup(realCombined);

/** The catalog of the C# page. */
export const realCatalog: SourceCatalog = split.csharp;

/** The catalog of the Markup page. */
export const realMarkup: SourceCatalog = split.markup;
export const realJavascript: SourceCatalog = extractJavascript(readJavascript(repositoryRoot));
export const realPython: SourceCatalog = extractPython(readPython(repositoryRoot));

/** Changes one file of the real JS sources and expects the extraction to fail with a message that names the file and matches. */
export function failsOnJavascript(
	file: keyof JsSources | `rules:${string}` | `ruleCode:${string}`,
	change: (text: string) => string,
	message: RegExp,
): void {
	const sources = readJavascript(repositoryRoot);

	swapFile(sources, file, change);
	expectExtractionError(() => extractJavascript(sources), message);
}

/** Changes one file of the real Python sources and expects the extraction to fail with a message that matches. */
export function failsOnPython(
	file: keyof PythonSources | `rules:${string}` | `ruleCode:${string}`,
	change: (text: string) => string,
	message: RegExp,
): void {
	const sources = readPython(repositoryRoot);

	swapFile(sources, file, change);
	expectExtractionError(() => extractPython(sources), message);
}

/** Replaces the text of the named source in place: a single file by its key, or one of a list as `key:file name`. */
function swapFile(sources: JsSources | PythonSources, file: string, change: (text: string) => string): void {
	const [key, name] = file.split(':') as [keyof (JsSources & PythonSources), string | undefined];
	const entry = (sources as unknown as Record<string, SourceFile | SourceFile[]>)[key]!;
	const target = Array.isArray(entry) ? entry.find((candidate) => candidate.file.endsWith(`/${name}`)) : entry;

	expect(target, `${file} is a source`).toBeDefined();
	target!.text = change(target!.text);
}

function expectExtractionError(run: () => unknown, message: RegExp): void {
	let error: unknown;

	try {
		run();
	} catch (caught) {
		error = caught;
	}

	expect(error, 'the extraction should fail').toBeInstanceOf(ExtractionError);
	expect((error as ExtractionError).message).toMatch(message);
}

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
		gapOwners: one(sourcePaths.gapOwners),
		formatters: one(sourcePaths.formatters),
		levels: one(sourcePaths.levels),
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
		gapOwners: swap(sources.gapOwners),
		formatters: swap(sources.formatters),
		levels: swap(sources.levels),
	};
}

/** Changes one file of the real pages and expects the extraction to fail with a message that names the file, or `reportedFile`, and matches. */
export function failsOn(file: string, change: (text: string) => string, message: RegExp, reportedFile = file): void {
	let error: unknown;

	try {
		extractFromSources(withText(realSources(), file, change));
	} catch (caught) {
		error = caught;
	}

	expect(error, `${file} should fail the extraction`).toBeInstanceOf(ExtractionError);
	expect((error as ExtractionError).message).toContain(reportedFile);
	expect((error as ExtractionError).message).toMatch(message);
}

/** The default configuration with the changes applied. */
export const config = (changes: Partial<Config>): Config => ({ ...emptyConfig(), ...changes });

/** The default configuration of the Markup page, which has no levels and so holds the none level, with the changes applied. */
export const markupConfig = (changes: Partial<Config>): Config => ({ ...emptyConfig('suggestion', 'none'), ...changes });

const snippet: Snippet = { lines: [{ html: 'x' }] };

/** The overrides of a fixture rule: the fields of a rule, and the C# facts flat, which `rule` nests under `csharp`. */
export type RuleOverrides = Partial<Omit<Rule<Snippet>, 'csharp'>> & Partial<CSharpRule>;

/** The overrides of a fixture option: the fields of an option, and the C# facts flat, which `option` nests under `csharp`. */
export type OptionOverrides = Partial<Omit<Option<Snippet>, 'csharp'>> & Partial<CSharpOption>;

/** A rule of the C# or Markup page, which holds the C# facts. */
export function rule(id: string, overrides: RuleOverrides = {}): Rule<Snippet> {
	const { csharpier, markupOnByDefault, readByMarkup, undoneByCSharpier, ...own } = overrides;
	const onByDefault = own.onByDefault ?? true;

	return {
		id,
		title: `Title of ${id}`,
		group: 'Group A',
		summary: 'Adds a blank line',
		tier: 'suggestion',
		onByDefault,
		level: onByDefault ? 'moderate' : 'strong',

		csharp: {
			csharpier: csharpier ?? null,
			markupOnByDefault: markupOnByDefault ?? false,
			readByMarkup: readByMarkup ?? false,
			undoneByCSharpier: undoneByCSharpier ?? false,
		},

		tierText: 'Suggestion, on by default',
		descriptionHtml: 'Does a thing.',
		examples: [{ beforeLabel: '', before: snippet, afters: [{ label: '', snippet }], unchanged: false }],
		parts: [],
		options: [],
		pageUrl: `https://example.test/${id}`,
		...own,
	};
}

/**
 * A key of the C# or Markup page, whose default is the same in both tools, unless the overrides give `markupDefaultValue` of their own.
 * Only a number has bounds.
 */
export function option(key: string, overrides: OptionOverrides = {}): Option<Snippet> {
	const { markupDefaultValue, markupDefaultByProfile, audience, perFile, words, values, ...own } = overrides;
	const defaultValue = own.defaultValue === undefined ? '100' : own.defaultValue;
	const isNumber = (own.kind ?? 'number') === 'number';

	return {
		key,
		descriptionHtml: 'Controls a thing.',
		defaultText: '`100`',
		defaultValue,
		readByHtml: 'Read by rules.',
		kind: 'number',
		choices: [],
		min: isNumber ? 0 : null,
		max: isNumber ? 1000 : null,
		examples: [],
		parts: [],
		rules: [],
		pageUrl: `https://example.test/${key}`,

		csharp: {
			markupDefaultValue: markupDefaultValue === undefined ? defaultValue : markupDefaultValue,
			markupDefaultByProfile: markupDefaultByProfile ?? false,
			audience: audience ?? 'analyzer',
			perFile: perFile ?? false,
			words: words ?? [],
			values: values ?? [],
		},

		...own,
	};
}

export function catalog(rules: Rule<Snippet>[], options: Option<Snippet>[], pairs: RulePair[] = []): Catalog<Snippet> {
	return {
		rules,
		options,
		pairs,
		groups: ['Group A', 'Group B'],
		markupExtensions: ['xml', 'csproj'],
		markupProfiles: ['xml', 'msbuild'],
	};
}

/** Two rules and four options, which the tests of the form, the address and the selection read. */
export const sample = catalog(
	[rule('ZA0002'), rule('ZA0014', { tier: 'suggestion', onByDefault: false })],

	[
		option('stanza_max_line_length'),
		option('stanza_multi_line_member_part', { kind: 'enum', choices: ['body', 'either'], defaultValue: 'either' }),
		option('stanza_doc_indent_size', { min: 0, max: 64, words: ['tab'], defaultValue: '0' }),
		option('stanza_assertion_roots', { kind: 'list', defaultValue: null }),
	],
);
