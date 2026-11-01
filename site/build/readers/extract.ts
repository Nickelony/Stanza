import type { Audience, Option, Rule, RulePair, SourceCatalog, SourceSnippet } from '../../src/state/model.ts';

import { parseLevels, type LevelRow } from './levels.ts';
import { isMarkupRule, markupRuleIds } from './markup.ts';
import { parseOptionPage, type ParsedOption } from './option-page.ts';
import { literalDefault, parseReadme, type Readme } from './readme.ts';
import { csharpierAnchor, parseMarkupProfiles, parseMarkupRuleIds, parsePairs, parseUndoneByCSharpier } from './references.ts';
import { parseRulePage } from './rule-page.ts';
import { byKey, ExtractionError, inFile, pageName, readFiles, readOne, sourcePaths, type ExtractionSources } from './source.ts';

type ParsedRule = ReturnType<typeof parseRulePage>;

/** The rule ID a rule page file starts with, such as `ZA0001` for `ZA0001-Some-title.md`. */
const idOfPage = (file: string): string => pageName(file).slice(0, 'ZA0000'.length);

/** Parses each rule page against its readme row, and fails on a page with no row and on a row with no page. */
function parseRules(sources: ExtractionSources, readme: Readme): Map<string, ParsedRule> {
	const parsedRules = new Map<string, ParsedRule>();

	for (const source of sources.rules) {
		try {
			const id = /^(ZA\d{4})-/.exec(pageName(source.file))?.[1] ?? '';

			const parsed = parseRulePage(
				source.file,
				source.text,
				readme.rules.find((row) => row.id === id),
			);

			if (parsedRules.has(parsed.id)) {
				throw new ExtractionError(source.file, `${parsed.id} has two pages`);
			}

			parsedRules.set(parsed.id, parsed);
		} catch (error) {
			throw inFile(source.file, error);
		}
	}

	for (const row of readme.rules) {
		if (!parsedRules.has(row.id)) {
			throw new ExtractionError(sources.readme.file, `${row.id} has a row in the rule table but no page under docs/rules`);
		}
	}

	return parsedRules;
}

/**
 * Checks a markup rule, which no level holds: its readme row says `No level`, the level table has no row for it, and it ships off.
 * Only a markup rule may say `No level`, so any other rule needs a level.
 */
function checkMarkupRow(sources: ExtractionSources, row: Readme['rules'][number], entry: LevelRow | undefined): void {
	if (isMarkupRule(row.id)) {
		if (row.level !== 'no-level') {
			throw new ExtractionError(sources.readme.file, `${row.id} is a markup rule, which no level holds, but the Level column gives it ${row.level}`);
		}

		if (entry) {
			throw new ExtractionError(sources.levels.file, `${row.id} is a markup rule, which no level holds, but the level table gives it ${entry.level}`);
		}

		if (row.onByDefault) {
			throw new ExtractionError(
				sources.readme.file,
				`${row.id} is a markup rule, which no level holds, so it ships off, but its row says on by default`,
			);
		}
	} else if (row.level === 'no-level') {
		throw new ExtractionError(
			sources.readme.file,
			`the Level column gives ${row.id} No level, which only a markup rule (${markupRuleIds[0]} to ${markupRuleIds.at(-1)}) has`,
		);
	}
}

/** Checks the readme rows and the rule pages against the level table, which is the source of the levels and the CSharpier marks. */
function checkLevels(sources: ExtractionSources, readme: Readme, levels: Map<string, LevelRow>, parsedRules: Map<string, ParsedRule>): void {
	for (const id of markupRuleIds) {
		if (!readme.rules.some((row) => row.id === id)) {
			throw new ExtractionError(sources.readme.file, `${id} is a markup rule but has no row in the rule table`);
		}
	}

	for (const row of readme.rules) {
		const entry = levels.get(row.id);

		checkMarkupRow(sources, row, entry);

		if (isMarkupRule(row.id)) {
			continue;
		}

		if (!entry) {
			throw new ExtractionError(sources.levels.file, `${row.id} has a row in the rule table of the readme but none in the level table`);
		}

		if (entry.level !== row.level) {
			throw new ExtractionError(sources.readme.file, `the Level column gives ${row.id} ${row.level}, but the level table gives it ${entry.level}`);
		}

		const ships = entry.level === 'minimal' || entry.level === 'moderate';

		if (ships !== row.onByDefault) {
			throw new ExtractionError(
				sources.levels.file,
				`${row.id} is ${entry.level} there, so it ships ${ships ? 'on' : 'off'}, but its readme row says ${row.onByDefault ? 'on' : 'off'} by default`,
			);
		}
	}

	for (const id of levels.keys()) {
		if (isMarkupRule(id)) {
			throw new ExtractionError(sources.levels.file, `${id} is a markup rule, which no level holds, but the level table has a row for it`);
		}

		if (!parsedRules.has(id)) {
			throw new ExtractionError(sources.levels.file, `${id} has a row in the level table but no page under docs/rules`);
		}
	}
}

/** Parses each option page, and fails on a readme row with no page. */
function parseOptions(sources: ExtractionSources, readme: Readme, profiles: readonly string[]): ParsedOption[] {
	const parsedOptions: ParsedOption[] = [];

	for (const source of sources.options) {
		try {
			parsedOptions.push(parseOptionPage(source.file, source.text, profiles));
		} catch (error) {
			throw inFile(source.file, error);
		}
	}

	for (const row of readme.options) {
		if (!parsedOptions.some((option) => option.key === row.key)) {
			throw new ExtractionError(sources.readme.file, `${row.key} has a row in the option table but no page under docs/options`);
		}
	}

	return parsedOptions;
}

/** Fails on a rule that the pair table or the section on layout that CSharpier undoes names and that has no page under docs/rules. */
function checkNamedRules(sources: ExtractionSources, parsedRules: Map<string, ParsedRule>, pairs: RulePair[], listed: string[]): void {
	for (const id of pairs.flatMap((pair) => [pair.first, pair.second])) {
		if (!parsedRules.has(id)) {
			throw new ExtractionError(sources.gapOwners.file, `the pair table names ${id}, which has no page under docs/rules`);
		}
	}

	for (const id of listed) {
		if (!parsedRules.has(id)) {
			throw new ExtractionError(
				sources.formatters.file,
				`the section on layout that CSharpier undoes links ${id}, which has no page under docs/rules`,
			);
		}
	}
}

/** Fails on a key a rule page names and an ID a "Read by" line names that have no page, because the link between them would drop without a word. */
function checkPageReferences(parsedRules: Map<string, ParsedRule>, parsedOptions: ParsedOption[]): void {
	const optionKeys = new Set(parsedOptions.map((option) => option.key));

	for (const rule of parsedRules.values()) {
		const missing = rule.optionKeys.find((key) => !optionKeys.has(key));

		if (missing !== undefined) {
			throw new ExtractionError(rule.file, `the page names ${missing}, which has no page under docs/options`);
		}
	}

	for (const option of parsedOptions) {
		const missing = option.readByRules.find((id) => !parsedRules.has(id));

		if (missing !== undefined) {
			throw new ExtractionError(option.file, `the "Read by" line names ${missing}, which has no page under docs/rules`);
		}
	}
}

/** Joins the parsed pages to the readme rows, the level table and the facts of the other pages into the rules of the catalog. */
function buildRules(
	readme: Readme,
	parsedRules: Map<string, ParsedRule>,
	levels: Map<string, LevelRow>,
	markupIds: ReadonlySet<string>,
	undone: ReadonlySet<string>,
): Rule<SourceSnippet>[] {
	return readme.rules.map((row) => {
		const { optionKeys: named, markupOnByDefault, file: _file, ...rule } = parsedRules.get(row.id)!;

		return {
			...rule,
			level: row.level,

			csharp: {
				csharpier: levels.get(row.id)?.csharpier ?? null,
				markupOnByDefault,
				readByMarkup: markupIds.has(row.id),
				undoneByCSharpier: undone.has(row.id),
			},

			options: named.toSorted(),
		};
	});
}

/** Joins the parsed pages to the readme rows and the rules into the options of the catalog, ordered by key. */
function buildOptions(readme: Readme, parsedOptions: ParsedOption[], rules: Rule<SourceSnippet>[]): Option<SourceSnippet>[] {
	return parsedOptions
		.map((parsed) => {
			const row = readme.options.find((candidate) => candidate.key === parsed.key);

			// A page may name the tool only to say it ignores the key, as the page of the key that picks the C# literals does.
			const markupReads = /stanza-markup/.test(parsed.readByText) && !/\bstanza-markup ignores it\b/.test(parsed.readByText);

			if (!row && !markupReads) {
				throw new ExtractionError(
					parsed.file,
					'the key is in no option table of the readme and its "Read by" text does not say stanza-markup reads it',
				);
			}

			if (row && /\bthe analyzer ignores it\b/.test(parsed.readByText)) {
				throw new ExtractionError(parsed.file, 'the key is in the option table of the readme but its "Read by" text says the analyzer ignores it');
			}

			const audience: Audience = row ? (markupReads ? 'both' : 'analyzer') : 'markup';
			const { readByRules, readByText: _text, pageDefault, toolDiffers, file: _file, csharp, ...option } = parsed;
			const related = rules.filter((rule) => readByRules.includes(rule.id) || rule.options.includes(parsed.key)).map((rule) => rule.id);
			const analyzerDefault = (row ? literalDefault(row.defaultCell) : null) ?? (row ? null : pageDefault);

			return {
				...option,

				// A key only the tool reads has no analyzer default to speak of.
				defaultValue: audience === 'markup' && toolDiffers ? null : analyzerDefault,
				csharp: { ...csharp, audience, markupDefaultValue: toolDiffers ? null : analyzerDefault },
				rules: related,
			};
		})
		.sort(byKey);
}

export function extractFromSources(sources: ExtractionSources): SourceCatalog {
	const readme = parseReadme(sources.readme.file, sources.readme.text);
	const markupIds = parseMarkupRuleIds(sources.markupDiagnostics.file, sources.markupDiagnostics.text);
	const markupProfiles = parseMarkupProfiles(sources.markupBehavior.file, sources.markupBehavior.text);
	const parsedRules = parseRules(sources, readme);
	const levels = parseLevels(sources.levels.file, sources.levels.text);

	checkLevels(sources, readme, levels, parsedRules);

	const parsedOptions = parseOptions(sources, readme, markupProfiles.names);
	const pairs = parsePairs(sources.gapOwners.file, sources.gapOwners.text);
	const listed = parseUndoneByCSharpier(sources.formatters.file, sources.formatters.text);

	checkNamedRules(sources, parsedRules, pairs, listed);
	checkPageReferences(parsedRules, parsedOptions);

	// A rule page that links the section says that CSharpier undoes the rule, as the section does for the rules it names.
	const undone = new Set([
		...listed,
		...sources.rules.filter((source) => source.text.includes(csharpierAnchor)).map((source) => idOfPage(source.file)),
	]);

	const rules = buildRules(readme, parsedRules, levels, markupIds, undone);

	return {
		rules,
		options: buildOptions(readme, parsedOptions, rules),
		pairs,
		groups: readme.groups,
		markupExtensions: markupProfiles.extensions,
		markupProfiles: markupProfiles.names,
	};
}

/** Reads the pages under a repository root and returns what the site shows. Throws an `ExtractionError` on a page it cannot read. */
export function extractCatalog(root: string): SourceCatalog {
	return extractFromSources({
		rules: readFiles(root, sourcePaths.rules, /^ZA\d{4}-.+\.md$/, 'rule'),
		options: readFiles(root, sourcePaths.options, /^stanza_[a-z0-9_]+\.md$/, 'option'),
		readme: readOne(root, sourcePaths.readme),
		markupBehavior: readOne(root, sourcePaths.markupBehavior),
		markupDiagnostics: readOne(root, sourcePaths.markupDiagnostics),
		gapOwners: readOne(root, sourcePaths.gapOwners),
		formatters: readOne(root, sourcePaths.formatters),
		levels: readOne(root, sourcePaths.levels),
	});
}
