import { describe, expect, it } from 'vitest';

import { emptyConfig } from '../../src/state/config.ts';
import type { Selection } from '../../src/state/selection.ts';
import { isSelected, opening, type AppState } from '../../src/state/store.ts';
import { sample } from '../fixtures.ts';

describe('opening a selection', () => {
	const config = { ...emptyConfig(), options: { stanza_max_line_length: '120' } };
	const changed = { kind: 'option' as const, id: 'stanza_max_line_length' };
	const unchanged = { kind: 'option' as const, id: 'stanza_doc_indent_size' };

	it('shows the selection from its first variant and clears the text filter', () => {
		expect(opening(sample, config, changed)).toMatchObject({ selection: changed, variant: 0, query: '' });
	});

	it('leaves Changed only as it is when the selection is a changed row', () => {
		expect(opening(sample, config, changed)).not.toHaveProperty('changedOnly');
	});

	it('clears Changed only when the selection is not a changed row, which the filter would hide', () => {
		expect(opening(sample, config, unchanged)).toMatchObject({ selection: unchanged, changedOnly: false });
		expect(opening(sample, emptyConfig(), changed)).toMatchObject({ changedOnly: false });
	});

	it('clears Changed only when there is no selection', () => {
		expect(opening(sample, config, null)).toMatchObject({ selection: null, changedOnly: false });
	});
});

describe('the selection of a row', () => {
	const stateOf = (selection: Selection | null): AppState => ({ config: emptyConfig(), selection, query: '', changedOnly: false, variant: 0 });

	it('is the one the state holds, whichever list the row belongs to', () => {
		expect(isSelected(stateOf({ kind: 'rule', id: 'ZA0002' }), { kind: 'rule', id: 'ZA0002' })).toBe(true);
		expect(isSelected(stateOf({ kind: 'option', id: 'ZA0002' }), { kind: 'rule', id: 'ZA0002' })).toBe(false);
		expect(isSelected(stateOf(null), { kind: 'rule', id: 'ZA0002' })).toBe(false);
	});
});
