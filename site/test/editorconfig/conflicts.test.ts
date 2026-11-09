import { describe, expect, it } from 'vitest';

import { findConflicts } from '../../src/editorconfig/conflicts.ts';
import { emptyConfig, hiddenByCSharpier } from '../../src/state/config.ts';
import type { RulePair } from '../../src/state/model.ts';
import { catalog, config, option, realCatalog, rule } from '../fixtures.ts';

const pairs: RulePair[] = [
	{ first: 'ZA0101', second: 'ZA0102', defaultHalf: 'ZA0102', ownerWhenBothEnabled: 'ZA0102' },
	{ first: 'ZA0306', second: 'ZA0307', defaultHalf: 'ZA0306', ownerWhenBothEnabled: 'ZA0307' },
];

const small = catalog(
	[
		rule('ZA0101', { onByDefault: false, undoneByCSharpier: true }),
		rule('ZA0102', { onByDefault: false }),
		rule('ZA0306'),
		rule('ZA0307', { onByDefault: false }),
		rule('ZA0402', { undoneByCSharpier: true }),
	],

	[option('stanza_max_line_length')],
	pairs,
);

describe('the conflicts of a pair', () => {
	it('are none while at most one half is set', () => {
		expect(findConflicts(small, emptyConfig())).toEqual([]);
		expect(findConflicts(small, config({ rules: { ZA0101: 'warning' } }))).toEqual([]);
		expect(findConflicts(small, config({ rules: { ZA0102: 'warning' } }))).toEqual([]);
	});

	it('name the half that owns the gap when both halves are enabled', () => {
		const [conflict, ...rest] = findConflicts(small, config({ rules: { ZA0101: 'warning', ZA0102: 'suggestion' } }));

		expect(rest).toEqual([]);
		expect(conflict).toMatchObject({ kind: 'pair', ids: ['ZA0101', 'ZA0102'], owner: 'ZA0102' });
		expect(conflict!.message).toContain('ZA0102 reports it');
		expect(conflict!.message).toContain('ZA0101 reports nothing');
	});

	it('say a gap or a construct, since a pair may judge a test for null', () => {
		const found = findConflicts(realCatalog, config({ rules: { ZA0651: 'warning', ZA0652: 'warning' } }));

		expect(found).toHaveLength(1);

		expect(found[0]!.message).toBe(
			'ZA0651 and ZA0652 judge the same gap or construct and both are enabled, so ZA0651 reports it and ZA0652 reports nothing. Enable one of them.',
		);
	});

	it('give the gap to the other half in the arrow pair, as the pair table says', () => {
		const [conflict] = findConflicts(small, config({ rules: { ZA0306: 'warning', ZA0307: 'warning' } }));
		expect(conflict).toMatchObject({ owner: 'ZA0307' });
	});

	it('count silent as enabled, and none and the default as not enabled', () => {
		expect(findConflicts(small, config({ rules: { ZA0101: 'silent', ZA0102: 'silent' } }))).toHaveLength(1);
		expect(findConflicts(small, config({ rules: { ZA0101: 'warning', ZA0102: 'none' } }))).toEqual([]);
		expect(findConflicts(small, config({ rules: { ZA0306: 'warning' } }))).toEqual([]);
	});

	it('cover every pair of the pages', () => {
		for (const pair of realCatalog.pairs) {
			const found = findConflicts(realCatalog, config({ rules: { [pair.first]: 'warning', [pair.second]: 'warning' } }));

			expect(
				found.map((conflict) => conflict.ids),
				`${pair.first} ${pair.second}`,
			).toEqual([[pair.first, pair.second]]);

			expect(found[0]!.owner).toBe(pair.ownerWhenBothEnabled);
		}
	});
});

describe('the conflicts of a pair across a level', () => {
	it('count a half the preset file enables, but not one that ships on without a file', () => {
		// ZA0306 is moderate and ZA0307 is strong in the fixtures' terms: ZA0306 is on by default, and ZA0307 is set by its own line.
		expect(findConflicts(small, config({ rules: { ZA0307: 'warning' } }))).toEqual([]);

		// At warning the package adds a preset file that states ZA0306, so a file enables it.
		const found = findConflicts(small, config({ severity: 'warning', rules: { ZA0307: 'warning' } }));

		expect(found.map((conflict) => conflict.ids)).toEqual([['ZA0306', 'ZA0307']]);
		expect(found[0]!.owner).toBe('ZA0307');
	});

	it('count a half the expanded file writes', () => {
		expect(findConflicts(small, config({ everySetting: true, rules: { ZA0307: 'warning' } })).map((conflict) => conflict.ids)).toEqual([
			['ZA0306', 'ZA0307'],
		]);
	});

	it('are none for the halves the level enables, since no level enables both', () => {
		for (const level of ['minimal', 'moderate', 'strong'] as const) {
			for (const formatter of ['stanza-format', 'csharpier'] as const) {
				expect(findConflicts(realCatalog, config({ level, formatter, severity: 'warning' })).filter((conflict) => conflict.kind === 'pair')).toEqual(
					[],
				);
			}
		}
	});
});

describe('the conflicts with CSharpier', () => {
	it('name a rule CSharpier undoes when it is enabled', () => {
		const found = findConflicts(small, config({ formatter: 'csharpier', rules: { ZA0101: 'warning' } }));

		expect(found.filter((conflict) => conflict.ids[0] === 'ZA0101')).toHaveLength(1);
		expect(found[0]).toMatchObject({ kind: 'formatter', ids: ['ZA0101'] });
		expect(found[0]!.message).toContain('CSharpier');
	});

	it('pass over a rule CSharpier undoes while it is off, and a rule it leaves alone', () => {
		expect(findConflicts(small, config({ formatter: 'csharpier', rules: { ZA0101: 'none', ZA0102: 'warning', ZA0402: 'none' } }))).toEqual([]);
	});

	it('count a rule that is on by default and that CSharpier undoes', () => {
		const found = findConflicts(small, config({ formatter: 'csharpier' }));
		expect(found.map((conflict) => conflict.ids)).toEqual([['ZA0402']]);
	});

	it('hide the rules CSharpier undoes, apart from one the configuration sets', () => {
		expect(hiddenByCSharpier(small.rules[0]!, config({ formatter: 'csharpier' }))).toBe(true);
		expect(hiddenByCSharpier(small.rules[0]!, config({ formatter: 'csharpier', rules: { ZA0101: 'warning' } }))).toBe(false);
		expect(hiddenByCSharpier(small.rules[0]!, emptyConfig())).toBe(false);
		expect(hiddenByCSharpier(small.rules[1]!, config({ formatter: 'csharpier' }))).toBe(false);
	});

	it('hide ZA0151 and ZA0161, and keep the rules that agree with CSharpier', () => {
		const csharpier = config({ formatter: 'csharpier' });

		const hidden = (id: string) =>
			hiddenByCSharpier(
				realCatalog.rules.find((rule) => rule.id === id)!,
				csharpier,
			);

		expect(hidden('ZA0151')).toBe(true);
		expect(hidden('ZA0161')).toBe(true);
		expect(hidden('ZA0199')).toBe(true);
		expect(hidden('ZA0198')).toBe(false);
	});
});
