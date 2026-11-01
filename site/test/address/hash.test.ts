import { describe, expect, it } from 'vitest';

import { decodeHash, encodeHash, patchFromHash } from '../../src/address/hash.ts';
import { csharp } from '../../src/languages/csharp.ts';
import { javascript } from '../../src/languages/javascript.ts';
import { markup } from '../../src/languages/markup.ts';
import { pages } from '../../src/languages/pages.ts';
import { python } from '../../src/languages/python.ts';
import { emptyConfig } from '../../src/state/config.ts';
import type { AppState } from '../../src/state/store.ts';
import { markupConfig, realCatalog, realJavascript, realMarkup, realPython, sample } from '../fixtures.ts';

describe('the address hash', () => {
	it('round-trips a configuration and a selection', () => {
		const config = {
			...emptyConfig(),
			everySetting: true,
			markupSection: false,
			rules: { ZA0014: 'warning' as const, ZA0002: 'none' as const },
			options: { stanza_assertion_roots: 'Verify, Assert' },
		};

		const hash = encodeHash(sample, { config, selection: { kind: 'rule', id: 'ZA0014' } }, csharp);

		expect(hash).toBe('ZA0002=n&ZA0014=w&stanza_assertion_roots=Verify%2C+Assert&all=1&markup=0&sel=ZA0014');
		expect(decodeHash(sample, `#${hash}`, csharp)).toEqual({ config, selection: { kind: 'rule', id: 'ZA0014' } });
	});

	it('carries the level, the formatter and the severity, and drops each that is the default', () => {
		const config = { ...emptyConfig(), level: 'strong' as const, formatter: 'csharpier' as const, severity: 'error' as const };
		const hash = encodeHash(sample, { config, selection: null }, csharp);

		expect(hash).toBe('level=strong&formatter=csharpier&severity=error');
		expect(decodeHash(sample, `#${hash}`, csharp).config).toMatchObject({ level: 'strong', formatter: 'csharpier', severity: 'error' });
		expect(encodeHash(sample, { config: { ...emptyConfig(), level: 'moderate' }, selection: null }, csharp)).toBe('');
		expect(decodeHash(sample, '#level=huge&formatter=other&severity=info', csharp).config).toMatchObject(emptyConfig());
	});

	it('drops a key the catalog does not know, a letter that is no severity, and a value the page does not allow', () => {
		const state = decodeHash(sample, '#ZA9999=w&ZA0002=x&ZA0014=s&stanza_max_line_length=wide&stanza_multi_line_member_part=body&sel=ZA9999', csharp);

		expect(state.config.rules).toEqual({ ZA0014: 'suggestion' });
		expect(state.config.options).toEqual({ stanza_multi_line_member_part: 'body' });
		expect(state.selection).toBeNull();
	});

	it('selects an option by its key', () => {
		expect(decodeHash(sample, '#sel=stanza_max_line_length', csharp).selection).toEqual({ kind: 'option', id: 'stanza_max_line_length' });
	});
});

describe('an address the visitor edits', () => {
	const current: AppState = {
		config: { ...emptyConfig(), rules: { ZA0002: 'none' } },
		selection: { kind: 'rule', id: 'ZA0014' },
		query: 'blank',
		changedOnly: true,
		variant: 1,
	};

	it('replaces the configuration and opens the selection it names on the first variant', () => {
		const next = { ...current, ...patchFromHash(sample, 'ZA0014=e&sel=stanza_max_line_length', csharp) };

		expect(next.config.rules).toEqual({ ZA0014: 'error' });
		expect(next.selection).toEqual({ kind: 'option', id: 'stanza_max_line_length' });
		expect(next.variant).toBe(0);
	});

	it('opens the selection on the first variant when it names the one already shown', () => {
		expect({ ...current, ...patchFromHash(sample, 'sel=ZA0014', csharp) }.variant).toBe(0);
	});

	it('keeps the selection and its variant when it names none, or one the catalog lacks', () => {
		for (const hash of ['ZA0002=e', 'ZA0002=e&sel=ZA9999', '']) {
			const next = { ...current, ...patchFromHash(sample, hash, csharp) };

			expect(next.selection).toEqual(current.selection);
			expect(next.variant).toBe(1);
		}
	});

	it('clears the filter when it names a selection the filter would hide', () => {
		const next = { ...current, ...patchFromHash(sample, 'sel=ZA0002', csharp) };
		expect([next.query, next.changedOnly]).toEqual(['', false]);
	});

	it('keeps Changed only when it names a selection that is a changed row', () => {
		const next = { ...current, ...patchFromHash(sample, 'ZA0002=n&sel=ZA0002', csharp) };
		expect([next.query, next.changedOnly]).toEqual(['', true]);
	});

	it('leaves the filter alone when it names no selection', () => {
		const next = { ...current, ...patchFromHash(sample, 'ZA0002=e', csharp) };
		expect([next.query, next.changedOnly]).toEqual(['blank', true]);
	});
});

describe('the address hash of JS and Python', () => {
	it('round-trips a JS configuration, with the severity warn written only when it is not the default', () => {
		const config = {
			...emptyConfig('error'),
			level: 'strong' as const,
			severity: 'warning' as const,
			rules: { 'separate-await-runs': 'none' as const },
			options: { 'separate-import-groups.typeImports': 'true', assertionRoots: 'expect, should' },
		};

		const hash = encodeHash(realJavascript, { config, selection: { kind: 'option', id: 'separate-import-groups.typeImports' } }, javascript);

		expect(hash).toBe(
			'separate-await-runs=n&assertionRoots=expect%2C+should&separate-import-groups.typeImports=true&level=strong&severity=warning&sel=separate-import-groups.typeImports',
		);

		expect(decodeHash(realJavascript, hash, javascript)).toEqual({
			config,
			selection: { kind: 'option', id: 'separate-import-groups.typeImports' },
		});

		expect(encodeHash(realJavascript, { config: emptyConfig('error'), selection: null }, javascript)).toBe('');
	});

	it('round-trips the option of each rule that takes the same name, under the rule it belongs to', () => {
		const keep = 'keep-single-line-elements-together.objectMembers';
		const separate = 'separate-multi-line-elements.objectMembers';
		const config = { ...emptyConfig('error'), options: { [keep]: 'false' } };
		const hash = encodeHash(realJavascript, { config, selection: { kind: 'option', id: keep } }, javascript);

		expect(hash).toBe(`${keep}=false&sel=${keep}`);
		expect(decodeHash(realJavascript, hash, javascript)).toEqual({ config, selection: { kind: 'option', id: keep } });

		const other = decodeHash(realJavascript, `${separate}=false`, javascript);

		expect(other.config.options).toEqual({ [separate]: 'false' });
		expect(decodeHash(realJavascript, 'objectMembers=false', javascript).config.options).toEqual({});
	});

	it('drops what only the C# page has, and a severity the language lacks', () => {
		const state = decodeHash(
			realJavascript,
			'#all=1&markup=0&formatter=csharpier&severity=suggestion&separate-await-runs=s&separate-jump-statements=w',
			javascript,
		);

		expect(state.config).toMatchObject({ everySetting: false, markupSection: true, formatter: 'stanza-format', severity: 'error' });
		expect(state.config.rules).toEqual({ 'separate-jump-statements': 'warning' });
	});

	it('round-trips a Python configuration, which has a level, rules that are on or off, and keys', () => {
		const config = {
			...emptyConfig('error'),
			level: 'minimal' as const,
			rules: { ZA3011: 'error' as const, ZA3001: 'none' as const },
			options: { 'max-loose-run-size': '5' },
		};

		const hash = encodeHash(realPython, { config, selection: { kind: 'rule', id: 'ZA3011' } }, python);

		expect(hash).toBe('ZA3001=n&ZA3011=e&max-loose-run-size=5&level=minimal&sel=ZA3011');
		expect(decodeHash(realPython, hash, python)).toEqual({ config, selection: { kind: 'rule', id: 'ZA3011' } });
		expect(decodeHash(realPython, '#ZA3001=w&severity=warning', python).config).toMatchObject({ rules: {}, severity: 'error' });
	});
});

describe('the address hash of the Markup page', () => {
	const none = markupConfig({});

	it('round-trips a configuration of rules, keys and the two checkboxes, and holds no level', () => {
		const config = {
			...none,
			everySetting: true,
			markupSection: false,
			rules: { ZA2001: 'none' as const, ZA2002: 'warning' as const },
			options: { stanza_markup_scope: 'containers', stanza_markup_separated_elements: 'Separator, Divider' },
		};

		const hash = encodeHash(realMarkup, { config, selection: { kind: 'rule', id: 'ZA2002' } }, markup);

		expect(hash).toBe(
			'ZA2001=n&ZA2002=w&stanza_markup_scope=containers&stanza_markup_separated_elements=Separator%2C+Divider&all=1&markup=0&sel=ZA2002',
		);

		expect(decodeHash(realMarkup, `#${hash}`, markup)).toEqual({ config, selection: { kind: 'rule', id: 'ZA2002' } });
	});

	it('writes nothing for the configuration the page opens on', () => {
		expect(encodeHash(realMarkup, { config: none, selection: null }, markup)).toBe('');
		expect(decodeHash(realMarkup, '', markup).config).toEqual(none);
	});

	it('keeps the none level whatever the address says, and drops the picks it lacks', () => {
		const state = decodeHash(realMarkup, '#level=strong&formatter=csharpier&severity=error&ZA2002=s', markup);

		expect(state.config).toMatchObject({ level: 'none', formatter: 'stanza-format', severity: 'suggestion' });
		expect(state.config.rules).toEqual({ ZA2002: 'suggestion' });
	});

	it('never writes the none level into the address, though the form holds it', () => {
		expect(encodeHash(realMarkup, { config: { ...none, level: 'none' }, selection: null }, markup)).not.toContain('level');
	});

	it('drops a rule, a key and a selection of the C# page, and the Markup page ones from the C# page', () => {
		const fromMarkup = decodeHash(realMarkup, '#ZA0002=w&stanza_max_line_length=120&stanza_markup_literals=all&sel=ZA0002', markup);

		expect(fromMarkup.config.rules).toEqual({});
		expect(fromMarkup.config.options).toEqual({});
		expect(fromMarkup.selection).toBeNull();

		const fromCsharp = decodeHash(realCatalog, '#ZA2002=w&stanza_markup_scope=all&stanza_markup_literals=all&sel=ZA2002', csharp);

		expect(fromCsharp.config.rules).toEqual({});
		expect(fromCsharp.config.options).toEqual({ stanza_markup_literals: 'all' });
		expect(fromCsharp.selection).toBeNull();
	});

	it('selects a key of the page by its name', () => {
		expect(decodeHash(realMarkup, '#sel=stanza_markup_enabled', markup).selection).toEqual({ kind: 'option', id: 'stanza_markup_enabled' });
	});

	it('has its own address among the pages, and takes the level only where there is one', () => {
		expect(pages.find((page) => page.id === 'markup')).toEqual({ id: 'markup', name: 'Markup', path: 'markup/' });
		expect(decodeHash(realCatalog, '#level=strong', csharp).config.level).toBe('strong');
		expect(decodeHash(realMarkup, '#level=strong', markup).config.level).toBe('none');
	});
});
