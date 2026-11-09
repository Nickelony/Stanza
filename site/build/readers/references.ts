import type { RulePair } from '../../src/state/model.ts';
import { findTables } from '../render/markdown.ts';

import { ExtractionError, normalize, withFile } from './source.ts';

/** The line range under the `## <heading>` line of a page, up to the next `##` heading, or fails when the page has none. */
function sectionOf(file: string, lines: readonly string[], heading: string): { from: number; to: number } {
	const start = lines.indexOf(`## ${heading}`);

	if (start < 0) {
		throw new ExtractionError(file, `the page has no "## ${heading}" section`);
	}

	const end = lines.findIndex((line, index) => index > start && line.startsWith('## '));

	return { from: start + 1, to: end < 0 ? lines.length : end };
}

/** Returns the profiles of the markup behavior page, each with the extensions it claims, without the dot. */
export function parseMarkupProfiles(file: string, text: string): { names: string[]; extensions: string[] } {
	return withFile(file, () => {
		const lines = normalize(text).split('\n');
		const { from, to } = sectionOf(file, lines, 'Profiles');
		const names: string[] = [];
		const extensions: string[] = [];

		for (const table of findTables(lines, from, to)) {
			const nameColumn = table.head.findIndex((cell) => /^profile$/i.test(cell));
			const extensionColumn = table.head.findIndex((cell) => /^extensions$/i.test(cell));

			if (extensionColumn < 0) {
				continue;
			}

			for (const [row, cells] of table.rows.entries()) {
				const name = /^`([A-Za-z0-9-]+)`$/.exec(cells[nameColumn] ?? '')?.[1];
				const claimed = [...(cells[extensionColumn] ?? '').matchAll(/`\.([A-Za-z0-9]+)`/g)].map((match) => match[1]!);

				// A row with extensions and no readable name would add its extensions to the markup section and leave its profile out of the dropdown.
				if (!name && claimed.length > 0) {
					throw new ExtractionError(
						file,
						`the profile row "${lines[table.at + 2 + row]!.trim()}" claims extensions but its "Profile" cell is not a name in backticks`,
					);
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
	});
}

/** Returns the rule IDs `stanza-markup` declares, which are the rules it reads. */
export function parseMarkupRuleIds(file: string, text: string): Set<string> {
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

/** Reads one row of a pair table into a pair, from its cells and the number of its line. */
type PairRow = (cells: string[], lineNumber: number) => RulePair;

/** Reads the pair table under the `## <heading>` line of a page, and fails when it holds no row or a rule is a half of two pairs. */
function readPairTable(file: string, text: string, heading: string, readRow: PairRow): RulePair[] {
	return withFile(file, () => {
		const lines = normalize(text).split('\n');
		const { from, to } = sectionOf(file, lines, heading);
		const pairs = findTables(lines, from, to).flatMap((table) => table.rows.map((cells, row) => readRow(cells, table.at + row + 3)));

		if (pairs.length === 0) {
			throw new ExtractionError(file, 'the pair table holds no row');
		}

		const ids = pairs.flatMap((pair) => [pair.first, pair.second]);
		const twice = ids.find((id, at) => ids.indexOf(id) !== at);

		if (twice) {
			throw new ExtractionError(file, `${twice} is a half of two pairs`);
		}

		return pairs;
	});
}

/**
 * Returns the pairs of the pair table on the gap owners page.
 * A row holds the two halves as links in its second cell, plain or in backticks, and the default half in backticks in its third,
 * which also names the half that owns the gap when both are enabled.
 * A fourth cell, as the Python table has, names that owner on its own.
 */
export function parsePairs(file: string, text: string): RulePair[] {
	return readPairTable(file, text, 'Pairs and their default halves', (cells, lineNumber) => {
		const halves = [...(cells[1] ?? '').matchAll(/\[`?(ZA\d{4})`?\]/g)].map((match) => match[1]!);
		const named = [...(cells[2] ?? '').matchAll(/`(ZA\d{4})`/g)].map((match) => match[1]!);

		if (halves.length !== 2 || halves[0] === halves[1]) {
			throw new ExtractionError(file, `line ${lineNumber}: the "Halves" cell does not link two different rules`);
		}

		if (named.length === 0) {
			throw new ExtractionError(file, `line ${lineNumber}: the row of ${halves.join(' and ')} names no default half in backticks`);
		}

		const defaultHalf = named[0]!;
		const bothOwner = /`(ZA\d{4})` when both are enabled/.exec(cells[2] ?? '')?.[1];
		const ownerWhenBothEnabled = /`(ZA\d{4})`/.exec(cells[3] ?? '')?.[1] ?? bothOwner ?? defaultHalf;

		for (const id of [defaultHalf, ownerWhenBothEnabled]) {
			if (!halves.includes(id)) {
				throw new ExtractionError(file, `line ${lineNumber}: ${id} is no half of the pair ${halves.join(' and ')}`);
			}
		}

		return { first: halves[0]!, second: halves[1]!, defaultHalf, ownerWhenBothEnabled };
	});
}

/**
 * Returns the pairs of the pair table on the rule map of JS Stanza.
 * A row names the separating half in backticks in its second cell and the tight half in its third.
 * A preset enables the separating half, so it is the default half, and ESLint gives a gap to the tight half when both are enabled.
 */
export function parseTightPairs(file: string, text: string): RulePair[] {
	return readPairTable(file, text, 'Pairs and their tight halves', (cells, lineNumber) => {
		const [separating, tight] = [cells[1], cells[2]].map((cell) => /^`([a-z]+(?:-[a-z]+)*)`$/.exec(cell ?? '')?.[1]);

		if (separating === undefined || tight === undefined || separating === tight) {
			throw new ExtractionError(
				file,
				`line ${lineNumber}: the "Separating half" and "Tight half" cells are not two different rule names in backticks`,
			);
		}

		return { first: separating, second: tight, defaultHalf: separating, ownerWhenBothEnabled: tight };
	});
}

export const csharpierAnchor = 'Working-with-formatters#layout-that-csharpier-undoes';

/** Returns the IDs of the rules the section on layout that CSharpier undoes links, on the formatters page. */
export function parseUndoneByCSharpier(file: string, text: string): string[] {
	const lines = normalize(text).split('\n');
	const { from, to } = sectionOf(file, lines, 'Layout that CSharpier undoes');
	const section = lines.slice(from, to).join('\n');

	return [...new Set([...section.matchAll(/\/wiki\/(ZA\d{4})-/g)].map((match) => match[1]!))];
}
