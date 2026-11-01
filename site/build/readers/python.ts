import type { RulePair, SourceCatalog } from '../../src/state/model.ts';

import { assembleGuideCatalog, checkDefaultHalf, joinRules, markOpposites, parseGuideReadme, parseGuideRule, type GuideFlavor } from './guides.ts';
import { parseKnownKeys, readConfigurationOptions } from './python-config.ts';
import { parsePairs } from './references.ts';
import { ExtractionError, normalize, pythonSourcePaths, readFiles, readOne, type SourceFile } from './source.ts';

export interface PythonSources {
	rules: SourceFile[];
	ruleCode: SourceFile[];
	readme: SourceFile;
	configuration: SourceFile;
	options: SourceFile;
	config: SourceFile;
	formatter: SourceFile;
	ruleMap: SourceFile;
}

const flavor: GuideFlavor = {
	dir: 'py',
	comment: '#',
	heading: /^# (?<id>ZA3\d{3}) - (?<title>[a-z]+(?:-[a-z]+)*)$/,

	// The tool applies the moderate level when a project sets none, and an opt-in rule is in no level.
	shipsOn: (level) => level !== 'strong' && level !== 'opt-in',
};

interface Registered {
	code: string;
	name: string;
	level: string;

	/** The code of the rule that does the opposite at the same gaps, which the module names for the rules of a pair. */
	opposite: string | null;

	/** Whether the rule removes the blank line of a gap, as a tight half of a pair does. */
	keeps: boolean;
}

/** Reads the code, the name, the level and the opposite a rule module registers. */
function parseRegistered(text: string): Registered | null {
	const source = /^RULE = Rule\(([\s\S]*?)^\)$/m.exec(normalize(text))?.[1];
	const match = /^\s*code="(ZA3\d{3})",\s*name="([a-z-]+)",\s*level="([a-z-]+)",/.exec(source ?? '');

	if (source === undefined || !match) {
		return null;
	}

	return {
		code: match[1]!,
		name: match[2]!,
		level: match[3]!,
		opposite: /^\s*opposite="(ZA3\d{3})",?$/m.exec(source)?.[1] ?? null,
		keeps: /^\s*role="keep",?$/m.test(source),
	};
}

/** Fails when the pair table of the rule map and the `opposite` each rule module names do not describe the same pairs. */
function checkPairs(file: string, pairs: RulePair[], registered: Registered[]): void {
	const byCode = new Map(registered.map((entry) => [entry.code, entry]));
	const inTable = new Set(pairs.flatMap((pair) => [pair.first, pair.second]));

	for (const pair of pairs) {
		const halves = [pair.first, pair.second].map((code) => byCode.get(code));

		for (const [at, half] of halves.entries()) {
			const code = at === 0 ? pair.first : pair.second;
			const other = at === 0 ? pair.second : pair.first;

			if (half === undefined) {
				throw new ExtractionError(file, `the pair table lists ${code}, which no module under ${pythonSourcePaths.ruleCode} registers`);
			}

			if (half.opposite !== other) {
				throw new ExtractionError(file, `the pair table pairs ${code} with ${other}, but its module names ${half.opposite ?? 'no opposite'}`);
			}
		}

		const [first, second] = halves as [Registered, Registered];
		checkDefaultHalf(file, pair, (code) => byCode.get(code)!.level !== 'opt-in');

		const tight = [first, second].filter((half) => half.keeps).map((half) => half.code);

		if (tight.length !== 1 || tight[0] !== pair.ownerWhenBothEnabled) {
			throw new ExtractionError(
				file,
				`${pair.first} and ${pair.second} are owned by ${pair.ownerWhenBothEnabled} when both are enabled, but ${tight.join(' and ') || 'no half'} removes the blank line`,
			);
		}
	}

	for (const entry of registered) {
		if (entry.opposite !== null && !inTable.has(entry.code)) {
			throw new ExtractionError(file, `${entry.code} names the opposite ${entry.opposite} in its module, but the pair table has no row for it`);
		}
	}
}

/** Reads the rule pages, the readme and the code of Python Stanza into the catalog the page shows. */
export function extractPython(sources: PythonSources): SourceCatalog {
	const pages = sources.rules.map((source) => parseGuideRule(source.file, source.text, flavor));
	const rules = joinRules(sources.readme.file, parseGuideReadme(sources.readme.file, sources.readme.text), pages, flavor);
	const registered = sources.ruleCode.flatMap((source) => parseRegistered(source.text) ?? []);
	const pageOf = new Map(pages.map((page) => [page.id, page]));

	for (const rule of rules) {
		const entry = registered.find((candidate) => candidate.code === rule.id);

		if (!entry) {
			throw new ExtractionError(sources.readme.file, `${rule.id} has a page but no module under ${pythonSourcePaths.ruleCode} registers it`);
		}

		if (entry.level !== rule.level) {
			throw new ExtractionError(sources.readme.file, `${rule.id} is ${entry.level} in its module, but its page says ${rule.level}`);
		}

		if (entry.name !== rule.title) {
			throw new ExtractionError(pageOf.get(rule.id)!.file, `${rule.id} is named ${entry.name} in its module, but its page says ${rule.title}`);
		}
	}

	for (const entry of registered) {
		if (!rules.some((rule) => rule.id === entry.code)) {
			throw new ExtractionError(sources.readme.file, `${entry.code} is registered but has no row in the rule table`);
		}
	}

	const known = parseKnownKeys(sources.config.file, sources.config.text);
	const pairs = parsePairs(sources.ruleMap.file, sources.ruleMap.text);

	checkPairs(sources.ruleMap.file, pairs, registered);

	// The check above holds each module's opposite equal to its half of the table.
	markOpposites(rules, pairs);

	return { ...assembleGuideCatalog(rules, readConfigurationOptions(sources, pages, known), pages), pairs };
}

/** Reads the files of Python Stanza under a repository root. */
export function readPython(root: string): PythonSources {
	return {
		rules: readFiles(root, pythonSourcePaths.rules, /^ZA3\d{3}-[a-z-]+\.md$/, 'rule'),
		ruleCode: readFiles(root, pythonSourcePaths.ruleCode, /^[a-z_]+\.py$/, 'rule module', /^__/),
		readme: readOne(root, pythonSourcePaths.readme),
		configuration: readOne(root, pythonSourcePaths.configuration),
		options: readOne(root, pythonSourcePaths.options),
		config: readOne(root, pythonSourcePaths.config),
		formatter: readOne(root, pythonSourcePaths.formatter),
		ruleMap: readOne(root, pythonSourcePaths.ruleMap),
	};
}
