import { describe, expect, it } from 'vitest';

import { failsOn, realCombined, realSources } from '../../fixtures.ts';

describe('the conflict facts of the pages', () => {
	const gapPath = 'docs/Which-rule-owns-which-gap.md';
	const formattersPath = 'docs/Working-with-formatters.md';
	const pairOf = (id: string) => realCombined.pairs.find((pair) => pair.first === id || pair.second === id);

	it('read a pair from every row of the pair table, with no list of pairs in the site', () => {
		const rows = realSources()
			.gapOwners.text.split('## Pairs and their default halves')[1]!
			.split('\n## ')[0]!
			.split('\n')
			.filter((line) => /^\| .*ZA\d{4}/.test(line));

		expect(rows.length).toBeGreaterThan(0);
		expect(realCombined.pairs).toHaveLength(rows.length);
	});

	it('read the halves, the default half and the owner when both are enabled', () => {
		expect(pairOf('ZA0198')).toEqual({ first: 'ZA0198', second: 'ZA0199', defaultHalf: 'ZA0198', ownerWhenBothEnabled: 'ZA0198' });
		expect(pairOf('ZA2009')).toEqual({ first: 'ZA2008', second: 'ZA2009', defaultHalf: 'ZA2008', ownerWhenBothEnabled: 'ZA2008' });
		expect(pairOf('ZA0151')).toEqual({ first: 'ZA0151', second: 'ZA0152', defaultHalf: 'ZA0152', ownerWhenBothEnabled: 'ZA0152' });
		expect(pairOf('ZA0306')).toEqual({ first: 'ZA0306', second: 'ZA0307', defaultHalf: 'ZA0306', ownerWhenBothEnabled: 'ZA0307' });
		expect(pairOf('ZA0652')).toEqual({ first: 'ZA0651', second: 'ZA0652', defaultHalf: 'ZA0651', ownerWhenBothEnabled: 'ZA0651' });
	});

	it('mark the rules whose layout CSharpier undoes', () => {
		const undone = realCombined.rules.filter((rule) => rule.csharp!.undoneByCSharpier).map((rule) => rule.id);

		for (const id of ['ZA0101', 'ZA0141', 'ZA0151', 'ZA0161', 'ZA0171', 'ZA0201', 'ZA0199', 'ZA2009']) {
			expect(undone, id).toContain(id);
		}

		for (const id of ['ZA0002', 'ZA0152', 'ZA0198', 'ZA2008', 'ZA2005', 'ZA0404']) {
			expect(undone, id).not.toContain(id);
		}
	});

	it('fails the build when a row of the pair table names a rule with no page', () => {
		failsOn(gapPath, (text) => text.replace('ZA0198-Add-a-trailing-comma', 'ZA0999-Nothing').replace('[ZA0198]', '[ZA0999]'), /ZA0999/);
	});

	it('fails the build when a row of the pair table names no default half', () => {
		failsOn(gapPath, (text) => text.replace(/\| `ZA0198` \|/, '| none |'), /default half/);
	});

	it('fails the build when the pair table is gone', () => {
		failsOn(gapPath, (text) => text.replace('## Pairs and their default halves', '## Pairs'), /Pairs and their default halves/);
	});

	it('fails the build when the formatter section is gone', () => {
		failsOn(formattersPath, (text) => text.replace('## Layout that CSharpier undoes', '## Layout CSharpier changes'), /Layout that CSharpier undoes/);
	});
});
