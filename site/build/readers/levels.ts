import type { CSharpierMark, RuleLevel } from '../../src/state/model.ts';

import { ExtractionError, normalize } from './source.ts';

export interface LevelRow {
	level: RuleLevel;
	csharpier: CSharpierMark | null;
}

/** The levels a rule has in the analyzers, in the order the level table and the readme list them; a markup rule has none and no row in the table. */
export const ruleLevels = ['minimal', 'moderate', 'strong', 'opt-in'] as const satisfies readonly RuleLevel[];

/** The level of a rule that has one; JS Stanza and Python Stanza have no markup rule. */
export type LeveledRule = (typeof ruleLevels)[number];

/** The levels a preset holds. */
export type GuideLevel = Exclude<LeveledRule, 'opt-in'>;

/**
 * Reads the level table the preset files are written from: one `ID level [csharpier-off|csharpier-on]` per line,
 * with blank lines and `#` comments between them.
 */
export function parseLevels(file: string, text: string): Map<string, LevelRow> {
	const rows = new Map<string, LevelRow>();

	normalize(text)
		.split('\n')
		.forEach((raw, index) => {
			const line = raw.trim();

			if (line === '' || line.startsWith('#')) {
				return;
			}

			const match = /^(ZA\d{4})\s+(\S+)(?:\s+(csharpier-off|csharpier-on))?$/.exec(line);

			if (!match) {
				throw new ExtractionError(file, `line ${index + 1}: "${line}" is not "<ID> <level> [csharpier-off|csharpier-on]"`);
			}

			const [, id, level, mark] = match;

			if (!ruleLevels.some((known) => known === level)) {
				throw new ExtractionError(file, `line ${index + 1}: the level "${level}" of ${id} is none of ${ruleLevels.join(', ')}`);
			}

			if (rows.has(id!)) {
				throw new ExtractionError(file, `line ${index + 1}: ${id} has two rows`);
			}

			rows.set(id!, { level: level as RuleLevel, csharpier: mark === undefined ? null : mark === 'csharpier-off' ? 'off' : 'on' });
		});

	if (rows.size === 0) {
		throw new ExtractionError(file, 'no rule was found');
	}

	return rows;
}
