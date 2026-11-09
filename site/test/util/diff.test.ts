import { describe, expect, it } from 'vitest';

import { diffLines } from '../../src/util/diff.ts';

describe('the line diff', () => {
	it('marks an added blank line on the After side only', () => {
		const diff = diffLines(['a', 'b', 'c'], ['a', '', 'b', 'c']);

		expect([...diff.added]).toEqual([1]);
		expect([...diff.removed]).toEqual([]);
	});

	it('marks a removed blank line on the Before side only', () => {
		const diff = diffLines(['a', '', 'b'], ['a', 'b']);

		expect([...diff.removed]).toEqual([1]);
		expect([...diff.added]).toEqual([]);
	});

	it('marks a rewritten line on both sides', () => {
		const diff = diffLines(['a', 'b => c;', 'd'], ['a', 'b =>', 'c;', 'd']);

		expect([...diff.removed]).toEqual([1]);
		expect([...diff.added]).toEqual([1, 2]);
	});

	it('puts the added blank line where it was inserted when blank lines already stand nearby', () => {
		const diff = diffLines(['a', 'b', '', 'c'], ['a', '', 'b', '', 'c']);
		expect([...diff.added]).toEqual([1]);
	});

	it('ignores trailing whitespace', () => {
		expect(diffLines(['a  '], ['a']).added.size).toBe(0);
	});
});
