import { describe, expect, it } from 'vitest';

import { highlightCatalog } from '../../build/render/highlight.ts';
import { importEditorConfig } from '../../src/editorconfig/import.ts';
import { changedCount, emptyConfig, isRuleChanged } from '../../src/state/config.ts';
import { optionDefaultLabel } from '../../src/state/labels.ts';
import { enabledAt, levels } from '../../src/state/levels.ts';
import { lineText } from '../../src/util/line-text.ts';
import { generateEditorConfig, writtenOptionLines, writtenRuleLines } from '../../src/writers/editorconfig.ts';
import { catalog, config, markupConfig, option, realCatalog, realMarkup, rule } from '../fixtures.ts';

const small = catalog(
	[
		rule('ZA0002'),
		rule('ZA0014', { tier: 'suggestion', onByDefault: false, group: 'Group B', tierText: 'Suggestion, off by default' }),
		rule('ZA0202', { tier: 'suggestion', onByDefault: true, group: 'Group B' }),
		rule('ZA2001', { onByDefault: false, markupOnByDefault: true, readByMarkup: true, group: 'Group B' }),
		rule('ZA2002', { onByDefault: false, readByMarkup: true, group: 'Group B' }),
	],

	[
		option('stanza_max_line_length', { audience: 'both' }),

		option('stanza_named_argument_match', {
			audience: 'analyzer',
			kind: 'enum',
			choices: ['allow', 'require'],
			defaultValue: 'allow',
		}),

		option('stanza_markup_enabled', { audience: 'markup', kind: 'enum', choices: ['true', 'false'], defaultValue: 'true' }),
		option('stanza_doc_max_line_length', { audience: 'both', defaultValue: null, min: 0, max: 1000 }),
	],
);

describe('the lines a preview shows', () => {
	const [zero, three, , markupRule] = small.rules;
	const [lineLength, , markupOnly, docLineLength] = small.options;

	it('are none while every setting is at its default', () => {
		expect(writtenRuleLines(zero!, emptyConfig())).toEqual([]);
		expect(writtenOptionLines(lineLength!, emptyConfig())).toEqual([]);
	});

	it('are the line the generator writes for a chosen rule or option', () => {
		const chosen = config({ rules: { ZA0014: 'warning' }, options: { stanza_max_line_length: '120' } });

		expect(writtenRuleLines(three!, chosen)).toEqual(['dotnet_diagnostic.ZA0014.severity = warning']);
		expect(writtenOptionLines(lineLength!, chosen)).toEqual(['stanza_max_line_length = 120']);
	});

	it('are the line that Every setting adds, a comment for a rule the level leaves off', () => {
		const every = config({ everySetting: true, markupSection: false });

		expect(writtenRuleLines(zero!, every)).toEqual(['dotnet_diagnostic.ZA0002.severity = suggestion']);
		expect(writtenRuleLines(three!, every)).toEqual(['# dotnet_diagnostic.ZA0014.severity = suggestion']);
		expect(writtenOptionLines(lineLength!, every)).toEqual(['stanza_max_line_length = 100']);
		expect(writtenOptionLines(docLineLength!, every)).toEqual([]);
	});

	it('hold the line of each section, as a rule stanza-markup applies by default is written twice', () => {
		const every = config({ everySetting: true });

		expect(writtenRuleLines(markupRule!, every)).toEqual([
			'# dotnet_diagnostic.ZA2001.severity = suggestion',
			'dotnet_diagnostic.ZA2001.severity = suggestion',
		]);

		expect(writtenRuleLines(markupRule!, { ...every, markupSection: false })).toEqual(['# dotnet_diagnostic.ZA2001.severity = suggestion']);
	});

	it('leave out an option only the markup section writes while that section is off', () => {
		expect(writtenOptionLines(markupOnly!, config({ everySetting: true }))).toEqual(['stanza_markup_enabled = true']);
		expect(writtenOptionLines(markupOnly!, config({ everySetting: true, markupSection: false }))).toEqual([]);
	});
});

const nothing = '# Every rule and option is what Stanza applies without a line, so nothing is written.\n';

describe('the generated .editorconfig', () => {
	it('writes nothing but a note while every setting is what the level and the defaults give', () => {
		expect(generateEditorConfig(small, emptyConfig())).toBe(nothing);
	});

	it('writes a rule raised above the severity of the level, and none that says what the level gives', () => {
		expect(generateEditorConfig(small, config({ rules: { ZA0002: 'warning' }, markupSection: false }))).toBe(
			'[*.cs]\ndotnet_diagnostic.ZA0002.severity = warning\n',
		);

		expect(generateEditorConfig(small, config({ rules: { ZA0002: 'suggestion', ZA0014: 'none' }, markupSection: false }))).toBe(nothing);
	});

	it('writes a rule that differs, and the line that enables an off-by-default rule', () => {
		const text = generateEditorConfig(small, config({ rules: { ZA0002: 'none', ZA0014: 'warning', ZA0202: 'warning' }, markupSection: false }));

		expect(text).toBe(
			[
				'[*.cs]',
				'dotnet_diagnostic.ZA0002.severity = none',
				'dotnet_diagnostic.ZA0014.severity = warning',
				'dotnet_diagnostic.ZA0202.severity = warning',
				'',
			].join('\n'),
		);
	});

	it('writes the severity an off-by-default rule ships at, because that line enables it', () => {
		expect(generateEditorConfig(small, config({ rules: { ZA0014: 'suggestion' }, markupSection: false }))).toContain(
			'dotnet_diagnostic.ZA0014.severity = suggestion',
		);
	});

	it('writes silent, which enables a rule without reporting it', () => {
		expect(generateEditorConfig(small, config({ rules: { ZA0014: 'silent' }, markupSection: false }))).toContain('ZA0014.severity = silent');
	});

	it('writes an option only when its value differs from the default', () => {
		expect(generateEditorConfig(small, config({ options: { stanza_max_line_length: '100' }, markupSection: false }))).toBe(nothing);

		expect(
			generateEditorConfig(
				small,
				config({ options: { stanza_max_line_length: '120', stanza_named_argument_match: 'require' }, markupSection: false }),
			),
		).toBe(['[*.cs]', 'stanza_max_line_length = 120', 'stanza_named_argument_match = require', ''].join('\n'));
	});

	it('keeps a markup-only key out of the [*.cs] section', () => {
		const text = generateEditorConfig(small, config({ options: { stanza_markup_enabled: 'false' } }));
		expect(text).toBe(['[*.{xml,csproj}]', '# The section stanza-markup reads for markup files.', 'stanza_markup_enabled = false', ''].join('\n'));
	});

	it('writes a rule stanza-markup reads, and a shared key, into the markup section too', () => {
		const text = generateEditorConfig(
			small,
			config({ rules: { ZA2002: 'warning', ZA0002: 'none' }, options: { stanza_max_line_length: '120', stanza_named_argument_match: 'require' } }),
		);

		expect(text).toBe(
			[
				'[*.cs]',
				'dotnet_diagnostic.ZA0002.severity = none',
				'dotnet_diagnostic.ZA2002.severity = warning',
				'',
				'stanza_max_line_length = 120',
				'stanza_named_argument_match = require',
				'',
				'[*.{xml,csproj}]',
				'# The section stanza-markup reads for markup files.',
				'dotnet_diagnostic.ZA2002.severity = warning',
				'',
				'stanza_max_line_length = 120',
				'',
			].join('\n'),
		);
	});

	it('leaves the markup section out when the configuration asks for none', () => {
		expect(generateEditorConfig(small, config({ rules: { ZA0002: 'none' }, markupSection: false }))).toBe(
			['[*.cs]', 'dotnet_diagnostic.ZA0002.severity = none', ''].join('\n'),
		);
	});

	it('says so at the top when a changed setting goes unwritten because the markup section is off', () => {
		const note = '# stanza-markup settings are left out because Markup section is off.';

		expect(generateEditorConfig(small, config({ options: { stanza_markup_enabled: 'false' }, markupSection: false }))).toBe(`${note}\n`);

		expect(
			generateEditorConfig(small, config({ rules: { ZA0002: 'none' }, options: { stanza_markup_enabled: 'false' }, markupSection: false })),
		).toBe([note, '', '[*.cs]', 'dotnet_diagnostic.ZA0002.severity = none', ''].join('\n'));

		expect(generateEditorConfig(small, config({ options: { stanza_markup_enabled: 'false' } }))).not.toContain(note);
	});

	it('does not say so for a key the [*.cs] section still writes', () => {
		expect(generateEditorConfig(small, config({ options: { stanza_max_line_length: '120' }, markupSection: false }))).not.toContain('left out');
	});

	describe('with every setting', () => {
		const text = generateEditorConfig(small, config({ everySetting: true, rules: { ZA0014: 'warning' } }));
		const lines = text.split('\n');

		it('writes a rule of the level at the severity of the level', () => {
			expect(lines).toContain('dotnet_diagnostic.ZA0002.severity = suggestion');
			expect(lines).toContain('dotnet_diagnostic.ZA0202.severity = suggestion');
		});

		it('writes a rule the level leaves off as a comment, because a severity would enable it', () => {
			expect(lines).toContain('# dotnet_diagnostic.ZA2002.severity = suggestion');
			expect(lines).not.toContain('dotnet_diagnostic.ZA2002.severity = suggestion');
		});

		it('writes a chosen rule as an active line', () => {
			expect(lines).toContain('dotnet_diagnostic.ZA0014.severity = warning');
		});

		it('writes the default of an option that has a literal one, and skips one that does not', () => {
			expect(lines).toContain('stanza_max_line_length = 100');
			expect(lines).toContain('stanza_named_argument_match = allow');
			expect(text).not.toContain('stanza_doc_max_line_length');
		});

		it('names the group of each run of rules in the [*.cs] section only', () => {
			const cs = text.slice(0, text.indexOf('[*.{'));
			const markup = text.slice(text.indexOf('[*.{'));

			expect(cs).toContain('# Group A.');
			expect(cs).toContain('# Group B.');
			expect(markup).not.toContain('# Group');
		});

		it('writes a rule stanza-markup applies by default as an active line in its own section only', () => {
			const markup = text.slice(text.indexOf('[*.{'));

			expect(markup).toContain('\ndotnet_diagnostic.ZA2001.severity = suggestion');
			expect(text.slice(0, text.indexOf('[*.{'))).toContain('# dotnet_diagnostic.ZA2001.severity = suggestion');
		});
	});
});

describe('the generated .editorconfig for the real rules', () => {
	it('writes a changed rule and a changed key to the C# section and the section of the markup files', () => {
		const text = generateEditorConfig(
			realCatalog,
			config({ rules: { ZA0014: 'warning', ZA0002: 'none' }, options: { stanza_max_line_length: '120' } }),
		);

		expect(text).toBe(
			[
				'[*.cs]',
				'dotnet_diagnostic.ZA0002.severity = none',
				'dotnet_diagnostic.ZA0014.severity = warning',
				'',
				'stanza_max_line_length = 120',
				'',
				`[*.{${realCatalog.markupExtensions.join(',')}}]`,
				'# The section stanza-markup reads for markup files.',
				'dotnet_diagnostic.ZA0909.severity = suggestion',
				'',
				'stanza_max_line_length = 120',
				'',
			].join('\n'),
		);
	});

	it('lists every rule exactly once in the [*.cs] section when it writes every setting', () => {
		const text = generateEditorConfig(realCatalog, config({ everySetting: true }));
		const cs = text.slice(0, text.indexOf('\n[*.{'));
		const ids = [...cs.matchAll(/dotnet_diagnostic\.(ZA\d{4})\.severity/g)].map((match) => match[1]);

		expect(ids).toHaveLength(realCatalog.rules.length);
		expect(new Set(ids).size).toBe(realCatalog.rules.length);
	});

	it('writes no rule the level leaves off as an active line of [*.cs] while the level is the default', () => {
		const text = generateEditorConfig(realCatalog, config({ everySetting: true }));
		const cs = text.slice(0, text.indexOf('\n[*.{')).split('\n');

		for (const rule of realCatalog.rules.filter((candidate) => !candidate.onByDefault)) {
			expect(cs).not.toContain(`dotnet_diagnostic.${rule.id}.severity = suggestion`);
			expect(cs).toContain(`# dotnet_diagnostic.${rule.id}.severity = suggestion`);
		}
	});
});

describe('the .editorconfig of a level', () => {
	const analyzerPart = (text: string): string => (text.includes('[*.{') ? text.slice(0, text.indexOf('[*.{')) : text);
	const markupPart = (text: string): string => (text.includes('[*.{') ? text.slice(text.indexOf('[*.{')) : '');

	const ruleIds = (part: string, active = true): string[] =>
		[...part.matchAll(active ? /^dotnet_diagnostic\.(ZA\d{4})\.severity/gm : /^# dotnet_diagnostic\.(ZA\d{4})\.severity/gm)].map(
			(match) => match[1]!,
		);

	it('writes no rule line for a level the package applies, but the markup rules of the level that stanza-markup does not apply by itself', () => {
		const text = generateEditorConfig(realCatalog, config({ level: 'strong', severity: 'warning' }));

		const markupIds = realCatalog.rules
			.filter((rule) => rule.csharp!.readByMarkup && rule.level !== 'opt-in' && !rule.csharp!.markupOnByDefault)
			.map((rule) => rule.id);

		expect(ruleIds(analyzerPart(text))).toEqual([]);
		expect(ruleIds(markupPart(text))).toEqual(markupIds);
		expect(markupPart(text)).toContain('dotnet_diagnostic.ZA0903.severity = warning');
		expect(markupIds).toContain('ZA0903');
		expect(markupPart(text)).not.toContain('ZA2002');
	});

	it('writes no severity for a rule stanza-markup applies by default, since the tool tells only on from off', () => {
		for (const severity of ['warning', 'error'] as const) {
			const text = generateEditorConfig(small, config({ level: 'strong', severity }));
			expect(markupPart(text), severity).not.toContain('ZA2001');
		}

		// Raising such a rule changes the analyzer's line and leaves the tool's section alone.
		const raised = generateEditorConfig(small, config({ level: 'strong', rules: { ZA2001: 'error' } }));

		expect(markupPart(raised)).not.toContain('ZA2001');
		expect(analyzerPart(raised)).toContain('dotnet_diagnostic.ZA2001.severity = error');
	});

	it('writes the changes against the level in the section of the analyzer', () => {
		const text = generateEditorConfig(
			realCatalog,
			config({ level: 'strong', rules: { ZA0003: 'none', ZA0054: 'warning', ZA0001: 'suggestion' }, markupSection: false }),
		);

		expect(text).toBe(['[*.cs]', 'dotnet_diagnostic.ZA0003.severity = none', 'dotnet_diagnostic.ZA0054.severity = warning', ''].join('\n'));
	});

	it('keeps the rules that stanza-markup applies by default, since no level turns them off', () => {
		for (const level of ['minimal', 'none'] as const) {
			expect(generateEditorConfig(small, config({ level })), level).not.toContain('ZA2001');
		}

		// A line of the visitor can turn one off in the tool, and the tool alone writes it.
		const off = generateEditorConfig(small, config({ level: 'minimal', rules: { ZA2001: 'none' } }));

		expect(analyzerPart(off)).not.toContain('ZA2001');
		expect(markupPart(off)).toContain('dotnet_diagnostic.ZA2001.severity = none');
	});

	it('writes nothing at the minimal level, because the tool has no shared rule to repeat', () => {
		expect(generateEditorConfig(realCatalog, config({ level: 'minimal' }))).toBe(nothing);
	});

	it('writes every rule of the level at its severity when it writes every setting, and the others as comments', () => {
		const text = generateEditorConfig(realCatalog, config({ level: 'strong', severity: 'error', everySetting: true, markupSection: false }));
		const on = realCatalog.rules.filter((rule) => enabledAt(rule, 'strong', 'stanza-format')).map((rule) => rule.id);

		expect(ruleIds(text).sort()).toEqual(on.sort());
		expect(text).toContain('dotnet_diagnostic.ZA0001.severity = error');
		expect(text).toContain('# dotnet_diagnostic.ZA0054.severity = error');
		expect(text).not.toContain('severity = none');
	});

	it('writes the rules of a lower level as none, which the package would otherwise leave on', () => {
		const text = generateEditorConfig(realCatalog, config({ level: 'minimal', everySetting: true, markupSection: false }));
		const minimal = realCatalog.rules.filter((rule) => rule.level === 'minimal');
		const shipped = realCatalog.rules.filter((rule) => rule.onByDefault && rule.level !== 'minimal');

		expect(ruleIds(text).sort()).toEqual([...minimal, ...shipped].map((rule) => rule.id).sort());

		for (const rule of shipped) {
			expect(text).toContain(`dotnet_diagnostic.${rule.id}.severity = none`);
		}

		for (const rule of minimal) {
			expect(text).toContain(`dotnet_diagnostic.${rule.id}.severity = suggestion`);
		}
	});

	it('writes a setting of the visitor as none even where the rule is off, so it wins over a package level', () => {
		const text = generateEditorConfig(
			realCatalog,
			config({ level: 'strong', everySetting: true, markupSection: false, rules: { ZA0054: 'none', ZA0003: 'none' } }),
		);

		expect(text).toContain('dotnet_diagnostic.ZA0054.severity = none');
		expect(text).toContain('dotnet_diagnostic.ZA0003.severity = none');
	});

	it('writes the csharpier formatter as the overlay does at the strong level', () => {
		const text = generateEditorConfig(realCatalog, config({ level: 'strong', formatter: 'csharpier', everySetting: true, markupSection: false }));

		expect(text).toContain('# dotnet_diagnostic.ZA0012.severity = suggestion');
		expect(text).toContain('dotnet_diagnostic.ZA0142.severity = suggestion');
		expect(text).toContain('# dotnet_diagnostic.ZA0141.severity = suggestion');
		expect(text).not.toContain('\ndotnet_diagnostic.ZA0141.severity');
	});

	it('writes the section of stanza-markup from the level whether or not the rest is expanded', () => {
		const expanded = generateEditorConfig(realCatalog, config({ level: 'strong', everySetting: true }));
		const delta = generateEditorConfig(realCatalog, config({ level: 'strong' }));

		for (const rule of realCatalog.rules.filter((candidate) => candidate.csharp!.readByMarkup && candidate.level !== 'opt-in')) {
			const line = `dotnet_diagnostic.${rule.id}.severity = suggestion`;

			// A rule the tool applies by default at that severity needs no line of its own, until the file is expanded.
			if (!rule.csharp!.markupOnByDefault) {
				expect(markupPart(delta), rule.id).toContain(line);
			}

			expect(markupPart(expanded), rule.id).toContain(line);
		}
	});

	it('gives an expanded file that imports as no change, for each level', () => {
		for (const level of levels) {
			for (const every of [false, true]) {
				const picks = config({ level, severity: 'warning', formatter: level === 'strong' ? 'csharpier' : 'stanza-format', everySetting: every });
				const result = importEditorConfig(realCatalog, generateEditorConfig(realCatalog, picks), picks);

				expect(result.refused, `${level} ${every}`).toBeNull();
				expect(result.skipped, `${level} ${every}`).toEqual([]);
				expect(result.config.rules, `${level} ${every}`).toEqual({});
				expect(realCatalog.rules.filter((rule) => isRuleChanged(rule, result.config))).toEqual([]);
			}
		}
	});

	it('gives the same delta back from the file it writes', () => {
		const picks = config({
			level: 'strong',
			rules: { ZA0003: 'none', ZA0054: 'warning', ZA0909: 'none', ZA0903: 'error' },
			options: { stanza_max_line_length: '120' },
		});

		for (const every of [false, true]) {
			const written = { ...picks, everySetting: every };
			const result = importEditorConfig(realCatalog, generateEditorConfig(realCatalog, written), { ...emptyConfig(), level: 'strong' });

			expect(result.config.rules, String(every)).toEqual(picks.rules);
			expect(result.config.options.stanza_max_line_length, String(every)).toBe('120');
		}
	});
});

describe('the .editorconfig of the Markup page', () => {
	const analyzerPart = (text: string): string => (text.includes('\n[*.{') ? text.slice(0, text.indexOf('\n[*.{')) : text);
	const markupPart = (text: string): string => (text.includes('[*.{') ? text.slice(text.indexOf('[*.{')) : '');

	const idsOf = (part: string, prefix = ''): string[] =>
		[...part.matchAll(new RegExp(`^${prefix}dotnet_diagnostic\\.(ZA\\d{4})\\.severity`, 'gm'))].map((match) => match[1]!);

	const on = ['ZA2001', 'ZA2006', 'ZA2007'];

	it('holds the ten markup rules and the six keys of stanza-markup, and no other rule or key', () => {
		expect(realMarkup.rules.map((rule) => rule.id)).toEqual(Array.from({ length: 10 }, (_, index) => `ZA${2001 + index}`));

		expect(realMarkup.options.map((option) => option.key)).toEqual([
			'stanza_markup_doc_reprint',
			'stanza_markup_enabled',
			'stanza_markup_max_blank_lines',
			'stanza_markup_profile',
			'stanza_markup_scope',
			'stanza_markup_separated_elements',
		]);
	});

	it('gives every rule no level, and the pairs the C# page models', () => {
		expect(realMarkup.rules.every((rule) => rule.level === 'no-level' && rule.csharp!.csharpier === null && !rule.onByDefault)).toBe(true);
		expect(realMarkup.rules.filter((rule) => rule.csharp!.markupOnByDefault).map((rule) => rule.id)).toEqual(on);

		expect(realMarkup.pairs.map((pair) => [pair.first, pair.second])).toEqual([
			['ZA2001', 'ZA2002'],
			['ZA2008', 'ZA2009'],
		]);
	});

	it('writes the rules the tool applies by default as severity lines for the analyzer, which no level enables', () => {
		const text = generateEditorConfig(realMarkup, markupConfig({}));
		expect(text).toBe(['[*.cs]', ...on.map((id) => `dotnet_diagnostic.${id}.severity = suggestion`), ''].join('\n'));
	});

	it('writes a rule that is turned on in both sections, and one turned off as none in the tool alone', () => {
		const text = generateEditorConfig(realMarkup, markupConfig({ rules: { ZA2002: 'suggestion', ZA2001: 'none' } }));

		expect(idsOf(analyzerPart(text))).toEqual(['ZA2002', 'ZA2006', 'ZA2007']);
		expect(markupPart(text)).toContain('dotnet_diagnostic.ZA2001.severity = none');
		expect(markupPart(text)).toContain('dotnet_diagnostic.ZA2002.severity = suggestion');
		expect(analyzerPart(text)).not.toContain('ZA2001');
	});

	it('writes a raised severity in the section of the analyzer, and a rule the tool applies by default once without it in the tool', () => {
		const text = generateEditorConfig(realMarkup, markupConfig({ rules: { ZA2001: 'error', ZA2005: 'warning' } }));

		expect(analyzerPart(text)).toContain('dotnet_diagnostic.ZA2001.severity = error');
		expect(markupPart(text)).not.toContain('ZA2001');
		expect(markupPart(text)).toContain('dotnet_diagnostic.ZA2005.severity = warning');
	});

	it('writes a rule the visitor sets to the default as it writes the default', () => {
		const set = generateEditorConfig(realMarkup, markupConfig({ rules: { ZA2001: 'suggestion', ZA2002: 'none' } }));
		expect(set).toBe(generateEditorConfig(realMarkup, markupConfig({})));
	});

	it('writes every rule under every setting: on at its severity, off as a comment, and none where the tool applies it', () => {
		const text = generateEditorConfig(realMarkup, markupConfig({ everySetting: true, rules: { ZA2006: 'none' } }));

		expect(idsOf(analyzerPart(text))).toEqual(['ZA2001', 'ZA2006', 'ZA2007']);
		expect(idsOf(analyzerPart(text), '# ')).toEqual(['ZA2002', 'ZA2003', 'ZA2004', 'ZA2005', 'ZA2008', 'ZA2009', 'ZA2010']);
		expect(analyzerPart(text)).toContain('dotnet_diagnostic.ZA2006.severity = none');
		expect(markupPart(text)).toContain('dotnet_diagnostic.ZA2006.severity = none');
		expect(markupPart(text)).toContain('# dotnet_diagnostic.ZA2002.severity = suggestion');
	});

	it('leaves the markup section out when the checkbox is off, and says so for a key only the tool reads', () => {
		const text = generateEditorConfig(realMarkup, markupConfig({ markupSection: false, options: { stanza_markup_enabled: 'false' } }));

		expect(text.startsWith('# stanza-markup settings are left out because Markup section is off.\n')).toBe(true);
		expect(text).not.toContain('[*.{');
		expect(text).toContain('[*.cs]');
	});

	it('writes a key whose default differs in the tool, the way the C# page did', () => {
		const scope = realMarkup.options.find((entry) => entry.key === 'stanza_markup_scope')!;

		expect(optionDefaultLabel(scope)).toBe('Default (all; per profile)');

		const every = generateEditorConfig(realMarkup, markupConfig({ everySetting: true }));

		expect(analyzerPart(every)).toContain('stanza_markup_scope = all');
		expect(markupPart(every)).not.toContain('stanza_markup_scope');

		const explicit = markupConfig({ options: { stanza_markup_scope: 'all' } });
		const written = generateEditorConfig(realMarkup, explicit);

		expect(markupPart(written)).toContain('stanza_markup_scope = all');
		expect(analyzerPart(written)).not.toContain('stanza_markup_scope');
		expect(changedCount(realMarkup, explicit)).toBe(1);

		const containers = generateEditorConfig(realMarkup, markupConfig({ options: { stanza_markup_scope: 'containers' } }));

		expect(analyzerPart(containers)).toContain('stanza_markup_scope = containers');
		expect(markupPart(containers)).toContain('stanza_markup_scope = containers');
	});

	it('warns above a profile that every file the section matches would take it', () => {
		const text = generateEditorConfig(realMarkup, markupConfig({ options: { stanza_markup_profile: 'msbuild' } }));
		const lines = markupPart(text).split('\n');
		const at = lines.indexOf('stanza_markup_profile = msbuild');

		expect(at).toBeGreaterThan(0);
		expect(lines[at - 1]).toBe('# stanza_markup_profile applies to every file this section matches, so narrow the glob to the files that need it.');
	});

	it('shows the lines of a rule as the file holds them', () => {
		const [first, second] = realMarkup.rules;

		expect(writtenRuleLines(first!, markupConfig({}))).toEqual(['dotnet_diagnostic.ZA2001.severity = suggestion']);
		expect(writtenRuleLines(second!, markupConfig({}))).toEqual([]);
		expect(writtenRuleLines(first!, markupConfig({ rules: { ZA2001: 'none' } }))).toEqual(['dotnet_diagnostic.ZA2001.severity = none']);
	});

	it('gives a file that imports as no change, with every setting and without', () => {
		for (const everySetting of [false, true]) {
			for (const markupSection of [true, false]) {
				const picks = markupConfig({ everySetting, markupSection });
				const back = importEditorConfig(realMarkup, generateEditorConfig(realMarkup, picks), picks);

				expect(back.skipped, `${everySetting} ${markupSection}`).toEqual([]);
				expect(back.config.rules).toEqual({});
				expect(back.config.options).toEqual({});
				expect(realMarkup.rules.filter((rule) => isRuleChanged(rule, back.config))).toEqual([]);
			}
		}
	});

	it('gives back the rules and keys the visitor set', () => {
		const picks = markupConfig({
			rules: { ZA2001: 'none', ZA2002: 'warning', ZA2005: 'suggestion', ZA2010: 'error' },
			options: { stanza_markup_scope: 'containers', stanza_markup_max_blank_lines: '2', stanza_markup_enabled: 'false' },
		});

		for (const everySetting of [false, true]) {
			const back = importEditorConfig(realMarkup, generateEditorConfig(realMarkup, { ...picks, everySetting }), markupConfig({}));

			expect(back.skipped).toEqual([]);
			expect(back.config.rules).toEqual(picks.rules);
			expect(back.config.options).toEqual(picks.options);
		}
	});
});

describe('highlighting', () => {
	it('keeps the text of every line in its markup, so the diff compares what the page wrote', async () => {
		const { catalog: highlighted, css } = await highlightCatalog(realCatalog);

		for (const [index, rule] of highlighted.rules.entries()) {
			for (const [number, example] of rule.examples.entries()) {
				const written = realCatalog.rules[index]!.examples[number]!;

				const pairs = [
					[example.before, written.before],
					...example.afters.map((after, at) => [after.snippet, written.afters[at]!.snippet] as const),
				] as const;

				for (const [snippet, source] of pairs) {
					const lines = source.code.split('\n');

					expect(
						snippet.lines.map((line) => lineText(line.html)),
						rule.id,
					).toEqual(lines.slice(0, snippet.lines.length));
				}
			}
		}

		expect(css).toContain('.t0{color:');
	}, 60_000);
});
