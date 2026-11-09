import { describe, expect, it } from 'vitest';

import { emptyConfig } from '../../src/state/config.ts';
import { firstChangedSelection, firstSelection, isSelectionChanged, selectionOf } from '../../src/state/selection.ts';
import { catalog, option, sample } from '../fixtures.ts';

describe('the selection of an ID', () => {
	it('names a rule by its ID and an option by its key, and nothing for an unknown name', () => {
		expect(selectionOf(sample, 'ZA0014')).toEqual({ kind: 'rule', id: 'ZA0014' });
		expect(selectionOf(sample, 'stanza_doc_indent_size')).toEqual({ kind: 'option', id: 'stanza_doc_indent_size' });
		expect(selectionOf(sample, 'ZA9999')).toBeNull();
		expect(selectionOf(sample, '')).toBeNull();
	});

	it('names the first rule or option of a tab, and nothing for an empty catalog', () => {
		expect(firstSelection(sample, 'rule')).toEqual({ kind: 'rule', id: 'ZA0002' });
		expect(firstSelection(sample, 'option')).toEqual({ kind: 'option', id: 'stanza_max_line_length' });
		expect(firstSelection(catalog([], []), 'rule')).toBeNull();
		expect(firstSelection(catalog([], []), 'option')).toBeNull();
	});
});

describe('the selection of an option two rules take under one key', () => {
	const same = catalog(
		[],
		['a', 'b'].map((scope) => option('objectMembers', { kind: 'enum', choices: ['true', 'false'], defaultValue: 'true', scope, rules: [scope] })),
	);

	it('names each by the rule and the key, and not by the key alone', () => {
		expect(selectionOf(same, 'a.objectMembers')).toEqual({ kind: 'option', id: 'a.objectMembers' });
		expect(selectionOf(same, 'b.objectMembers')).toEqual({ kind: 'option', id: 'b.objectMembers' });
		expect(selectionOf(same, 'objectMembers')).toBeNull();
		expect(firstSelection(same, 'option')).toEqual({ kind: 'option', id: 'a.objectMembers' });
	});

	it('holds each changed by its own value', () => {
		const config = { ...emptyConfig(), options: { 'b.objectMembers': 'false' } };

		expect(isSelectionChanged(same, config, { kind: 'option', id: 'a.objectMembers' })).toBe(false);
		expect(isSelectionChanged(same, config, { kind: 'option', id: 'b.objectMembers' })).toBe(true);
		expect(firstChangedSelection(same, config, 'option')).toEqual({ kind: 'option', id: 'b.objectMembers' });
	});
});

describe('whether a selection is a changed row', () => {
	const config = { ...emptyConfig(), rules: { ZA0002: 'none' as const }, options: { stanza_max_line_length: '120' } };

	it('is true for a rule or an option the configuration changes', () => {
		expect(isSelectionChanged(sample, config, { kind: 'rule', id: 'ZA0002' })).toBe(true);
		expect(isSelectionChanged(sample, config, { kind: 'option', id: 'stanza_max_line_length' })).toBe(true);
	});

	it('is false for one left at its default, one set to its default, and one the catalog lacks', () => {
		const atDefault = { ...config, options: { stanza_max_line_length: '100' } };

		expect(isSelectionChanged(sample, config, { kind: 'rule', id: 'ZA0014' })).toBe(false);
		expect(isSelectionChanged(sample, config, { kind: 'option', id: 'stanza_doc_indent_size' })).toBe(false);
		expect(isSelectionChanged(sample, atDefault, { kind: 'option', id: 'stanza_max_line_length' })).toBe(false);
		expect(isSelectionChanged(sample, config, { kind: 'rule', id: 'ZA9999' })).toBe(false);
	});
});

describe('the first changed selection of a tab', () => {
	it('names the first rule or option the configuration changes, in catalog order', () => {
		const config = { ...emptyConfig(), rules: { ZA0014: 'error' as const }, options: { stanza_doc_indent_size: '4', stanza_max_line_length: '120' } };

		expect(firstChangedSelection(sample, config, 'rule')).toEqual({ kind: 'rule', id: 'ZA0014' });
		expect(firstChangedSelection(sample, config, 'option')).toEqual({ kind: 'option', id: 'stanza_max_line_length' });
	});

	it('names nothing for a kind the configuration leaves alone', () => {
		const config = { ...emptyConfig(), options: { stanza_max_line_length: '120' } };

		expect(firstChangedSelection(sample, config, 'rule')).toBeNull();
		expect(firstChangedSelection(sample, emptyConfig(), 'option')).toBeNull();
	});
});
