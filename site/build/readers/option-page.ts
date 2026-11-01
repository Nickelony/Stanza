import { pageLinkAttributes, wikiUrl } from '../../src/address/links.ts';
import type {
	CSharpOption,
	Option,
	OptionExample,
	OptionKind,
	OptionPart,
	OptionSegment,
	OptionValue,
	SourceSnippet,
} from '../../src/state/model.ts';
import { parseBlocks, plainText, renderBlocks, renderInline, type RawBlock } from '../render/markdown.ts';

import { literalDefault } from './readme.ts';
import { ExtractionError, labelSegments, pageName, readParagraph, skipBlank, splitPage, type Chunk } from './source.ts';

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

export interface ParsedOption extends Omit<Option<SourceSnippet>, 'rules' | 'defaultValue' | 'csharp'> {
	/** What the page gives of the C# facts; the reader of all the pages adds the audience and the default of the tool. */
	csharp: Omit<CSharpOption, 'audience' | 'markupDefaultValue'>;
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

/** Whether the tool default of such a line is the profile's own, as the scope's is. */
const profileDefault = /\bthe profile's\b/;

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

/** Reads the text after the opening paragraph and the examples, which stay in the order the page writes them with the value table. */
function readOptionBody(
	first: string,
	rest: readonly Chunk[],
): { raw: Array<RawBlock | { kind: 'example'; index: number }>; examples: OptionExample<SourceSnippet>[] } {
	const raw: Array<RawBlock | { kind: 'example'; index: number }> = parseBlocks(first);
	const examples: OptionExample<SourceSnippet>[] = [];

	for (const chunk of rest) {
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

	return { raw, examples };
}

export function parseOptionPage(file: string, text: string, profiles: readonly string[] = []): ParsedOption {
	const name = pageName(file);
	const { head, rest } = splitPage(file, text);

	if ((head[0] ?? '') !== `# ${name}`) {
		throw new ExtractionError(file, `the first line is not "# ${name}"`);
	}

	const { lines: intro, end: cursor } = readParagraph(head, skipBlank(head, 1));
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
	const { raw, examples } = readOptionBody(head.slice(cursor).join('\n'), rest);
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
		valueHtml: renderInline(row[0] ?? '', pageLinkAttributes),
		effectHtml: renderInline(row[1] ?? '', pageLinkAttributes),
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

		return block.kind === 'example' ? block : renderBlocks([block], pageLinkAttributes)[0]!;
	});

	return {
		key: name,
		descriptionHtml: renderInline(intro.slice(0, defaultAt).join(' '), pageLinkAttributes),
		defaultText: plainText(defaultMarkdown),
		kind: classified.kind,
		choices: classified.choices,
		min: classified.min,
		max: classified.max,
		readByHtml: renderInline(readBy, pageLinkAttributes),
		examples,
		parts,
		readByRules: [...new Set(readBy.match(/ZA\d{4}/g) ?? [])],
		readByText: readBy,
		pageDefault: readDefault(file, defaultMarkdown, toolDiffers, tableLiterals),
		toolDiffers,

		csharp: {
			markupDefaultByProfile: profileDefault.test(plainText(defaultMarkdown)),
			perFile: classified.perFile,
			words: classified.words,
			values,
		},

		file,
		pageUrl: `${wikiUrl}/${name}`,
	};
}
