import { describe, expect, it } from 'vitest';

import { importEditorConfig } from '../../src/editorconfig/import.ts';
import { changedCount, checkOption, emptyConfig, replacedBy, type Config } from '../../src/state/config.ts';
import { enabledAt } from '../../src/state/levels.ts';
import type { Catalog, Option, RulePair, Snippet } from '../../src/state/model.ts';
import { generateEditorConfig } from '../../src/writers/editorconfig.ts';
import { generateEslintConfig } from '../../src/writers/eslint-config.ts';
import { generatePyproject } from '../../src/writers/pyproject.ts';
import { realCatalog, realMarkup } from '../fixtures.ts';

function paired(first: string, second: string, native = false): Catalog<Snippet> {
	const pair: RulePair = { first, second, defaultHalf: first, ownerWhenBothEnabled: first };

	return {
		rules: [first, second].map((id, index) => ({
			id,
			title: id,
			group: 'Styles',
			summary: 'Changes layout',
			tier: 'suggestion',
			onByDefault: index === 0,
			level: index === 0 ? 'moderate' : 'opt-in',
			tierText: 'Suggestion',
			descriptionHtml: '',
			examples: [],
			parts: [],
			options: [],
			opposite: index === 0 ? second : first,
			...(native ? { csharp: { csharpier: null, markupOnByDefault: false, readByMarkup: false, undoneByCSharpier: false } } : {}),
			pageUrl: 'https://example.test/' + id,
		})),

		options: [],
		pairs: [pair],
		groups: ['Styles'],
		markupExtensions: [],
		markupProfiles: [],
	};
}

/** Sets one half of a pair at the configuration's severity and the other to `none`, as a file that picks one style does. */
function choose(config: Config, pair: RulePair, half: string): Config {
	const other = half === pair.first ? pair.second : pair.first;
	return { ...config, rules: { ...config.rules, [half]: config.severity, [other]: 'none' } };
}

describe('explicit pair settings across import and export', () => {
	it('counts both halves of an explicit pair choice as changed even when it matches the preset', () => {
		const catalog = paired('ZA0111', 'ZA0112', true);
		const config = choose(emptyConfig(), catalog.pairs[0]!, 'ZA0111');

		expect(changedCount(catalog, config)).toBe(2);
		expect(changedCount(catalog, emptyConfig())).toBe(0);
	});

	it('preserves both enabled halves at preset severity', () => {
		const catalog = paired('ZA0111', 'ZA0112', true);
		const config = { ...emptyConfig(), markupSection: false };
		const source = '[*.cs]\ndotnet_diagnostic.ZA0111.severity = suggestion\ndotnet_diagnostic.ZA0112.severity = suggestion\n';
		const imported = importEditorConfig(catalog, source, config).config;

		expect(imported.rules).toEqual({ ZA0111: 'suggestion', ZA0112: 'suggestion' });
		expect(importEditorConfig(catalog, generateEditorConfig(catalog, imported), config).config.rules).toEqual(imported.rules);
	});

	it('writes and reimports both sides of a C# pair choice', () => {
		const catalog = paired('ZA0111', 'ZA0112', true);
		const config = choose({ ...emptyConfig(), markupSection: false }, catalog.pairs[0]!, 'ZA0112');
		const text = generateEditorConfig(catalog, config);

		expect(text).toContain('dotnet_diagnostic.ZA0111.severity = none');
		expect(text).toContain('dotnet_diagnostic.ZA0112.severity = suggestion');
		expect(importEditorConfig(catalog, text, emptyConfig()).config.rules).toEqual(config.rules);
	});

	it('writes the selected JavaScript variant and an explicit off for its opposite', () => {
		const catalog = paired('separate-multi-line-switch-cases', 'keep-multi-line-switch-cases-together');
		const config = choose(emptyConfig('error'), catalog.pairs[0]!, catalog.pairs[0]!.first);
		const text = generateEslintConfig(catalog, config);

		expect(text).toContain("'stanza/separate-multi-line-switch-cases': 'error'");
		expect(text).toContain("'stanza/keep-multi-line-switch-cases-together': 'off'");
	});

	it('preserves both enabled JavaScript variants even when one matches the preset', () => {
		const catalog = paired('separate-multi-line-switch-cases', 'keep-multi-line-switch-cases-together');

		const config = {
			...emptyConfig('error'),
			rules: { 'separate-multi-line-switch-cases': 'error' as const, 'keep-multi-line-switch-cases-together': 'error' as const },
		};

		const text = generateEslintConfig(catalog, config);

		expect(text).toContain("'stanza/separate-multi-line-switch-cases': 'error'");
		expect(text).toContain("'stanza/keep-multi-line-switch-cases-together': 'error'");
	});

	it('writes enable and disable lists for an explicit Python pair choice', () => {
		const catalog = paired('ZA3111', 'ZA3112');
		const config = choose(emptyConfig('error'), catalog.pairs[0]!, catalog.pairs[0]!.first);
		const text = generatePyproject(catalog, config);

		expect(text).toContain('enable = ["ZA3111"]');
		expect(text).toContain('disable = ["ZA3112"]');
	});
});

describe('native receiver selector validation', () => {
	const receiver: Option<Snippet> = {
		key: 'receiverRoots',
		descriptionHtml: '',
		defaultText: '[]',
		defaultValue: '',
		readByHtml: '',
		kind: 'list',
		choices: [],
		min: null,
		max: null,
		examples: [],
		parts: [],
		rules: [],
		pageUrl: 'https://example.test/settings',
		global: true,
	};

	it('accepts and quotes a JavaScript private receiver name', () => {
		expect(checkOption(receiver, '#db')).toEqual({ ok: true, value: '#db' });
		const catalog = { ...paired('first', 'second'), options: [receiver] };
		expect(generateEslintConfig(catalog, { ...emptyConfig('error'), options: { receiverRoots: '#db' } })).toContain("receiverRoots: ['#db']");
	});

	it('refuses editorconfig comment markers and native control characters', () => {
		const csharp = {
			...receiver,
			key: 'stanza_receiver_roots',
			csharp: { markupDefaultValue: null, markupDefaultByProfile: false, audience: 'analyzer' as const, perFile: false, words: [], values: [] },
		};

		expect(checkOption(csharp, '#db').ok).toBe(false);
		expect(checkOption(receiver, 'db\tcache').ok).toBe(false);
	});
});

const base = (): Config => ({ ...emptyConfig(), markupSection: false });

const activePairs = realCatalog.pairs.filter((pair) => {
	const rule = realCatalog.rules.find((rule) => rule.id === pair.defaultHalf)!;
	return enabledAt(rule, 'moderate', 'stanza-format');
});

describe('real native pair configuration', () => {
	it.each(activePairs)('keeps both lines of a preset-matching choice of $first or $second through export and import', (pair) => {
		const selected = choose(base(), pair, pair.defaultHalf);
		const exported = generateEditorConfig(realCatalog, selected);
		const imported = importEditorConfig(realCatalog, exported, base()).config;

		expect(changedCount(realCatalog, selected)).toBe(2);
		expect(imported.rules).toEqual(selected.rules);
	});

	it('keeps the default switch separator explicit when both styles are enabled', () => {
		const selected = { ...base(), rules: { ZA0111: 'suggestion' as const, ZA0112: 'suggestion' as const } };
		const exported = generateEditorConfig(realCatalog, selected);

		expect(exported).toContain('dotnet_diagnostic.ZA0111.severity = suggestion');
		expect(exported).toContain('dotnet_diagnostic.ZA0112.severity = suggestion');
		expect(importEditorConfig(realCatalog, exported, base()).config.rules).toEqual(selected.rules);
	});

	it('does not apply automatic preset replacement to native C# pairs', () => {
		const separate = realCatalog.rules.find((rule) => rule.id === 'ZA0111')!;
		expect(replacedBy(separate, { ...base(), rules: { ZA0112: 'suggestion' } })).toBeNull();
	});

	it('preserves explicit markup pair choices without changing their native priority', () => {
		const pair = realMarkup.pairs.find((pair) => pair.first === 'ZA2001')!;
		const config: Config = { ...emptyConfig(), rules: { ZA2001: 'suggestion', ZA2002: 'suggestion' } };
		const exported = generateEditorConfig(realMarkup, config);

		expect(importEditorConfig(realMarkup, exported, emptyConfig()).config.rules).toEqual(config.rules);

		expect(
			replacedBy(
				realMarkup.rules.find((rule) => rule.id === pair.first)!,
				config,
			),
		).toBeNull();
	});
});
