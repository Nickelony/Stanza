import { pageLinkAttributes, wikiUrl } from '../../src/address/links.ts';
import type { Rule, RuleExample, RulePart, SourceSnippet } from '../../src/state/model.ts';
import { parseBlocks, plainText, renderBlocks, renderInline } from '../render/markdown.ts';

import { parseTier, type ReadmeRule } from './readme.ts';
import { ExtractionError, labelSegments, pageName, readParagraph, skipBlank, splitPage, type Chunk, type CodeChunk } from './source.ts';

/** The bold line under the heading of a rule page: what a message calls it, the pattern it matches and an example of it. */
export interface LeadLine {
	name: string;
	pattern: RegExp;
	example: string;
}

/** Reads the bold line under the heading of a rule page and the sentence under it, and returns the first line after both. */
export function readLead(file: string, head: readonly string[], line: LeadLine): { bold: RegExpExecArray; description: string; cursor: number } {
	const at = skipBlank(head, 1);
	const bold = line.pattern.exec((head[at] ?? '').trim());

	if (!bold) {
		throw new ExtractionError(file, `the line after the heading is not the bold ${line.name} line, such as "${line.example}"`);
	}

	const description = readParagraph(head, at + 1);

	if (description.lines.length === 0) {
		throw new ExtractionError(file, `the ${line.name} line is not followed by a sentence on what the rule does`);
	}

	return { bold, description: description.lines.join(' '), cursor: description.end };
}

/** How a rule page turns its text and its code fences into parts. */
export interface BodyReader {
	/** Turns a run of the text between fences into parts. */
	text(text: string): RulePart[];

	/** Takes a fence that is no example of the rule, and says whether it did. */
	fence?(chunk: CodeChunk): boolean;
}

/** Reads what follows the lead of a rule page: its text and its examples, in the order the page writes them. */
export function readRuleBody(
	file: string,
	first: string,
	rest: readonly Chunk[],
	comment: string,
	reader: BodyReader,
): { parts: RulePart[]; examples: RuleExample<SourceSnippet>[] } {
	const parts = reader.text(first);
	const examples: RuleExample<SourceSnippet>[] = [];

	for (const chunk of rest) {
		if (chunk.kind === 'text') {
			parts.push(...reader.text(chunk.text));
			continue;
		}

		if (reader.fence?.(chunk)) {
			continue;
		}

		for (const example of ruleExamples(file, chunk.lang, chunk.code, comment)) {
			parts.push({ kind: 'example', index: examples.length });
			examples.push(example);
		}
	}

	if (examples.length === 0) {
		throw new ExtractionError(file, `the page has no "${comment} Before" and "${comment} After" example`);
	}

	return { parts, examples };
}

export function parseRulePage(
	file: string,
	text: string,
	readme: ReadmeRule | undefined,
): Omit<Rule<SourceSnippet>, 'csharp' | 'options' | 'level'> & { markupOnByDefault: boolean; optionKeys: string[]; file: string } {
	const name = pageName(file);
	const idFromName = /^(ZA\d{4})-/.exec(name)?.[1];

	if (!idFromName) {
		throw new ExtractionError(file, 'a rule page is named ZA<4 digits>-<title>.md');
	}

	const { head, rest } = splitPage(file, text);
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

	const lead = readLead(file, head, {
		name: 'tier',
		pattern: /^\*\*(.+)\.\*\*$/,
		example: '**Warning, on by default.**',
	});

	const tierText = lead.bold[1]!;
	const tier = parseTier(file, tierText);

	if (tier.onByDefault === null) {
		throw new ExtractionError(file, `the tier line "${tierText}" does not say "on by default" or "off by default"`);
	}

	if (tier.tier !== readme.tier || tier.onByDefault !== readme.onByDefault) {
		throw new ExtractionError(file, `the tier line says "${tierText}" but the readme row of ${id} disagrees`);
	}

	const { parts, examples } = readRuleBody(file, head.slice(lead.cursor).join('\n'), rest, '//', {
		text: (text) => renderBlocks(parseBlocks(text), pageLinkAttributes),
	});

	return {
		file,
		id,
		title,
		group: readme.group,
		summary: readme.summary,
		tier: tier.tier,
		onByDefault: tier.onByDefault,
		markupOnByDefault: tier.markupOn,
		tierText: plainText(tierText),
		descriptionHtml: renderInline(lead.description, pageLinkAttributes),
		examples,
		parts,
		optionKeys: [...new Set(text.match(/\bstanza_[a-z0-9]+(?:_[a-z0-9]+)*/g) ?? [])],
		pageUrl: `${wikiUrl}/${name}`,
	};
}

const beforeLabel = /^Before(?: - (.+))?$/;
const afterLabel = /^After(?: - (.+))?$/;
const unchangedLabel = /^Unchanged(?: - (.+))?$/;

/** Reads the Before, After and Unchanged segments of a code block; `comment` is what starts their labels. */
function ruleExamples(file: string, lang: string, code: string, comment: string): RuleExample<SourceSnippet>[] {
	const examples: RuleExample<SourceSnippet>[] = [];
	let current: RuleExample<SourceSnippet> | null = null;

	for (const segment of labelSegments(lang, code, true, comment)) {
		const before = beforeLabel.exec(segment.label);
		const after = afterLabel.exec(segment.label);
		const unchanged = unchangedLabel.exec(segment.label);

		if (before) {
			current = { beforeLabel: before[1] ?? '', before: { lang, code: segment.code }, afters: [], unchanged: false };
			examples.push(current);
		} else if (after) {
			if (!current) {
				throw new ExtractionError(file, `an "${comment} After" segment has no "${comment} Before" segment above it`);
			}

			current.afters.push({ label: after[1] ?? '', snippet: { lang, code: segment.code } });
		} else if (unchanged) {
			// An unchanged example stands alone, so an After below it belongs to no Before.
			examples.push({ beforeLabel: unchanged[1] ?? '', before: { lang, code: segment.code }, afters: [], unchanged: true });
			current = null;
		} else {
			throw new ExtractionError(
				file,
				`a code block holds "${segment.label || segment.code.split('\n')[0]}" where "${comment} Before", "${comment} After" or "${comment} Unchanged" is expected`,
			);
		}
	}

	for (const example of examples) {
		if (!example.unchanged && example.afters.length === 0) {
			throw new ExtractionError(file, `a "${comment} Before" segment has no "${comment} After" segment`);
		}
	}

	return examples;
}
