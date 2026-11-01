import type { Option, Rule, RulePair, SourceCatalog, SourceSnippet } from '../../src/state/model.ts';
import { parseBlocks } from '../render/markdown.ts';

import {
	assembleGuideCatalog,
	buildGuideOption,
	checkDefaultHalf,
	joinRules,
	listOfStrings,
	markOpposites,
	parseGuideReadme,
	parseGuideRule,
	tableOf,
	type DeclaredNumber,
	type GuideFlavor,
	type GuideRule,
} from './guides.ts';
import type { GuideLevel } from './levels.ts';
import { parseTightPairs } from './references.ts';
import { byKey, ExtractionError, jsSourcePaths, normalize, readFiles, readOne, type SourceFile } from './source.ts';

export interface JsSources {
	rules: SourceFile[];
	ruleCode: SourceFile[];
	readme: SourceFile;
	presets: SourceFile;
	ruleMap: SourceFile;
	environment: SourceFile;
}

const flavor: GuideFlavor = {
	dir: 'js',
	comment: '//',
	heading: /^# (?<id>[a-z]+(?:-[a-z]+)*)$/,
	shipsOn: () => false,
};

interface Presets {
	/** The first level whose preset holds the rule. */
	levelOf: Map<string, GuideLevel>;

	/** The rules that no preset holds, which only an entry of a project's own enables. */
	optIn: Set<string>;
}

/** Reads the object of a default: `{ switchCases: true, tags: ['a'] }` as the text of each value. */
function readObject(file: string, body: string): Record<string, string> {
	const values: Record<string, string> = {};
	const value = /(\w+): (true|false|\[[^\]]*\])/g;

	for (const match of body.matchAll(value)) {
		values[match[1]!] = match[2]!.startsWith('[') ? listOfStrings(file, match[2]!).join(', ') : match[2]!;
	}

	const rest = body.replace(value, '').replace(/[\s,{}]/g, '');

	if (rest !== '') {
		throw new ExtractionError(file, `the options ${body} hold a value this build does not read; it reads true, false and lists of strings`);
	}

	return values;
}

/** Reads the preset table: the rules each level adds, and the rules that no level holds. */
function parsePresets(file: string, text: string): Presets {
	const presets: Presets = { levelOf: new Map(), optIn: new Set() };

	let level: GuideLevel | null = null;

	for (const [index, line] of text.replace(/\r\n?/g, '\n').split('\n').entries()) {
		const opened = /^\t(minimal|moderate|strong): \{$/.exec(line)?.[1];

		if (opened !== undefined) {
			level = opened as GuideLevel;
			continue;
		}

		if (/^\t\},?$/.test(line)) {
			level = null;
			continue;
		}

		const entry = /^\t\t'([a-z-]+)': (.+),$/.exec(line);

		if (level === null || !entry) {
			continue;
		}

		const [, rule, value] = entry;

		if (value !== "'error'") {
			throw new ExtractionError(
				file,
				`line ${index + 1}: the entry ${value} of ${rule} is not 'error', the one entry the writer of the config knows`,
			);
		}

		if (!presets.levelOf.has(rule!)) {
			presets.levelOf.set(rule!, level);
		}
	}

	if (presets.levelOf.size === 0) {
		throw new ExtractionError(file, 'no rule was found in the presets');
	}

	const optIn = /^export const optInRules = \[([^\]]*)\] as const;$/m.exec(normalize(text))?.[1];

	if (optIn === undefined) {
		throw new ExtractionError(file, 'there is no "export const optInRules = [...] as const;" that lists the rules no level holds');
	}

	for (const rule of listOfStrings(file, `[${optIn}]`)) {
		if (presets.levelOf.has(rule)) {
			throw new ExtractionError(file, `${rule} is an opt-in rule, but the ${presets.levelOf.get(rule)} preset holds it`);
		}

		presets.optIn.add(rule);
	}

	return presets;
}

/** Reads the options a rule declares as defaults: the `defaultOptions` of its source, which a rule with no option leaves out. */
function parseRuleDefaults(file: string, text: string): Record<string, string> {
	const body = /^\t+defaultOptions: \[(.*)\],$/m.exec(text)?.[1];
	return body === undefined || body === '' ? {} : readObject(file, body);
}

/** Reads the lists of strings that `environment.ts` exports as the defaults of the shared settings, by setting. */
function parseSettingDefaults(file: string, text: string): Map<string, string[]> {
	const defaults = new Map<string, string[]>();

	for (const match of text.matchAll(/export const default(\w+): readonly string\[\] = \[([\s\S]*?)\];/g)) {
		defaults.set(`${match[1]![0]!.toLowerCase()}${match[1]!.slice(1)}`, listOfStrings(file, `[${match[2]}]`));
	}

	return defaults;
}

/**
 * Reads the whole numbers that `environment.ts` exports as `default<Name>`, `least<Name>` and `greatest<Name>`, by setting.
 * A default with no `least` or no `greatest` fails the build.
 */
function parseSettingNumbers(file: string, text: string): Map<string, DeclaredNumber> {
	const numbers = new Map<string, DeclaredNumber>();

	for (const [, name, value] of text.matchAll(/^export const default(\w+) = (\d+);$/gm)) {
		const read = (prefix: string): number | undefined => {
			const bound = new RegExp(`^export const ${prefix}${name} = (\\d+);$`, 'm').exec(text)?.[1];
			return bound === undefined ? undefined : Number(bound);
		};

		const min = read('least');
		const max = read('greatest');

		if (min === undefined || max === undefined) {
			throw new ExtractionError(file, `default${name} has no "export const least${name} = <number>;" and "export const greatest${name} = <number>;"`);
		}

		numbers.set(`${name![0]!.toLowerCase()}${name!.slice(1)}`, { value: Number(value), min, max });
	}

	return numbers;
}

const proseOf = (section: string | undefined): string =>
	parseBlocks(section ?? '')
		.flatMap((block) => (block.kind === 'p' ? [block.text] : []))
		.join(' ');

/** The options of one rule page: the rows of its Options table, checked against the defaults its source declares. */
function readRuleOptions(page: GuideRule, defaults: Record<string, string>, file: string): Option<SourceSnippet>[] {
	const options: Option<SourceSnippet>[] = [];
	const seen = new Set<string>();

	for (const [key, defaultCell, effect] of page.sections.has('Options') ? tableOf(page.file, page.sections.get('Options'), 'Options') : []) {
		const name = /^`([A-Za-z]+)`$/.exec(key ?? '')?.[1];

		if (name === undefined) {
			throw new ExtractionError(page.file, `the option "${key}" is not one name between backticks`);
		}

		const option = buildGuideOption({
			key: name,
			file: page.file,
			defaultCell: defaultCell ?? '',
			effect: effect ?? '',
			readBy: `Option of [\`${page.id}\`](${page.pageUrl}).`,
			pageUrl: `${page.pageUrl}#options`,
			global: false,
		});

		const declared = defaults[name];

		if (declared === undefined) {
			throw new ExtractionError(file, `the page of ${page.id} documents ${name}, but its source declares no such option`);
		}

		if ((option.defaultValue ?? '') !== declared) {
			throw new ExtractionError(
				page.file,
				`the page gives ${name} the default ${option.defaultText}, but the source declares ${declared || 'an empty list'}`,
			);
		}

		option.rules = [page.id];
		option.scope = page.id;

		seen.add(name);
		options.push(option);
	}

	for (const key of Object.keys(defaults)) {
		if (!seen.has(key)) {
			throw new ExtractionError(page.file, `the source of ${page.id} declares the option ${key}, which the page does not document`);
		}
	}

	return options;
}

/** Orders options by key, and the options of one key, which different rules may take, by the rule. */
const byKeyThenScope = (a: Option<SourceSnippet>, b: Option<SourceSnippet>): number =>
	byKey(a, b) || byKey({ key: a.scope ?? '' }, { key: b.scope ?? '' });

/**
 * The options of `settings.stanza`: the keys the Settings sections show, with the defaults and the bounds `environment.ts` exports.
 * An example that shows a list names the key as `key: [`, and one that shows a whole number as `key: 3`.
 */
function readSettingOptions(pages: GuideRule[], sources: JsSources): Option<SourceSnippet>[] {
	const { file, text } = sources.environment;
	const lists = parseSettingDefaults(file, text);
	const numbers = parseSettingNumbers(file, text);
	const options: Option<SourceSnippet>[] = [];

	for (const page of pages) {
		const fences = page.settingFences;

		const shown = new Map(
			fences.flatMap((fence) => [...fence.code.matchAll(/\b(\w+): (\[|\d+\b)/g)].map((match) => [match[1]!, match[2]!] as const)),
		);

		for (const [key, example] of shown) {
			const items = lists.get(key);
			const number = numbers.get(key);

			if (example === '[' ? items === undefined : number === undefined) {
				throw new ExtractionError(
					page.file,
					`the setting ${key} has no default exported as default${key[0]!.toUpperCase()}${key.slice(1)} by environment.ts`,
				);
			}

			const effect = proseOf(page.sections.get('Settings'));

			if (effect === '') {
				throw new ExtractionError(page.file, 'the Settings section has no sentence under its example');
			}

			if (number !== undefined && Number(example) !== number.value) {
				throw new ExtractionError(page.file, `the example of ${key} shows ${example}, but environment.ts gives it the default ${number.value}`);
			}

			if (number !== undefined && !effect.includes(`from ${number.min} to ${number.max}`)) {
				throw new ExtractionError(page.file, `the Settings section does not say that ${key} is a whole number from ${number.min} to ${number.max}`);
			}

			options.push(
				buildGuideOption({
					key,
					file: page.file,
					defaultCell: `\`${number === undefined ? JSON.stringify(items) : number.value}\``,
					effect,
					readBy: 'Set in `settings.stanza`, which the rules share.',
					pageUrl: `${page.pageUrl}#settings`,
					global: true,
					examples: fences,
					...(number === undefined ? {} : { bounds: number }),
				}),
			);
		}
	}

	return options;
}

/** The rules a rule source asks about through `context.isEnabled`, which is how the separating half of a pair yields to its tight half. */
function askedAbout(text: string): string[] {
	return [...new Set([...text.matchAll(/\bcontext\.isEnabled\('([a-z]+(?:-[a-z]+)*)'\)/g)].map((match) => match[1]!))];
}

/** Fails when the pair table of the rule map and the rule sources do not describe the same pairs, or a pair does not split the levels as the table says. */
function checkPairs(file: string, pairs: RulePair[], rules: Rule<SourceSnippet>[], code: Map<string, SourceFile>): void {
	const levelOf = new Map(rules.map((rule) => [rule.id, rule.level]));

	for (const pair of pairs) {
		for (const id of [pair.first, pair.second]) {
			if (!levelOf.has(id)) {
				throw new ExtractionError(file, `the pair table lists ${id}, which has no page under ${jsSourcePaths.rules}`);
			}
		}

		checkDefaultHalf(file, pair, (id) => levelOf.get(id) !== 'opt-in');

		const asked = askedAbout(code.get(pair.first)!.text);

		if (asked.length !== 1 || asked[0] !== pair.second) {
			throw new ExtractionError(
				file,
				`the pair table pairs ${pair.first} with ${pair.second}, but its source asks context.isEnabled about ${asked.join(' and ') || 'no rule'}`,
			);
		}
	}

	for (const [id, source] of code) {
		const asked = askedAbout(source.text);

		if (asked.length > 0 && !pairs.some((pair) => pair.first === id)) {
			throw new ExtractionError(
				file,
				`${id} asks context.isEnabled about ${asked.join(' and ')} in its source, but the pair table has no row that separates with it`,
			);
		}
	}
}

/** Reads the rule pages, the readme and the code of JS Stanza into the catalog the page shows. */
export function extractJavascript(sources: JsSources): SourceCatalog {
	const pages = sources.rules.map((source) => parseGuideRule(source.file, source.text, flavor));
	const rules = joinRules(sources.readme.file, parseGuideReadme(sources.readme.file, sources.readme.text), pages, flavor);
	const presets = parsePresets(sources.presets.file, sources.presets.text);
	const code = new Map(sources.ruleCode.map((source) => [(source.file.split('/').pop() ?? '').replace(/\.ts$/, ''), source]));
	const options: Option<SourceSnippet>[] = [];

	for (const rule of rules) {
		const level = presets.levelOf.get(rule.id) ?? (presets.optIn.has(rule.id) ? 'opt-in' : undefined);

		if (level !== rule.level) {
			throw new ExtractionError(sources.presets.file, `${rule.id} is ${level ?? 'in no preset'} there, but its page says ${rule.level}`);
		}

		const source = code.get(rule.id);

		if (!source) {
			throw new ExtractionError(sources.readme.file, `${rule.id} has a page but no source under ${jsSourcePaths.ruleCode}`);
		}

		options.push(
			...readRuleOptions(
				pages.find((page) => page.id === rule.id)!,
				parseRuleDefaults(source.file, source.text),
				source.file,
			),
		);
	}

	for (const rule of [...presets.levelOf.keys(), ...presets.optIn]) {
		if (!rules.some((candidate) => candidate.id === rule)) {
			throw new ExtractionError(sources.presets.file, `${rule} is in a preset but has no page`);
		}
	}

	options.push(...readSettingOptions(pages, sources));

	const pairs = parseTightPairs(sources.ruleMap.file, sources.ruleMap.text);

	checkPairs(sources.ruleMap.file, pairs, rules, code);
	markOpposites(rules, pairs);

	return { ...assembleGuideCatalog(rules, options.sort(byKeyThenScope), pages), pairs };
}

/** Reads the files of JS Stanza under a repository root. */
export function readJavascript(root: string): JsSources {
	return {
		rules: readFiles(root, jsSourcePaths.rules, /^[a-z-]+\.md$/, 'rule'),
		ruleCode: readFiles(root, jsSourcePaths.ruleCode, /^[a-z-]+\.ts$/, 'rule source'),
		readme: readOne(root, jsSourcePaths.readme),
		presets: readOne(root, jsSourcePaths.presets),
		ruleMap: readOne(root, jsSourcePaths.ruleMap),
		environment: readOne(root, jsSourcePaths.environment),
	};
}
