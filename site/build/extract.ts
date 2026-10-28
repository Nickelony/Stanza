import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import type {
	Audience,
	Option,
	OptionExample,
	OptionKind,
	OptionPart,
	OptionSegment,
	OptionValue,
	Rule,
	RuleExample,
	RulePart,
	SourceCatalog,
	SourceSnippet,
	Tier,
	Variant,
} from '../src/model.ts';
import { type LinkAttributes, MarkdownError, type RawBlock, parseBlocks, plainText, renderBlocks, renderInline, splitRow } from './markdown.ts';

const repoUrl = 'https://github.com/Nickelony/Stanza';
const wikiUrl = `${repoUrl}/wiki`;

/** The files the site reads, relative to the repository root. */
export const sourcePaths = {
	rules: 'docs/rules',
	options: 'docs/options',
	readme: 'src/Nickelony.Stanza.Analyzers/README.md',
	markupBehavior: 'docs/Markup-behavior.md',
	markupDiagnostics: 'src/Nickelony.Stanza.Markup.Core/MarkupDiagnostics.cs',
} as const;

/** Raised when a page does not have the shape the site reads; the message starts with the file. */
export class ExtractionError extends Error {
	readonly file: string;

	constructor(file: string, message: string) {
		super(`${file}: ${message}`);
		this.name = 'ExtractionError';
		this.file = file;
	}
}

export interface SourceFile {
	/** The path relative to the repository root, with forward slashes. */
	file: string;
	text: string;
}

export interface ExtractionSources {
	rules: SourceFile[];
	options: SourceFile[];
	readme: SourceFile;
	markupBehavior: SourceFile;
	markupDiagnostics: SourceFile;
}

const normalize = (text: string): string => text.replace(/\r\n?/g, '\n');

const pageName = (file: string): string => file.slice(file.lastIndexOf('/') + 1).replace(/\.md$/, '');

/** Opens a link to another rule or option page as a selection in the site. */
const pageLinks: LinkAttributes = (url) => {
	const rule = /^https:\/\/github\.com\/Nickelony\/Stanza\/wiki\/(ZA\d{4})-/.exec(url);
	const option = /^https:\/\/github\.com\/Nickelony\/Stanza\/wiki\/(stanza_[a-z0-9_]+)(?:#.*)?$/.exec(url);
	const selected = rule?.[1] ?? option?.[1];

	return ` target="_blank" rel="noopener"${selected ? ` data-select="${selected}"` : ''}`;
};

// ---------------------------------------------------------------------------------------------------------------------
// Fenced code
type Chunk = { kind: 'text'; text: string } | { kind: 'code'; lang: string; code: string };

/** Splits a page into the text between code fences and the fenced blocks. */
export function splitFences(file: string, text: string): Chunk[] {
	const chunks: Chunk[] = [];
	const lines = normalize(text).split('\n');

	let prose: string[] = [];

	for (let index = 0; index < lines.length; index++) {
		const open = /^```(\S*)\s*$/.exec(lines[index]!);

		if (!open) {
			// A fence the splitter does not recognize would leave its example in the prose, where it vanishes without a word.
			if (/^\s*(?:~~~|```)/.test(lines[index]!)) {
				throw new ExtractionError(
					file,
					`line ${index + 1} opens a code fence this build does not read; use a bare "\`\`\`" in the first column, with at most a language after it`,
				);
			}

			prose.push(lines[index]!);
			continue;
		}

		const start = index + 1;
		let end = start;

		while (end < lines.length && !/^```\s*$/.test(lines[end]!)) {
			end++;
		}

		if (end >= lines.length) {
			throw new ExtractionError(file, `the code fence opened on line ${index + 1} is never closed`);
		}

		chunks.push({ kind: 'text', text: prose.join('\n') });
		chunks.push({ kind: 'code', lang: open[1]!, code: lines.slice(start, end).join('\n') });

		prose = [];
		index = end;
	}

	chunks.push({ kind: 'text', text: prose.join('\n') });

	return chunks;
}

interface Segment {
	label: string;
	code: string;
}

const csharpLabel = /^\/\/ (.+)$/;
const xmlLabel = /^<!-- (.+) -->$/;
const ruleLabel = /^\/\/ ((?:Before|After|Unchanged)(?: - .+)?)$/;

/**
 * Cuts a code block at each comment that opens in its first column, which labels the lines under it.
 * A rule page labels only `Before`, `After` and `Unchanged`, because any other comment there is part of the example.
 */
function labelSegments(lang: string, code: string, rule = false): Segment[] {
	const label = rule ? ruleLabel : lang === 'csharp' ? csharpLabel : lang === 'xml' ? xmlLabel : null;
	const segments: Segment[] = [];

	let current: { label: string; lines: string[] } = { label: '', lines: [] };

	const close = (): void => {
		const text = current.lines.join('\n').replace(/\s+$/, '').replace(/^\n+/, '');

		if (text !== '' || current.label !== '') {
			segments.push({ label: current.label, code: text });
		}
	};

	for (const line of code.split('\n')) {
		const match = label?.exec(line);

		if (match) {
			close();
			current = { label: match[1]!, lines: [] };
		} else {
			current.lines.push(line);
		}
	}

	close();

	return segments.filter((segment) => segment.code !== '' || segment.label !== '');
}

// ---------------------------------------------------------------------------------------------------------------------
// The analyzer readme
interface ReadmeRule {
	id: string;
	group: string;
	summary: string;
	page: string;
	tier: Tier;
	onByDefault: boolean;
}

interface ReadmeOption {
	key: string;
	defaultCell: string;
}

interface Readme {
	rules: ReadmeRule[];
	groups: string[];
	options: ReadmeOption[];
}

const tierWords: Record<string, Tier> = {
	Error: 'error',
	Warning: 'warning',
	Suggestion: 'suggestion',
	Silent: 'silent',
};

export function parseTier(file: string, text: string): { tier: Tier; onByDefault: boolean | null; markupOn: boolean } {
	const match = /^(Error|Warning|Suggestion|Silent)(?:, (on|off) by default)?( in the analyzer and on in `stanza-markup`)?$/.exec(text.trim());

	if (!match) {
		throw new ExtractionError(file, `the tier "${text}" is not "<Severity>, on|off by default" in the shape the pages use`);
	}

	const onByDefault = match[2] === undefined ? null : match[2] === 'on';

	return { tier: tierWords[match[1]!]!, onByDefault, markupOn: match[3] !== undefined };
}

function parseReadme(file: string, text: string): Readme {
	const lines = normalize(text).split('\n');
	const rules: ReadmeRule[] = [];
	const groups: string[] = [];
	const options: ReadmeOption[] = [];

	let section = '';
	let group = '';

	// The cell count of the table being read, which its header sets; zero between tables.
	let columns = 0;

	lines.forEach((line, index) => {
		const heading = /^(#{2,3}) (.+)$/.exec(line);

		if (heading) {
			if (heading[1] === '##') {
				section = heading[2]!;
				group = '';
			} else if (section === 'Rules') {
				group = heading[2]!;
			}

			return;
		}

		if (section === 'Rules' && /^\| `ZA\d{4}` \|/.test(line)) {
			const cells = splitRow(line);
			const id = /`(ZA\d{4})`/.exec(cells[0]!)![1]!;
			const link = /^\[(.+)\]\((\S+)\)$/.exec(cells[1] ?? '');

			if (!group) {
				throw new ExtractionError(file, `line ${index + 1}: the row of ${id} is under no ### heading`);
			}

			if (!link) {
				throw new ExtractionError(file, `line ${index + 1}: the gap of ${id} is not a link to its page`);
			}

			if (rules.some((rule) => rule.id === id)) {
				throw new ExtractionError(file, `line ${index + 1}: ${id} has two rows`);
			}

			const tierCell = /^(\w+)(?:, (off) by default)?$/.exec((cells[2] ?? '').trim());

			if (!tierCell || !tierWords[tierCell[1]!]) {
				throw new ExtractionError(file, `line ${index + 1}: the tier "${cells[2]}" of ${id} is not "<Severity>[, off by default]"`);
			}

			if (!groups.includes(group)) {
				groups.push(group);
			}

			rules.push({
				id,
				group,
				summary: plainText(link[1]!),
				page: link[2]!.slice(link[2]!.lastIndexOf('/') + 1),
				tier: tierWords[tierCell[1]!]!,
				onByDefault: tierCell[2] === undefined,
			});
		}

		if (section === 'Configuration') {
			if (!line.startsWith('|')) {
				columns = 0;
			} else if (columns === 0) {
				columns = splitRow(line).length;
			} else if (splitRow(line).length !== columns) {
				throw new ExtractionError(file, `line ${index + 1}: the row has ${splitRow(line).length} cells under a header of ${columns}`);
			}

			const row = /^\| \[`(stanza_[a-z0-9_]+)`\]\([^)]*\) \| (.+) \|$/.exec(line);

			if (row) {
				options.push({ key: row[1]!, defaultCell: row[2]! });
			} else if (/^\| [[`]/.test(line)) {
				throw new ExtractionError(
					file,
					`line ${index + 1}: the row "${line}" looks like an option row but is not "| [\`<key>\`](<link>) | <default> |"`,
				);
			}
		}
	});

	if (rules.length === 0) {
		throw new ExtractionError(file, 'no rule table was found under "## Rules"');
	}

	if (options.length === 0) {
		throw new ExtractionError(file, 'no option table was found under "## Configuration"');
	}

	return { rules, groups, options };
}

/** Returns the value a default cell names when it is only backticked literals, otherwise `null`. */
export function literalDefault(cell: string): string | null {
	const tokens = [...cell.matchAll(/`([^`]*)`/g)].map((match) => match[1]!);
	const rest = cell.replace(/`[^`]*`/g, '').replace(/[,\s]/g, '');

	return tokens.length > 0 && rest === '' ? tokens.join(', ') : null;
}

// ---------------------------------------------------------------------------------------------------------------------
// Rule pages
export function parseRulePage(
	file: string,
	text: string,
	readme: ReadmeRule | undefined,
): Omit<Rule<SourceSnippet>, 'readByMarkup' | 'options'> & { optionKeys: string[] } {
	const name = pageName(file);
	const idFromName = /^(ZA\d{4})-/.exec(name)?.[1];

	if (!idFromName) {
		throw new ExtractionError(file, 'a rule page is named ZA<4 digits>-<title>.md');
	}

	const chunks = splitFences(file, text);
	const first = chunks[0]!;

	if (first.kind !== 'text') {
		throw new ExtractionError(file, 'the page starts with a code block');
	}

	const head = first.text.split('\n');
	const heading = /^# (ZA\d{4}) - (.+)$/.exec(head[0] ?? '');

	if (!heading) {
		throw new ExtractionError(file, 'the first line is not "# <ID> - <title>"');
	}

	if (heading[1] !== idFromName) {
		throw new ExtractionError(file, `the heading names ${heading[1]} but the file name names ${idFromName}`);
	}

	const id = heading[1]!;
	const title = heading[2]!.trim();

	if (!readme) {
		throw new ExtractionError(file, `${id} has no row in the rule table of the analyzer readme`);
	}

	if (readme.page !== name) {
		throw new ExtractionError(file, `the readme row of ${id} links ${readme.page}, not this page`);
	}

	let cursor = 1;

	while (cursor < head.length && head[cursor]!.trim() === '') {
		cursor++;
	}

	const tierLine = /^\*\*(.+)\.\*\*$/.exec((head[cursor] ?? '').trim());

	if (!tierLine) {
		throw new ExtractionError(file, 'the line after the heading is not the bold tier line, such as "**Warning, on by default.**"');
	}

	const tier = parseTier(file, tierLine[1]!);

	if (tier.onByDefault === null) {
		throw new ExtractionError(file, `the tier line "${tierLine[1]}" does not say "on by default" or "off by default"`);
	}

	if (tier.tier !== readme.tier || tier.onByDefault !== readme.onByDefault) {
		throw new ExtractionError(file, `the tier line says "${tierLine[1]}" but the readme row of ${id} disagrees`);
	}

	const descriptionLines: string[] = [];

	for (cursor += 1; cursor < head.length && head[cursor]!.trim() !== ''; cursor++) {
		descriptionLines.push(head[cursor]!.trim());
	}

	if (descriptionLines.length === 0) {
		throw new ExtractionError(file, 'the tier line is not followed by a sentence on what the rule does');
	}

	// The text after the description and the examples stay in the order the page writes them.
	const parts: RulePart[] = renderBlocks(parseBlocks(head.slice(cursor).join('\n')), pageLinks);
	const examples: RuleExample<SourceSnippet>[] = [];

	for (const chunk of chunks.slice(1)) {
		if (chunk.kind === 'text') {
			parts.push(...renderBlocks(parseBlocks(chunk.text), pageLinks));
			continue;
		}

		for (const example of ruleExamples(file, chunk.lang, chunk.code)) {
			parts.push({ kind: 'example', index: examples.length });
			examples.push(example);
		}
	}

	if (examples.length === 0) {
		throw new ExtractionError(file, 'the page has no "// Before" and "// After" example');
	}

	return {
		id,
		title,
		group: readme.group,
		summary: readme.summary,
		tier: tier.tier,
		onByDefault: tier.onByDefault,
		markupOnByDefault: tier.markupOn,
		tierText: plainText(tierLine[1]!),
		descriptionHtml: renderInline(descriptionLines.join(' '), pageLinks),
		examples,
		parts,
		optionKeys: [...new Set(text.match(/\bstanza_[a-z0-9]+(?:_[a-z0-9]+)*/g) ?? [])],
		wikiUrl: `${wikiUrl}/${name}`,
	};
}

const beforeLabel = /^Before(?: - (.+))?$/;
const afterLabel = /^After(?: - (.+))?$/;
const unchangedLabel = /^Unchanged(?: - (.+))?$/;

function ruleExamples(file: string, lang: string, code: string): RuleExample<SourceSnippet>[] {
	const examples: RuleExample<SourceSnippet>[] = [];
	let current: RuleExample<SourceSnippet> | null = null;

	for (const segment of labelSegments(lang, code, true)) {
		const before = beforeLabel.exec(segment.label);
		const after = afterLabel.exec(segment.label);
		const unchanged = unchangedLabel.exec(segment.label);

		if (before) {
			current = { beforeLabel: before[1] ?? '', before: { lang, code: segment.code }, afters: [], unchanged: false };
			examples.push(current);
		} else if (after) {
			if (!current) {
				throw new ExtractionError(file, `an "// After" segment has no "// Before" segment above it`);
			}

			current.afters.push({ label: after[1] ?? '', snippet: { lang, code: segment.code } } satisfies Variant<SourceSnippet>);
		} else if (unchanged) {
			// An unchanged example stands alone, so an After below it belongs to no Before.
			examples.push({ beforeLabel: unchanged[1] ?? '', before: { lang, code: segment.code }, afters: [], unchanged: true });
			current = null;
		} else if (segment.label === '' && segment.code === '') {
			continue;
		} else {
			throw new ExtractionError(
				file,
				`a code block holds "${segment.label || segment.code.split('\n')[0]}" where "// Before", "// After" or "// Unchanged" is expected`,
			);
		}
	}

	for (const example of examples) {
		if (!example.unchanged && example.afters.length === 0) {
			throw new ExtractionError(file, 'a "// Before" segment has no "// After" segment');
		}
	}

	return examples;
}

// ---------------------------------------------------------------------------------------------------------------------
// Option pages
const invalidRow = /\b(absent|anything else|below zero|above \d+|not a number|empty)\b/i;

interface Classified {
	kind: OptionKind;
	choices: string[];
	min: number | null;
	max: number | null;
	words: string[];
	perFile: boolean;
}

/** Reads the value column of an option page into the kind of input the key takes; a cell that names a profile makes a dropdown of the profiles. */
export function classifyValues(file: string, cells: string[], profiles: readonly string[] = []): Classified {
	const literals: string[] = [];
	const ranges: Array<[number, number]> = [];

	let list = false;
	let profile = false;

	for (const cell of cells) {
		const plain = plainText(cell);
		const range = /a number from (\d+) to (\d+)/.exec(plain);

		if (invalidRow.test(plain)) {
			continue;
		}

		if (range) {
			ranges.push([Number(range[1]), Number(range[2])]);
		} else if (/comma separated list/.test(plain)) {
			list = true;
		} else if (/a profile name/.test(plain)) {
			profile = true;
		} else {
			const found = [...cell.matchAll(/`([^`]+)`/g)].map((match) => match[1]!);

			const rest = cell
				.replace(/`[^`]*`/g, '')
				.replace(/\bor\b|,/g, '')
				.trim();

			if (found.length === 0 || rest !== '') {
				throw new ExtractionError(file, `the value cell "${plain}" is neither a literal, a number range nor a list`);
			}

			literals.push(...found);
		}
	}

	if (ranges.length > 0) {
		const numbers = literals.filter((literal) => /^\d+$/.test(literal)).map(Number);

		return {
			kind: 'number',
			choices: [],
			min: Math.min(...ranges.map(([low]) => low), ...numbers),
			max: Math.max(...ranges.map(([, high]) => high), ...numbers),
			words: [...new Set(literals.filter((literal) => !/^\d+$/.test(literal)))],
			perFile: false,
		};
	}

	if (list) {
		return { kind: 'list', choices: [], min: null, max: null, words: [], perFile: false };
	}

	if (profile) {
		if (profiles.length === 0) {
			throw new ExtractionError(file, 'the value table names a profile, but the markup behavior page lists none');
		}

		return { kind: 'enum', choices: [...profiles], min: null, max: null, words: [], perFile: true };
	}

	if (literals.length === 0) {
		throw new ExtractionError(file, 'the value table names no value');
	}

	return { kind: 'enum', choices: [...new Set(literals)], min: null, max: null, words: [], perFile: false };
}

interface ParsedOption extends Omit<Option<SourceSnippet>, 'rules' | 'audience' | 'defaultValue' | 'markupDefaultValue'> {
	readByRules: string[];
	readByText: string;

	/** The literal the Default line names, which the readme row overrides for the analyzer. */
	pageDefault: string | null;

	/** Whether the Default line gives the tool a default of its own, apart from the analyzer's. */
	toolDiffers: boolean;
	file: string;
}

/** A Default line such as "`all` in the analyzer, and the profile's own scope in the tool" gives the tool a default that depends on the file. */
const toolDefault = /\bin the tool\b|\bthe profile's\b/;

/** Reads the literal a Default line names. A line that names a value of the table without giving it whole, or that splits the tool from the analyzer unreadably, fails. */
function readDefault(file: string, markdown: string, toolDiffers: boolean, tableLiterals: ReadonlySet<string>): string | null {
	const analyzer = /^`([^`]+)` in the analyzer\b/.exec(markdown)?.[1];
	const whole = literalDefault(markdown) ?? /^`([^`]+)`, which /.exec(markdown)?.[1];
	const found = toolDiffers ? analyzer : whole;

	if (found !== undefined && found !== null) {
		return found;
	}

	if (toolDiffers) {
		throw new ExtractionError(file, 'the Default line gives the tool a default of its own but does not open with "`<value>` in the analyzer"');
	}

	const named = [...markdown.matchAll(/`([^`]+)`/g)].map((match) => match[1]!).filter((token) => tableLiterals.has(token));

	if (named.length > 0) {
		throw new ExtractionError(
			file,
			`the Default line names \`${named[0]}\`, a value of the table, but no literal default can be read from "${markdown}"`,
		);
	}

	return null;
}

export function parseOptionPage(file: string, text: string, profiles: readonly string[] = []): ParsedOption {
	const name = pageName(file);
	const chunks = splitFences(file, text);
	const first = chunks[0]!;

	if (first.kind !== 'text') {
		throw new ExtractionError(file, 'the page starts with a code block');
	}

	const head = first.text.split('\n');

	if ((head[0] ?? '') !== `# ${name}`) {
		throw new ExtractionError(file, `the first line is not "# ${name}"`);
	}

	let cursor = 1;

	while (cursor < head.length && head[cursor]!.trim() === '') {
		cursor++;
	}

	const intro: string[] = [];

	for (; cursor < head.length && head[cursor]!.trim() !== ''; cursor++) {
		intro.push(head[cursor]!.trim());
	}

	const defaultAt = intro.findIndex((line) => line.startsWith('Default: '));
	const readAt = intro.findIndex((line) => line.startsWith('Read by '));

	if (defaultAt < 1) {
		throw new ExtractionError(file, 'the opening paragraph has no "Default: ..." line after the sentence on what the key controls');
	}

	if (readAt <= defaultAt) {
		throw new ExtractionError(file, 'the opening paragraph has no "Read by ..." line after the "Default:" line');
	}

	const defaultMarkdown = intro.slice(defaultAt, readAt).join(' ').slice('Default: '.length).replace(/\.$/, '');
	const readBy = intro.slice(readAt).join(' ');

	// The text after the opening paragraph, the examples and the value table stay in the order the page writes them.
	const raw: Array<RawBlock | { kind: 'example'; index: number }> = parseBlocks(head.slice(cursor).join('\n'));
	const examples: OptionExample<SourceSnippet>[] = [];

	for (const chunk of chunks.slice(1)) {
		if (chunk.kind === 'text') {
			raw.push(...parseBlocks(chunk.text));
			continue;
		}

		raw.push({ kind: 'example', index: examples.length });

		examples.push({
			segments: labelSegments(chunk.lang, chunk.code).map((segment): OptionSegment<SourceSnippet> => {
				const value = (segment.label.split(/\s+/)[0] ?? '').replace(/^`|`?,?$/g, '');

				return {
					label: segment.label,
					value,
					isDefault: /\(default\)/.test(segment.label),
					snippet: { lang: chunk.lang, code: segment.code },
				};
			}),
		});
	}

	const valueTable = raw.find((block) => block.kind === 'table' && plainText(block.head[0] ?? '').toLowerCase() === 'value');

	if (!valueTable || valueTable.kind !== 'table') {
		throw new ExtractionError(file, 'the page has no table whose first column is "Value"');
	}

	const classified = classifyValues(
		file,
		valueTable.rows.map((row) => row[0] ?? ''),
		profiles,
	);

	const values: OptionValue[] = valueTable.rows.map((row) => ({
		valueHtml: renderInline(row[0] ?? '', pageLinks),
		effectHtml: renderInline(row[1] ?? '', pageLinks),
		literals: [...(row[0] ?? '').matchAll(/`([^`]+)`/g)].map((match) => match[1]!),
	}));

	const toolDiffers = toolDefault.test(plainText(defaultMarkdown));
	const tableLiterals = new Set(
		valueTable.rows
			.map((row) => row[0] ?? '')
			.filter((cell) => !invalidRow.test(plainText(cell)))
			.flatMap((cell) => [...cell.matchAll(/`([^`]+)`/g)].map((match) => match[1]!)),
	);

	const parts: OptionPart[] = raw.map((block): OptionPart => {
		if (block === valueTable) {
			return { kind: 'values' };
		}

		return block.kind === 'example' ? block : renderBlocks([block], pageLinks)[0]!;
	});

	return {
		key: name,
		descriptionHtml: renderInline(intro.slice(0, defaultAt).join(' '), pageLinks),
		defaultText: plainText(defaultMarkdown),
		kind: classified.kind,
		perFile: classified.perFile,
		choices: classified.choices,
		min: classified.min,
		max: classified.max,
		words: classified.words,
		readByHtml: renderInline(readBy, pageLinks),
		values,
		examples,
		parts,
		readByRules: [...new Set(readBy.match(/ZA\d{4}/g) ?? [])],
		readByText: readBy,
		pageDefault: readDefault(file, defaultMarkdown, toolDiffers, tableLiterals),
		toolDiffers,
		file,
		wikiUrl: `${wikiUrl}/${name}`,
	};
}

// ---------------------------------------------------------------------------------------------------------------------
// The other sources
/** Returns the profiles of the markup behavior page, each with the extensions it claims, without the dot. */
function parseMarkupProfiles(file: string, text: string): { names: string[]; extensions: string[] } {
	const lines = normalize(text).split('\n');
	const start = lines.findIndex((line) => line === '## Profiles');

	if (start < 0) {
		throw new ExtractionError(file, 'the page has no "## Profiles" section');
	}

	const names: string[] = [];
	const extensions: string[] = [];

	let nameColumn = -1;
	let extensionColumn = -1;

	for (let index = start + 1; index < lines.length; index++) {
		const line = lines[index]!;

		if (line.startsWith('## ')) {
			break;
		}

		if (!line.startsWith('|')) {
			continue;
		}

		const cells = splitRow(line);

		if (extensionColumn < 0) {
			nameColumn = cells.findIndex((cell) => /^profile$/i.test(cell));
			extensionColumn = cells.findIndex((cell) => /^extensions$/i.test(cell));
			continue;
		}

		if (!/^[-\s|:]+$/.test(line)) {
			const name = /^`([A-Za-z0-9-]+)`$/.exec(cells[nameColumn] ?? '')?.[1];
			const claimed = [...(cells[extensionColumn] ?? '').matchAll(/`\.([A-Za-z0-9]+)`/g)].map((match) => match[1]!);

			// A row with extensions and no readable name would add its extensions to the markup section and leave its profile out of the dropdown.
			if (!name && claimed.length > 0) {
				throw new ExtractionError(file, `the profile row "${line.trim()}" claims extensions but its "Profile" cell is not a name in backticks`);
			}

			if (name) {
				names.push(name);
			}

			extensions.push(...claimed);
		}
	}

	if (extensions.length === 0) {
		throw new ExtractionError(file, 'the profile table lists no extension in an "Extensions" column');
	}

	if (names.length === 0) {
		throw new ExtractionError(file, 'the profile table lists no profile name in a "Profile" column');
	}

	return { names: [...new Set(names)], extensions: [...new Set(extensions)] };
}

/** Returns the rule IDs `stanza-markup` declares, which are the rules it reads. */
function parseMarkupRuleIds(file: string, text: string): Set<string> {
	const declared = [...text.matchAll(/const string \w+Id = "(ZA\d{4})"/g)].map((match) => match[1]!);
	const written = [...text.matchAll(/"ZA\d{4}"/g)];

	if (declared.length === 0) {
		throw new ExtractionError(file, 'no "const string ...Id = \\"ZA<4 digits>\\"" declaration was found');
	}

	// A rule ID written another way, such as in a static readonly field, would drop its rule from the list without a word.
	if (written.length !== declared.length) {
		throw new ExtractionError(file, `${written.length} rule ID strings are written but ${declared.length} sit in a "const string ...Id" declaration`);
	}

	return new Set(declared);
}

// ---------------------------------------------------------------------------------------------------------------------
// The catalog
/** Names the file in a failure of a page: Markdown the renderer cannot print, or a shape nothing foresaw. */
function inFile(file: string, error: unknown): ExtractionError {
	if (error instanceof ExtractionError) {
		return error;
	}

	return new ExtractionError(file, error instanceof MarkdownError ? error.message : `unexpected failure: ${String(error)}`);
}

export function extractFromSources(sources: ExtractionSources): SourceCatalog {
	const readme = parseReadme(sources.readme.file, sources.readme.text);
	const markupIds = parseMarkupRuleIds(sources.markupDiagnostics.file, sources.markupDiagnostics.text);
	const markupProfiles = parseMarkupProfiles(sources.markupBehavior.file, sources.markupBehavior.text);
	const parsedRules = new Map<string, ReturnType<typeof parseRulePage>>();

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

	const parsedOptions: ParsedOption[] = [];

	for (const source of sources.options) {
		try {
			parsedOptions.push(parseOptionPage(source.file, source.text, markupProfiles.names));
		} catch (error) {
			throw inFile(source.file, error);
		}
	}

	for (const row of readme.options) {
		if (!parsedOptions.some((option) => option.key === row.key)) {
			throw new ExtractionError(sources.readme.file, `${row.key} has a row in the option table but no page under docs/options`);
		}
	}

	const optionKeys = new Set(parsedOptions.map((option) => option.key));

	const rules: Rule<SourceSnippet>[] = readme.rules.map((row) => {
		const { optionKeys: named, ...rule } = parsedRules.get(row.id)!;

		return { ...rule, readByMarkup: markupIds.has(row.id), options: named.filter((key) => optionKeys.has(key)).sort() };
	});

	const options: Option<SourceSnippet>[] = parsedOptions
		.map((parsed) => {
			const row = readme.options.find((candidate) => candidate.key === parsed.key);
			const mentionsMarkup = /stanza-markup/.test(parsed.readByText);

			if (!row && !mentionsMarkup) {
				throw new ExtractionError(parsed.file, 'the key is in no option table of the readme and its "Read by" text does not name stanza-markup');
			}

			const audience: Audience = row ? (mentionsMarkup ? 'both' : 'analyzer') : 'markup';
			const { readByRules, readByText: _text, pageDefault, toolDiffers, file: _file, ...option } = parsed;
			const related = rules.filter((rule) => readByRules.includes(rule.id) || rule.options.includes(parsed.key)).map((rule) => rule.id);
			const analyzerDefault = (row ? literalDefault(row.defaultCell) : null) ?? (row ? null : pageDefault);

			return {
				...option,
				audience,

				// A key only the tool reads has no analyzer default to speak of.
				defaultValue: audience === 'markup' && toolDiffers ? null : analyzerDefault,
				markupDefaultValue: toolDiffers ? null : analyzerDefault,
				rules: related,
			};
		})
		// A plain comparison orders by code unit, so the output does not depend on the ICU data of the Node that builds it.
		.sort((a, b) => (a.key < b.key ? -1 : Number(a.key > b.key)));

	return {
		rules,
		options,
		groups: readme.groups,
		markupExtensions: markupProfiles.extensions,
		markupProfiles: markupProfiles.names,
		repoUrl,
		wikiHomeUrl: wikiUrl,
	};
}

function readFiles(root: string, directory: string, pattern: RegExp, what: string): SourceFile[] {
	const absolute = join(root, directory);
	let names: string[];

	try {
		names = readdirSync(absolute).sort();
	} catch {
		throw new ExtractionError(directory, `the ${what} directory cannot be read from ${absolute}`);
	}

	const unexpected = names.filter((name) => !pattern.test(name));

	if (unexpected.length > 0) {
		throw new ExtractionError(`${directory}/${unexpected[0]}`, `is not a ${what} page by its name`);
	}

	return names.map((name) => ({ file: `${directory}/${name}`, text: readFileSync(join(absolute, name), 'utf8') }));
}

function readOne(root: string, file: string): SourceFile {
	try {
		return { file, text: readFileSync(join(root, file), 'utf8') };
	} catch {
		throw new ExtractionError(file, `cannot be read from ${join(root, file)}`);
	}
}

/** Reads the pages under a repository root and returns what the site shows. Throws an `ExtractionError` on a page it cannot read. */
export function extractCatalog(root: string): SourceCatalog {
	return extractFromSources({
		rules: readFiles(root, sourcePaths.rules, /^ZA\d{4}-.+\.md$/, 'rule'),
		options: readFiles(root, sourcePaths.options, /^stanza_[a-z0-9_]+\.md$/, 'option'),
		readme: readOne(root, sourcePaths.readme),
		markupBehavior: readOne(root, sourcePaths.markupBehavior),
		markupDiagnostics: readOne(root, sourcePaths.markupDiagnostics),
	});
}
