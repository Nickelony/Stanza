import { describe, expect, it } from 'vitest';

import { matchesFilter, tokenize } from '../../src/state/filter.ts';

describe('the filter words', () => {
	it('are the lowercase words of the text, with the space around them dropped', () => {
		expect(tokenize('  Blank   LINE ')).toEqual(['blank', 'line']);
		expect(tokenize('')).toEqual([]);
	});

	it('must all be in the text of a row, and Changed only lists only a changed row', () => {
		expect(matchesFilter('za0001 separate multi-line constructs', ['multi', 'za0001'], false, false)).toBe(true);
		expect(matchesFilter('za0001 separate multi-line constructs', ['multi', 'za0002'], false, false)).toBe(false);
		expect(matchesFilter('za0001', [], true, false)).toBe(false);
		expect(matchesFilter('za0001', [], true, true)).toBe(true);
	});
});
