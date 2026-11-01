import type { Option, SourceSnippet } from '../../src/state/model.ts';

import {
	buildGuideOption,
	listOfStrings,
	parseDefault,
	repositoryFile,
	resolveLinks,
	tableOf,
	type DeclaredNumber,
	type GuideRule,
} from './guides.ts';
import { byKey, ExtractionError, normalize, type SourceFile } from './source.ts';

/** The files that say which keys `[tool.stanza]` has, what each does, and what the code accepts and defaults it to. */
export interface ConfigurationSources {
	configuration: SourceFile;
	options: SourceFile;
	config: SourceFile;
	formatter: SourceFile;
}

/** The keys that the pickers and the rule dropdowns set, so they are not options of the page. */
const pickerKeys = new Set(['level', 'enable', 'disable']);

/** Reads the keys the `[tool.stanza]` table accepts, from the set `config.py` checks it against. */
export function parseKnownKeys(file: string, text: string): Set<string> {
	const body = /^_KEYS = \{([^}]*)\}/m.exec(normalize(text))?.[1];

	if (body === undefined) {
		throw new ExtractionError(file, 'there is no set "_KEYS = {...}" of the accepted keys');
	}

	return new Set(listOfStrings(file, `[${body}]`));
}

/** Reads the defaults `options.py` declares as tuples of strings, by the key they belong to. */
function parseCodeDefaults(file: string, text: string): Map<string, string[]> {
	const defaults = new Map<string, string[]>();

	for (const match of normalize(text).matchAll(/^DEFAULT_(\w+) = \(([^)]*)\)/gm)) {
		defaults.set(match[1]!.toLowerCase().replaceAll('_', '-'), listOfStrings(file, `[${match[2]}]`));
	}

	return defaults;
}

/** The closed set of words the code declares for a key, the file that declares it, and the default of a key that takes one word. */
interface DeclaredWords {
	file: string;
	values: string[];
	defaultValue: string | null;
}

const keyOf = (name: string): string => name.toLowerCase().replaceAll('_', '-');

/** Reads the `Name = Literal[...]` types of a module by name, which hold the words a key accepts. */
function parseLiterals(file: string, source: string): Map<string, string[]> {
	const literals = new Map<string, string[]>();

	for (const [, name, body] of source.matchAll(/^(\w+) = Literal\[([^\]]*)\]$/gm)) {
		literals.set(name!, listOfStrings(file, `[${body}]`));
	}

	return literals;
}

/** Reads each `DEFAULT_<KEY>: Type = "word"` of a module with the `Literal` that `Type` names, because the code owns what a key accepts. */
function parseWordKeys(file: string, text: string): Map<string, DeclaredWords> {
	const source = normalize(text);
	const literals = parseLiterals(file, source);
	const words = new Map<string, DeclaredWords>();

	for (const [, name, type, defaultValue] of source.matchAll(/^DEFAULT_(\w+): (\w+) = "([a-z-]+)"$/gm)) {
		const values = literals.get(type!);

		if (values === undefined) {
			throw new ExtractionError(file, `there is no \`${type} = Literal[...]\` for DEFAULT_${name}`);
		}

		if (!values.includes(defaultValue!)) {
			throw new ExtractionError(file, `DEFAULT_${name} "${defaultValue}" is none of the values ${values.join(', ')}`);
		}

		words.set(keyOf(name!), { file, values, defaultValue: defaultValue! });
	}

	return words;
}

/** Reads each `<KEYS>: tuple[Type, ...] = get_args(Type)` of a module with the `Literal` that `Type` names, which closes the items of a list key. */
function parseListWords(file: string, text: string): Map<string, DeclaredWords> {
	const source = normalize(text);
	const literals = parseLiterals(file, source);
	const words = new Map<string, DeclaredWords>();

	for (const [, name, type] of source.matchAll(/^([A-Z_]+): tuple\[(\w+), \.\.\.\] = get_args\(\2\)$/gm)) {
		const values = literals.get(type!);

		if (values === undefined) {
			throw new ExtractionError(file, `there is no \`${type} = Literal[...]\` for ${name}`);
		}

		words.set(keyOf(name!), { file, values, defaultValue: null });
	}

	return words;
}

/** The lowercase words between backticks after the last colon of an effect, which is where the Keys table lists what a key accepts. */
const wordsNamedBy = (effect: string): string[] => [...effect.slice(effect.lastIndexOf(':') + 1).matchAll(/`([a-z]+)`/g)].map((match) => match[1]!);

/** Fails when the Keys table says a whole number takes other bounds than the code checks. */
function checkBounds(file: string, option: Option<SourceSnippet>, effect: string): void {
	const stated = /\bfrom (\d+) to (\d+)\b/.exec(effect);

	if (stated !== null && (Number(stated[1]) !== option.min || Number(stated[2]) !== option.max)) {
		throw new ExtractionError(
			file,
			`the effect of ${option.key} says from ${stated[1]} to ${stated[2]}, but the code checks it from ${option.min} to ${option.max}`,
		);
	}
}

/** Fails when the Keys table gives a key another default or another set of words than the code declares. */
function checkWords(file: string, option: Option<SourceSnippet>, effect: string, declared: DeclaredWords): void {
	if (option.kind === 'enum' && option.defaultValue !== declared.defaultValue) {
		throw new ExtractionError(declared.file, `the default of ${option.key} differs from the one ${file} gives`);
	}

	const unknown = (option.kind === 'list' ? (option.defaultValue?.split(', ') ?? []) : []).find((item) => !declared.values.includes(item));

	if (unknown !== undefined) {
		throw new ExtractionError(file, `the default of ${option.key} names ${unknown}, which ${declared.file} does not accept`);
	}

	const named = wordsNamedBy(effect);

	if (named.toSorted().join() !== declared.values.toSorted().join()) {
		throw new ExtractionError(
			file,
			`the effect of ${option.key} names ${named.join(', ')}, but ${declared.file} accepts ${declared.values.join(', ')}`,
		);
	}
}

/** Reads the least value that `_read_bounded` of `config.py` accepts, from its `not <least> <= value <= limit` check. */
function parseLeastBound(config: SourceFile, text: string): number {
	const body = /^def _read_bounded\(.*?(?=^(?:def|class) |(?![\s\S]))/ms.exec(text)?.[0];

	if (body === undefined) {
		throw new ExtractionError(config.file, 'there is no function "_read_bounded" that checks the whole numbers');
	}

	const least = /\bnot (\d+) <= value <= limit\b/.exec(body)?.[1];

	if (least === undefined) {
		throw new ExtractionError(config.file, '_read_bounded checks no "not <least> <= value <= limit"');
	}

	return Number(least);
}

/**
 * Reads the whole numbers `config.py` reads with `_read_bounded(table, "<key>", DEFAULT_<NAME>, <NAME>_LIMIT)`, by key.
 * The default and the greatest value are the constants `options.py` declares, and the least is the bound `_read_bounded` checks.
 */
function parseNumbers(options: SourceFile, config: SourceFile): Map<string, DeclaredNumber> {
	const optionsText = normalize(options.text);
	const configText = normalize(config.text);
	const min = parseLeastBound(config, configText);
	const numbers = new Map<string, DeclaredNumber>();
	const calls = configText.matchAll(/\b_read_bounded\(\s*table,\s*"([a-z-]+)",\s*DEFAULT_(\w+),\s*(\w+)_LIMIT,?\s*\)/g);

	for (const [, key, name, limitName] of calls) {
		if (limitName !== name || keyOf(name!) !== key) {
			throw new ExtractionError(
				config.file,
				`the call that reads ${key} names DEFAULT_${name} and ${limitName}_LIMIT, which do not belong to the key`,
			);
		}

		const value = new RegExp(`^DEFAULT_${name} = (\\d+)$`, 'm').exec(optionsText)?.[1];
		const limit = new RegExp(`^${name}_LIMIT = (\\d+)$`, 'm').exec(optionsText)?.[1];

		if (value === undefined || limit === undefined) {
			throw new ExtractionError(
				options.file,
				`${config.file} reads ${key} with DEFAULT_${name} and ${name}_LIMIT, which ${options.file} does not both declare`,
			);
		}

		numbers.set(key!, { value: Number(value), min, max: Number(limit) });
	}

	if (numbers.size === 0) {
		throw new ExtractionError(
			config.file,
			'there is no call \'_read_bounded(table, "<key>", DEFAULT_<NAME>, <NAME>_LIMIT)\' that reads a whole number',
		);
	}

	return numbers;
}

const backticked = (cell: string | undefined): string | undefined => /^`([^`]+)`$/.exec(cell ?? '')?.[1];

/** The rows of the Options table of each rule page, by key; the page that lists a key gives its default when the configuration page defers. */
function pageDefaults(pages: GuideRule[]): Map<string, string> {
	const defaults = new Map<string, string>();

	for (const page of pages) {
		if (!page.sections.has('Options')) {
			continue;
		}

		for (const [key, defaultCell] of tableOf(page.file, page.sections.get('Options'), 'Options')) {
			const name = backticked(key);

			if (name === undefined) {
				throw new ExtractionError(page.file, `the option "${key}" is not one key between backticks`);
			}

			defaults.set(name, defaultCell ?? '');
		}
	}

	return defaults;
}

/** The options of the page: the rows of the Keys table of the configuration page that the pickers do not set. */
export function readConfigurationOptions(sources: ConfigurationSources, pages: GuideRule[], known: Set<string>): Option<SourceSnippet>[] {
	const { file, text } = sources.configuration;
	const keys = /^## Keys\n([\s\S]*?)(?=^## )/m.exec(resolveLinks(file, normalize(text)))?.[1];
	const fromPages = pageDefaults(pages);
	const fromCode = parseCodeDefaults(sources.options.file, sources.options.text);
	const numbers = parseNumbers(sources.options, sources.config);

	const words = new Map([
		...parseWordKeys(sources.options.file, sources.options.text),
		...parseWordKeys(sources.formatter.file, sources.formatter.text),
	]);

	const listWords = parseListWords(sources.options.file, sources.options.text);
	const options: Option<SourceSnippet>[] = [];
	const listed = new Set<string>();

	for (const [keyCell, defaultCell, effect] of tableOf(file, keys, 'Keys')) {
		const key = backticked(keyCell);

		if (key === undefined) {
			throw new ExtractionError(file, `the key "${keyCell}" is not one name between backticks`);
		}

		listed.add(key);

		if (pickerKeys.has(key)) {
			continue;
		}

		// A default too long for the table is given on the page of the rule that reads the key.
		const cell = (defaultCell ?? '').startsWith('see ') ? fromPages.get(key) : defaultCell;

		if (cell === undefined) {
			throw new ExtractionError(file, `the default of ${key} defers to a rule page, but none lists the key`);
		}

		const declared = words.get(key) ?? listWords.get(key);

		const option = buildGuideOption({
			key,
			file,
			defaultCell: cell,
			effect: effect ?? '',
			readBy: 'Set in `[tool.stanza]` of a `pyproject.toml`.',
			pageUrl: `${repositoryFile(file)}#keys`,
			global: true,
			...(numbers.has(key) ? { bounds: numbers.get(key)! } : {}),
			...(declared === undefined ? {} : { choices: declared.values }),
		});

		if (declared !== undefined) {
			checkWords(file, option, effect ?? '', declared);
		}

		if (option.kind === 'number') {
			checkBounds(file, option, effect ?? '');
		}

		const inCode = numbers.get(key)?.value.toString() ?? fromCode.get(key)?.join(', ');

		if (inCode !== undefined && (option.defaultValue ?? '') !== inCode) {
			throw new ExtractionError(sources.options.file, `the default of ${key} differs from the one the configuration page gives`);
		}

		options.push(option);
	}

	checkKeySets(file, sources.config.file, listed, known);
	checkPageDefaults(file, options, fromPages);

	return options.sort(byKey);
}

/** Fails when the Keys table and the set of keys `config.py` accepts name other keys. */
function checkKeySets(file: string, configFile: string, listed: Set<string>, known: Set<string>): void {
	for (const key of known) {
		if (!listed.has(key)) {
			throw new ExtractionError(file, `${configFile} accepts the key ${key}, which the Keys table does not list`);
		}
	}

	for (const key of listed) {
		if (!known.has(key)) {
			throw new ExtractionError(file, `the Keys table lists ${key}, which ${configFile} does not accept`);
		}
	}
}

/** Fails when a rule page documents a key the Keys table lacks, or gives a key another default than the table. */
function checkPageDefaults(file: string, options: Option<SourceSnippet>[], fromPages: Map<string, string>): void {
	for (const [key, cell] of fromPages) {
		const documented = options.find((option) => option.key === key);

		if (!documented) {
			throw new ExtractionError(file, `a rule page documents the key ${key}, which the Keys table does not list`);
		}

		if (documented.defaultValue !== (parseDefault(file, cell).defaultValue ?? null)) {
			throw new ExtractionError(file, `a rule page gives ${key} another default than the Keys table`);
		}
	}
}
