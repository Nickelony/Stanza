import { posix } from 'node:path';

import { blobUrl, pageLinkAttributes } from '../../src/address/links.ts';
import {
	optionId,
	type Option,
	type OptionExample,
	type Rule,
	type RuleExample,
	type RulePair,
	type RulePart,
	type SourceCatalog,
	type SourceSnippet,
} from '../../src/state/model.ts';
import { parseBlocks, plainText, renderBlocks, renderInline, type RawBlock } from '../render/markdown.ts';

import { ruleLevels, type LeveledRule } from './levels.ts';
import { readLead, readRuleBody } from './rule-page.ts';
import { ExtractionError, normalize, pageName, splitPage, withFile } from './source.ts';

/** What differs between the rule pages of JS and Python. */
export interface GuideFlavor {
	/** The directory of the language under the repository root. */
	dir: string;

	/** What starts the label of a segment of an example: `//` or `#`. */
	comment: string;

	/** The first line of a rule page, with the `id` and, where the page has one, the `title` as named groups. */
	heading: RegExp;

	/** The `Rule.onByDefault` of a rule at a level: whether the tool applies it before the visitor configures anything. */
	shipsOn(level: LeveledRule): boolean;
}

/** A rule page as the shared reader sees it, before the language joins it to its code. */
export interface GuideRule {
	file: string;
	id: string;
	title: string;
	level: LeveledRule;
	tierText: string;
	descriptionHtml: string;
	examples: RuleExample<SourceSnippet>[];
	parts: RulePart[];
	pageUrl: string;

	/** The Markdown of each `##` section, by its heading, so a language reads its option tables from the sections it owns. */
	sections: Map<string, string>;

	/** The fenced blocks of the `Settings` section, the only section after the examples that holds any. */
	settingFences: SourceSnippet[];

	/** The text inside code spans and fences, where a page names a key of an option. */
	code: string;
}

/** A row of the rule table of a readme. */
interface GuideReadmeRow {
	id: string;
	page: string;
	level: LeveledRule;
	summary: string;
}

/** The address of a file of the repository on GitHub. */
export const repositoryFile = (path: string): string => `${blobUrl}/${path}`;

/** Turns a relative link of a page into the address of the file on GitHub, because the site cannot serve the repository. */
function resolveLink(file: string, link: string, target: string): string {
	if (target.startsWith('https://')) {
		return link;
	}

	if (/^[a-z]+:|^\/\//i.test(target)) {
		throw new ExtractionError(file, `the link target "${target}" is not an https:// address or a path of the repository`);
	}

	const [path = '', anchor] = target.split('#');
	const resolved = path === '' ? file : posix.normalize(posix.join(posix.dirname(file), path));

	return `](${repositoryFile(resolved)}${anchor === undefined ? '' : `#${anchor}`})`;
}

/** Resolves the relative links of a page, leaving the lines of its code fences and its inline code spans as written. */
export function resolveLinks(file: string, text: string): string {
	let fenced = false;

	return text
		.split('\n')
		.map((line) => {
			if (fenced ? /^```\s*$/.test(line) : /^```\S*\s*$/.test(line)) {
				fenced = !fenced;
				return line;
			}

			if (fenced) {
				return line;
			}

			return line
				.split(/(`[^`]*`)/)
				.map((piece, index) =>
					index % 2 === 1 ? piece : piece.replace(/\]\(([^)\s]+)\)/g, (link, target: string) => resolveLink(file, link, target)),
				)
				.join('');
		})
		.join('\n');
}

/** Reads the first table of a section's Markdown as rows of raw cells. */
export function tableOf(file: string, section: string | undefined, name: string): string[][] {
	const table = withFile(file, () => parseBlocks(section ?? '')).find(
		(block): block is Extract<RawBlock, { kind: 'table' }> => block.kind === 'table',
	);

	if (!table) {
		throw new ExtractionError(file, `the "${name}" section has no table`);
	}

	return table.rows;
}

/** Splits Markdown without fences into the text before the first `##` heading and the sections after it. */
function splitSections(text: string): Array<{ heading: string | null; text: string }> {
	const pieces: Array<{ heading: string | null; lines: string[] }> = [{ heading: null, lines: [] }];

	for (const line of text.split('\n')) {
		const heading = /^## (.+)$/.exec(line)?.[1];

		if (heading === undefined) {
			pieces[pieces.length - 1]!.lines.push(line);
		} else {
			pieces.push({ heading, lines: [] });
		}
	}

	return pieces.map((piece) => ({ heading: piece.heading, text: piece.lines.join('\n') }));
}

/** Reads a rule page: the heading, the level line, the description, the examples, and every `##` section after them. */
export function parseGuideRule(file: string, text: string, flavor: GuideFlavor): GuideRule {
	return withFile(file, () => readGuideRule(file, resolveLinks(file, normalize(text)), flavor));
}

function readGuideRule(file: string, text: string, flavor: GuideFlavor): GuideRule {
	const { head, rest } = splitPage(file, text);
	const heading = flavor.heading.exec(head[0] ?? '')?.groups;

	if (!heading?.['id']) {
		throw new ExtractionError(file, `the first line does not match ${flavor.heading}`);
	}

	const id = heading['id'];
	const title = heading['title'] ?? '';

	if (pageName(file) !== (title === '' ? id : `${id}-${title}`)) {
		throw new ExtractionError(file, `the heading names ${id}${title === '' ? '' : ` - ${title}`}, which the file name does not`);
	}

	const lead = readLead(file, head, {
		name: 'level',
		pattern: new RegExp(`^\\*\\*Level: (${ruleLevels.join('|')})\\.\\*\\*$`),
		example: '**Level: moderate.**',
	});

	const level = lead.bold[1] as LeveledRule;
	const sections = new Map<string, string>();
	const settingFences: SourceSnippet[] = [];
	const codeParts: string[] = [];

	let section: string | null = null;

	const readText = (body: string): RulePart[] => {
		const parts: RulePart[] = [];

		for (const piece of splitSections(body)) {
			if (piece.heading !== null) {
				section = piece.heading;
				sections.set(section, '');
				parts.push({ kind: 'p', html: `<strong>${renderInline(piece.heading)}</strong>` });
			}

			if (section !== null) {
				sections.set(section, `${sections.get(section)}${piece.text}\n`);
			}

			codeParts.push(...(piece.text.match(/`[^`]+`/g) ?? []));
			parts.push(...renderBlocks(parseBlocks(piece.text), pageLinkAttributes));
		}

		return parts;
	};

	const { parts, examples } = readRuleBody(file, head.slice(lead.cursor).join('\n'), rest, flavor.comment, {
		text: readText,

		fence: (chunk) => {
			codeParts.push(chunk.code);

			if (section === null) {
				return false;
			}

			// Only the Settings fences are read, so a fence under another heading would vanish without a word.
			if (section !== 'Settings') {
				throw new ExtractionError(file, `the code fence under "## ${section}" is read by nothing; only a "## Settings" section takes one`);
			}

			settingFences.push({ lang: chunk.lang, code: chunk.code });

			return true;
		},
	});

	return {
		file,
		id,
		title,
		level,
		tierText: `Level: ${level}`,
		descriptionHtml: renderInline(lead.description, pageLinkAttributes),
		examples,
		parts,
		pageUrl: repositoryFile(file),
		sections,
		settingFences,
		code: codeParts.join('\n'),
	};
}

/** Reads the rule table of a readme: the link and its text in the first cell, then the level, then what the rule does. */
export function parseGuideReadme(file: string, text: string): GuideReadmeRow[] {
	const section = new RegExp(`^## Rules\\n([\\s\\S]*?)(?=^## )`, 'm').exec(normalize(text))?.[1];

	if (section === undefined) {
		throw new ExtractionError(file, 'there is no "Rules" section');
	}

	const rows: GuideReadmeRow[] = [];
	const rulePages = `${blobUrl}/${posix.dirname(file)}/docs/rules/`;

	for (const row of tableOf(file, section, 'Rules')) {
		// A package readme links absolutely, because npm and PyPI show it away from the repository.
		const cell = /^\[`([^`]+)`\]\(([^)]+)\.md\)/.exec(row[0] ?? '');
		const level = (row[1] ?? '').trim();

		if (!cell?.[2]?.startsWith(rulePages)) {
			throw new ExtractionError(file, `the rule row "${row[0]}" does not start with a link to ${rulePages}<page>.md`);
		}

		const id = cell[1]!;

		if (!ruleLevels.includes(level as LeveledRule)) {
			throw new ExtractionError(file, `the level "${level}" of ${id} is none of ${ruleLevels.join(', ')}`);
		}

		rows.push({ id, page: cell[2].slice(rulePages.length), level: level as LeveledRule, summary: plainText(row[2] ?? '') });
	}

	if (rows.length === 0) {
		throw new ExtractionError(file, 'no rule was found');
	}

	return rows;
}

/** Joins the rule pages to the readme rows, and fails on a page with no row, a row with no page, and a level the two give differently. */
export function joinRules(readmeFile: string, rows: GuideReadmeRow[], pages: GuideRule[], flavor: GuideFlavor): Rule<SourceSnippet>[] {
	const byId = new Map(pages.map((page) => [page.id, page]));

	for (const page of pages) {
		if (!rows.some((row) => row.id === page.id)) {
			throw new ExtractionError(page.file, `${page.id} has a page but no row in the rule table of ${readmeFile}`);
		}
	}

	return rows.map((row) => {
		const page = byId.get(row.id);

		if (!page) {
			throw new ExtractionError(readmeFile, `${row.id} has a row in the rule table but no page under ${flavor.dir}/docs/rules`);
		}

		if (page.level !== row.level) {
			throw new ExtractionError(readmeFile, `the table gives ${row.id} the level ${row.level}, but its page says ${page.level}`);
		}

		return {
			id: page.id,
			title: page.title,
			group: `${row.level[0]!.toUpperCase()}${row.level.slice(1)}`,
			summary: row.summary,
			tier: 'error',
			onByDefault: flavor.shipsOn(row.level),
			level: row.level,
			tierText: page.tierText,
			descriptionHtml: page.descriptionHtml,
			examples: page.examples,
			parts: page.parts,
			options: [],
			pageUrl: page.pageUrl,
		};
	});
}

/**
 * The kind, choices and value of a default written as a code span: `true`, `false`, a whole number, a JSON list of strings,
 * or a string between double quotes, whose other choices only the code of the language knows, so `choices` is empty.
 */
export function parseDefault(file: string, cell: string): Pick<Option<SourceSnippet>, 'kind' | 'choices' | 'defaultValue' | 'defaultText'> {
	const literal = /^`(.+)`$/.exec(cell.trim())?.[1];
	const word = literal === undefined ? undefined : /^"([a-z]+)"$/.exec(literal)?.[1];

	if (word !== undefined) {
		return { kind: 'enum', choices: [], defaultValue: word, defaultText: `\`${literal}\`` };
	}

	if (literal !== undefined && /^\d+$/.test(literal)) {
		return { kind: 'number', choices: [], defaultValue: String(Number(literal)), defaultText: `\`${literal}\`` };
	}

	if (literal === 'true' || literal === 'false') {
		return { kind: 'enum', choices: ['true', 'false'], defaultValue: literal, defaultText: `\`${literal}\`` };
	}

	if (literal?.startsWith('[')) {
		const items = listOfStrings(file, literal);
		return { kind: 'list', choices: [], defaultValue: items.length === 0 ? null : items.join(', '), defaultText: `\`${literal}\`` };
	}

	throw new ExtractionError(file, `the default ${cell} is none of true, false, a whole number, a list of strings and a quoted word`);
}

/** Reads a list of strings written in either language: `["a", "b"]` or `['a', 'b']`. */
export function listOfStrings(file: string, literal: string): string[] {
	const items = [...literal.matchAll(/(["'])((?:(?!\1).)*)\1/g)].map((match) => match[2]!);
	const stripped = literal.replace(/(["'])(?:(?!\1).)*\1/g, '').replace(/[\s,[\]]/g, '');

	if (stripped !== '') {
		throw new ExtractionError(file, `${literal} is not a list of strings`);
	}

	return items;
}

/** The least and the greatest value of a whole number option. */
export interface NumberBounds {
	min: number;
	max: number;
}

/** A whole number a language declares in its code: its default and its bounds. */
export interface DeclaredNumber extends NumberBounds {
	value: number;
}

/** An option of a JS or Python page, with the parts the language reads from its own sources. */
interface GuideOptionInput {
	key: string;
	file: string;
	defaultCell: string;
	effect: string;
	readBy: string;
	pageUrl: string;
	global: boolean;
	examples?: SourceSnippet[];

	/** The bounds of a whole number, which the code of the language owns; a number has none without them. */
	bounds?: NumberBounds;

	/** The values a quoted word takes, or the words each item of a list takes, which the code of the language owns; a quoted default has none without them. */
	choices?: readonly string[];
}

/** Builds the option the page shows from the pieces a language read. */
export function buildGuideOption(input: GuideOptionInput): Option<SourceSnippet> {
	const parsed = parseDefault(input.file, input.defaultCell);

	if (parsed.kind === 'number' && input.bounds === undefined) {
		throw new ExtractionError(input.file, `the number ${input.key} has no bounds in the code of the language`);
	}

	if (parsed.kind === 'enum' && parsed.choices.length === 0) {
		if (input.choices === undefined || !input.choices.includes(parsed.defaultValue ?? '')) {
			throw new ExtractionError(input.file, `the default of ${input.key} is not one of the values the code of the language gives it`);
		}

		parsed.choices = [...input.choices];
	}

	if (parsed.kind === 'list' && input.choices !== undefined) {
		parsed.choices = [...input.choices];
	}

	const render = (markdown: string): string => withFile(input.file, () => renderInline(markdown, pageLinkAttributes));

	const examples: OptionExample<SourceSnippet>[] = (input.examples ?? []).map((snippet) => ({
		segments: [{ label: '', value: '', isDefault: false, snippet }],
	}));

	return {
		key: input.key,
		descriptionHtml: render(input.effect),
		...parsed,
		readByHtml: render(input.readBy),
		min: input.bounds?.min ?? null,
		max: input.bounds?.max ?? null,
		examples,
		parts: examples.map((_example, index) => ({ kind: 'example', index })),
		rules: [],
		pageUrl: input.pageUrl,
		global: input.global,
	};
}

/** Gives each half of a pair the other as its `opposite`, which is how the page shows a half as off while a file enables the other. */
export function markOpposites(rules: Rule<SourceSnippet>[], pairs: RulePair[]): void {
	for (const pair of pairs) {
		for (const [id, opposite] of [
			[pair.first, pair.second],
			[pair.second, pair.first],
		] as const) {
			rules.find((rule) => rule.id === id)!.opposite = opposite;
		}
	}
}

/** Fails when the default half of a pair is not the one half a level holds, because the default half is the one a level enables. */
export function checkDefaultHalf(file: string, pair: RulePair, isInLevel: (id: string) => boolean): void {
	const inLevel = [pair.first, pair.second].filter(isInLevel);

	if (inLevel.length !== 1 || inLevel[0] !== pair.defaultHalf) {
		throw new ExtractionError(
			file,
			`the default half of ${pair.first} and ${pair.second} is ${pair.defaultHalf}, but ${inLevel.join(' and ') || 'no half'} is in a level`,
		);
	}
}

/** Whether the text of a page names the key between code marks, which is how a page names an option. */
export function namesKey(code: string, key: string): boolean {
	const escaped = key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
	return new RegExp(`(?<![A-Za-z0-9_-])${escaped}(?![A-Za-z0-9_-])`).test(code);
}

/** Writes the rules each option names and the options each rule names, then assembles the catalog. */
export function assembleGuideCatalog(rules: Rule<SourceSnippet>[], options: Option<SourceSnippet>[], pages: GuideRule[]): SourceCatalog {
	const codeOf = new Map(pages.map((page) => [page.id, page.code]));

	for (const rule of rules) {
		rule.options = options
			.filter((option) => (option.global ? namesKey(codeOf.get(rule.id) ?? '', option.key) : option.rules.includes(rule.id)))
			.map(optionId);
	}

	for (const option of options) {
		if (option.global) {
			option.rules = rules.filter((rule) => rule.options.includes(optionId(option))).map((rule) => rule.id);
		}
	}

	return {
		rules,
		options,
		pairs: [],
		groups: [...new Set(rules.map((rule) => rule.group))],
		markupExtensions: [],
		markupProfiles: [],
	};
}
