import type { RuleLevel, Tier } from '../../src/state/model.ts';
import { plainText, readTable } from '../render/markdown.ts';

import { ruleLevels } from './levels.ts';
import { ExtractionError, normalize, withFile } from './source.ts';

export interface ReadmeRule {
	id: string;
	group: string;
	summary: string;
	page: string;
	tier: Tier;
	onByDefault: boolean;

	/** The first level that enables the rule, from the Level column; `no-level` for a markup rule. */
	level: RuleLevel;
}

interface ReadmeOption {
	key: string;
	defaultCell: string;
}

export interface Readme {
	rules: ReadmeRule[];
	groups: string[];
	options: ReadmeOption[];
}

/** The words of the Level column, which write each level with a capital, and `No level` for a markup rule. */
const levelWords: Record<string, RuleLevel> = {
	...Object.fromEntries(ruleLevels.map((level) => [`${level[0]!.toUpperCase()}${level.slice(1)}`, level])),
	'No level': 'no-level',
};

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

/** Reads the rule row at `lines[at]` into `rules`; a row whose first cell is no rule ID belongs to no rule and is left alone. */
function readRuleRow(file: string, cells: string[], at: number, group: string, rules: ReadmeRule[]): void {
	const id = /^`(ZA\d{4})`$/.exec(cells[0]!)?.[1];

	if (id === undefined) {
		return;
	}

	const link = /^\[(.+)\]\((\S+)\)$/.exec(cells[1] ?? '');

	if (!group) {
		throw new ExtractionError(file, `line ${at + 1}: the row of ${id} is under no ### heading`);
	}

	if (!link) {
		throw new ExtractionError(file, `line ${at + 1}: the gap of ${id} is not a link to its page`);
	}

	if (rules.some((rule) => rule.id === id)) {
		throw new ExtractionError(file, `line ${at + 1}: ${id} has two rows`);
	}

	const tierCell = /^(\w+)(?:, (off) by default)?$/.exec((cells[2] ?? '').trim());

	if (!tierCell || !tierWords[tierCell[1]!]) {
		throw new ExtractionError(file, `line ${at + 1}: the tier "${cells[2]}" of ${id} is not "<Severity>[, off by default]"`);
	}

	const levelCell = (cells[3] ?? '').replaceAll('*', '').trim();

	if (!levelWords[levelCell]) {
		throw new ExtractionError(file, `line ${at + 1}: the level "${cells[3] ?? ''}" of ${id} is none of ${Object.keys(levelWords).join(', ')}`);
	}

	rules.push({
		id,
		group,
		summary: plainText(link[1]!),
		page: link[2]!.slice(link[2]!.lastIndexOf('/') + 1),
		tier: tierWords[tierCell[1]!]!,
		onByDefault: tierCell[2] === undefined,
		level: levelWords[levelCell]!,
	});
}

/** Reads the option row at `lines[at]` into `options`; a row that starts like a key's but is not one fails. */
function readOptionRow(file: string, cells: string[], at: number, options: ReadmeOption[]): void {
	const key = /^\[`(stanza_[a-z0-9_]+)`\]\([^)]*\)$/.exec(cells[0]!)?.[1];

	if (key !== undefined && cells[1]) {
		options.push({ key, defaultCell: cells[1] });
	} else if (/^[[`]/.test(cells[0]!)) {
		throw new ExtractionError(
			file,
			`line ${at + 1}: the row "${cells.join(' | ')}" looks like an option row but is not "| [\`<key>\`](<link>) | <default> |"`,
		);
	}
}

export function parseReadme(file: string, text: string): Readme {
	return withFile(file, () => readReadme(file, normalize(text).split('\n')));
}

function readReadme(file: string, lines: string[]): Readme {
	const rules: ReadmeRule[] = [];
	const options: ReadmeOption[] = [];

	let section = '';
	let group = '';

	for (let index = 0; index < lines.length; index++) {
		const heading = /^(#{2,3}) (.+)$/.exec(lines[index]!);

		if (heading) {
			if (heading[1] === '##') {
				section = heading[2]!;
				group = '';
			} else if (section === 'Rules') {
				group = heading[2]!;
			}

			continue;
		}

		const table = section === 'Rules' || section === 'Configuration' ? readTable(lines, index) : null;

		if (!table) {
			continue;
		}

		table.rows.forEach((cells, row) => {
			if (section === 'Rules') {
				readRuleRow(file, cells, index + 2 + row, group, rules);
			} else {
				readOptionRow(file, cells, index + 2 + row, options);
			}
		});

		index += table.rows.length + 1;
	}

	if (rules.length === 0) {
		throw new ExtractionError(file, 'no rule table was found under "## Rules"');
	}

	if (options.length === 0) {
		throw new ExtractionError(file, 'no option table was found under "## Configuration"');
	}

	return { rules, groups: [...new Set(rules.map((rule) => rule.group))], options };
}

/** Returns the value a default cell names when it is only backticked literals, otherwise `null`. */
export function literalDefault(cell: string): string | null {
	const tokens = [...cell.matchAll(/`([^`]*)`/g)].map((match) => match[1]!);
	const rest = cell.replace(/`[^`]*`/g, '').replace(/[,\s]/g, '');

	return tokens.length > 0 && rest === '' ? tokens.join(', ') : null;
}
