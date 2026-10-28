import { describe, expect, it } from 'vitest';
import { type Config, changedCount, emptyConfig } from '../src/config.ts';
import { generateEditorConfig, writtenOptionLines, writtenRuleLines } from '../src/editorconfig.ts';
import type { SiteCatalog } from '../src/model.ts';
import { optionDefaultLabel } from '../src/ui/labels.ts';
import { catalog, option, realCatalog, rule } from './fixtures.ts';
import { highlightCatalog } from '../build/highlight.ts';

const small = catalog(
	[
		rule('ZA0001'),
		rule('ZA0003', { tier: 'suggestion', onByDefault: false, group: 'Group B', tierText: 'Suggestion, off by default' }),
		rule('ZA0035', { tier: 'suggestion', onByDefault: true, group: 'Group B' }),
		rule('ZA0101', { onByDefault: false, markupOnByDefault: true, readByMarkup: true, group: 'Group B' }),
		rule('ZA0102', { onByDefault: false, readByMarkup: true, group: 'Group B' }),
	],
	[
		option('stanza_max_line_length', { audience: 'both' }),

		option('stanza_named_argument_match', {
			audience: 'analyzer',
			kind: 'enum',
			choices: ['allow', 'require'],
			defaultValue: 'allow',
			min: null,
			max: null,
		}),

		option('stanza_markup_enabled', { audience: 'markup', kind: 'enum', choices: ['true', 'false'], defaultValue: 'true', min: null, max: null }),
		option('stanza_tab_width', { audience: 'both', defaultValue: null, min: 1, max: 64 }),
	],
);

const config = (changes: Partial<Config>): Config => ({ ...emptyConfig(), ...changes });

describe('the lines a preview shows', () => {
	const [zero, three, , markupRule] = small.rules;
	const [lineLength, , markupOnly, tabWidth] = small.options;

	it('are none while every setting is at its default', () => {
		expect(writtenRuleLines(zero!, emptyConfig())).toEqual([]);
		expect(writtenOptionLines(lineLength!, emptyConfig())).toEqual([]);
	});

	it('are the line the generator writes for a chosen rule or option', () => {
		const chosen = config({ rules: { ZA0003: 'warning' }, options: { stanza_max_line_length: '120' } });

		expect(writtenRuleLines(three!, chosen)).toEqual(['dotnet_diagnostic.ZA0003.severity = warning']);
		expect(writtenOptionLines(lineLength!, chosen)).toEqual(['stanza_max_line_length = 120']);
	});

	it('are the line that Every setting adds, a comment for a rule that is off by default', () => {
		const every = config({ everySetting: true, markupSection: false });

		expect(writtenRuleLines(zero!, every)).toEqual(['dotnet_diagnostic.ZA0001.severity = warning']);
		expect(writtenRuleLines(three!, every)).toEqual(['# dotnet_diagnostic.ZA0003.severity = suggestion']);
		expect(writtenOptionLines(lineLength!, every)).toEqual(['stanza_max_line_length = 100']);
		expect(writtenOptionLines(tabWidth!, every)).toEqual([]);
	});

	it('hold the line of each section, as a rule stanza-markup applies by default is written twice', () => {
		const every = config({ everySetting: true });

		expect(writtenRuleLines(markupRule!, every)).toEqual([
			'# dotnet_diagnostic.ZA0101.severity = warning',
			'dotnet_diagnostic.ZA0101.severity = warning',
		]);

		expect(writtenRuleLines(markupRule!, { ...every, markupSection: false })).toEqual(['# dotnet_diagnostic.ZA0101.severity = warning']);
	});

	it('leave out an option only the markup section writes while that section is off', () => {
		expect(writtenOptionLines(markupOnly!, config({ everySetting: true }))).toEqual(['stanza_markup_enabled = true']);
		expect(writtenOptionLines(markupOnly!, config({ everySetting: true, markupSection: false }))).toEqual([]);
	});
});

describe('the generated .editorconfig', () => {
	it('writes nothing but a note while every setting is at its default', () => {
		expect(generateEditorConfig(small, emptyConfig())).toBe('# Every rule and option is at its default, so nothing is written.\n');
	});

	it('writes a rule set to the severity it already ships at, because the line decides who owns a pair gap', () => {
		expect(generateEditorConfig(small, config({ rules: { ZA0001: 'warning' }, markupSection: false }))).toBe(
			'[*.cs]\ndotnet_diagnostic.ZA0001.severity = warning\n',
		);
	});

	it('writes a rule that differs, and the line that enables an off-by-default rule', () => {
		const text = generateEditorConfig(small, config({ rules: { ZA0001: 'none', ZA0003: 'warning', ZA0035: 'warning' }, markupSection: false }));

		expect(text).toBe(
			[
				'[*.cs]',
				'dotnet_diagnostic.ZA0001.severity = none',
				'dotnet_diagnostic.ZA0003.severity = warning',
				'dotnet_diagnostic.ZA0035.severity = warning',
				'',
			].join('\n'),
		);
	});

	it('writes the severity an off-by-default rule ships at, because that line enables it', () => {
		expect(generateEditorConfig(small, config({ rules: { ZA0003: 'suggestion' }, markupSection: false }))).toContain(
			'dotnet_diagnostic.ZA0003.severity = suggestion',
		);
	});

	it('writes silent, which enables a rule without reporting it', () => {
		expect(generateEditorConfig(small, config({ rules: { ZA0003: 'silent' }, markupSection: false }))).toContain('ZA0003.severity = silent');
	});

	it('writes an option only when its value differs from the default', () => {
		expect(generateEditorConfig(small, config({ options: { stanza_max_line_length: '100' }, markupSection: false }))).toBe(
			'# Every rule and option is at its default, so nothing is written.\n',
		);

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
			config({ rules: { ZA0102: 'warning', ZA0001: 'none' }, options: { stanza_max_line_length: '120', stanza_named_argument_match: 'require' } }),
		);

		expect(text).toBe(
			[
				'[*.cs]',
				'dotnet_diagnostic.ZA0001.severity = none',
				'dotnet_diagnostic.ZA0102.severity = warning',
				'',
				'stanza_max_line_length = 120',
				'stanza_named_argument_match = require',
				'',
				'[*.{xml,csproj}]',
				'# The section stanza-markup reads for markup files.',
				'dotnet_diagnostic.ZA0102.severity = warning',
				'',
				'stanza_max_line_length = 120',
				'',
			].join('\n'),
		);
	});

	it('leaves the markup section out when the configuration asks for none', () => {
		expect(generateEditorConfig(small, config({ rules: { ZA0001: 'none' }, markupSection: false }))).toBe(
			['[*.cs]', 'dotnet_diagnostic.ZA0001.severity = none', ''].join('\n'),
		);
	});

	it('says so at the top when a changed setting goes unwritten because the markup section is off', () => {
		const note = '# stanza-markup settings are left out because Markup section is off.';

		expect(generateEditorConfig(small, config({ options: { stanza_markup_enabled: 'false' }, markupSection: false }))).toBe(`${note}\n`);

		expect(
			generateEditorConfig(small, config({ rules: { ZA0001: 'none' }, options: { stanza_markup_enabled: 'false' }, markupSection: false })),
		).toBe([note, '', '[*.cs]', 'dotnet_diagnostic.ZA0001.severity = none', ''].join('\n'));

		expect(generateEditorConfig(small, config({ options: { stanza_markup_enabled: 'false' } }))).not.toContain(note);
	});

	it('does not say so for a key the [*.cs] section still writes', () => {
		expect(generateEditorConfig(small, config({ options: { stanza_max_line_length: '120' }, markupSection: false }))).not.toContain('left out');
	});

	describe('with every setting', () => {
		const text = generateEditorConfig(small, config({ everySetting: true, rules: { ZA0003: 'warning' } }));
		const lines = text.split('\n');

		it('writes a rule that is on by default at its own severity', () => {
			expect(lines).toContain('dotnet_diagnostic.ZA0001.severity = warning');
			expect(lines).toContain('dotnet_diagnostic.ZA0035.severity = suggestion');
		});

		it('writes a rule that is off by default as a comment, because a severity would enable it', () => {
			expect(lines).toContain('# dotnet_diagnostic.ZA0102.severity = warning');
			expect(lines).not.toContain('dotnet_diagnostic.ZA0102.severity = warning');
		});

		it('writes a chosen rule as an active line', () => {
			expect(lines).toContain('dotnet_diagnostic.ZA0003.severity = warning');
		});

		it('writes the default of an option that has a literal one, and skips one that does not', () => {
			expect(lines).toContain('stanza_max_line_length = 100');
			expect(lines).toContain('stanza_named_argument_match = allow');
			expect(text).not.toContain('stanza_tab_width');
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

			expect(markup).toContain('\ndotnet_diagnostic.ZA0101.severity = warning');
			expect(text.slice(0, text.indexOf('[*.{'))).toContain('# dotnet_diagnostic.ZA0101.severity = warning');
		});
	});
});

describe('the generated .editorconfig for the real rules', () => {
	const real = realCatalog as unknown as SiteCatalog;

	it('writes the sample the docs describe', () => {
		const text = generateEditorConfig(real, config({ rules: { ZA0003: 'warning', ZA0001: 'none' }, options: { stanza_max_line_length: '120' } }));

		expect(text).toBe(
			[
				'[*.cs]',
				'dotnet_diagnostic.ZA0001.severity = none',
				'dotnet_diagnostic.ZA0003.severity = warning',
				'',
				'stanza_max_line_length = 120',
				'',
				`[*.{${real.markupExtensions.join(',')}}]`,
				'# The section stanza-markup reads for markup files.',
				'stanza_max_line_length = 120',
				'',
			].join('\n'),
		);
	});

	it('lists every rule exactly once in the [*.cs] section when it writes every setting', () => {
		const text = generateEditorConfig(real, config({ everySetting: true }));
		const cs = text.slice(0, text.indexOf('\n[*.{'));
		const ids = [...cs.matchAll(/dotnet_diagnostic\.(ZA\d{4})\.severity/g)].map((match) => match[1]);

		expect(ids).toHaveLength(real.rules.length);
		expect(new Set(ids).size).toBe(real.rules.length);
	});

	it('writes no off-by-default rule as an active line of [*.cs] when every setting is at its default', () => {
		const text = generateEditorConfig(real, config({ everySetting: true }));
		const cs = text.slice(0, text.indexOf('\n[*.{')).split('\n');

		for (const rule of real.rules.filter((candidate) => !candidate.onByDefault)) {
			expect(cs).not.toContain(`dotnet_diagnostic.${rule.id}.severity = ${rule.tier}`);
			expect(cs).toContain(`# dotnet_diagnostic.${rule.id}.severity = ${rule.tier}`);
		}
	});

	it('writes a rule that only stanza-markup applies by default as an active line of the markup section', () => {
		const text = generateEditorConfig(real, config({ everySetting: true }));
		const markup = text.slice(text.indexOf('\n[*.{')).split('\n');

		expect(markup).toContain('dotnet_diagnostic.ZA0101.severity = warning');
		expect(markup).toContain('# dotnet_diagnostic.ZA0102.severity = warning');
	});
});

describe('a key whose default differs in stanza-markup', () => {
	const real = realCatalog as unknown as SiteCatalog;
	const scope = real.options.find((entry) => entry.key === 'stanza_markup_scope')!;
	const profile = real.options.find((entry) => entry.key === 'stanza_markup_profile')!;

	const analyzerPart = (text: string): string => text.slice(0, text.indexOf('[*.{'));
	const markupPart = (text: string): string => text.slice(text.indexOf('[*.{'));

	it('reads the analyzer default and no tool default from the page', () => {
		expect(scope).toMatchObject({ audience: 'both', defaultValue: 'all', markupDefaultValue: null });
		expect(optionDefaultLabel(scope)).toBe('Default (all; per profile)');
	});

	it('writes the analyzer default under every setting and leaves the markup section without it', () => {
		const text = generateEditorConfig(real, config({ everySetting: true }));

		expect(analyzerPart(text)).toContain('stanza_markup_scope = all');
		expect(markupPart(text)).not.toContain('stanza_markup_scope');
	});

	it('writes an explicit all into the markup section only, because a profile may make the tool narrower', () => {
		const changed = config({ options: { stanza_markup_scope: 'all' } });
		const text = generateEditorConfig(real, changed);

		expect(text).not.toContain('[*.cs]');
		expect(markupPart(text)).toContain('stanza_markup_scope = all');
		expect(changedCount(real, changed)).toBe(1);
	});

	it('writes containers into both sections', () => {
		const text = generateEditorConfig(real, config({ options: { stanza_markup_scope: 'containers' } }));

		expect(analyzerPart(text)).toContain('stanza_markup_scope = containers');
		expect(markupPart(text)).toContain('stanza_markup_scope = containers');
	});

	it('says why the key is missing when the markup section is off and only the tool would have had it', () => {
		const text = generateEditorConfig(real, config({ options: { stanza_markup_scope: 'all' }, markupSection: false }));
		expect(text).toBe('# stanza-markup settings are left out because Markup section is off.\n');
	});

	it('warns above a profile that every file the section matches would take it', () => {
		const text = generateEditorConfig(real, config({ options: { stanza_markup_profile: 'msbuild' } }));
		const lines = markupPart(text).split('\n');
		const at = lines.indexOf('stanza_markup_profile = msbuild');

		expect(profile).toMatchObject({ kind: 'enum', perFile: true });
		expect(text).not.toContain('[*.cs]');
		expect(at).toBeGreaterThan(0);
		expect(lines[at - 1]).toBe('# stanza_markup_profile applies to every file this section matches, so narrow the glob to the files that need it.');
	});
});

describe('highlighting', () => {
	it('keeps the text of every line, so the diff compares what the page wrote', async () => {
		const { catalog: highlighted, css } = await highlightCatalog(realCatalog);

		const plain = (markup: string): string =>
			markup
				.replace(/<[^>]+>/g, '')
				.replaceAll('&lt;', '<')
				.replaceAll('&gt;', '>')
				.replaceAll('&quot;', '"')
				.replaceAll('&amp;', '&');

		for (const rule of highlighted.rules) {
			for (const example of rule.examples) {
				for (const snippet of [example.before, ...example.afters.map((after) => after.snippet)]) {
					for (const line of snippet.lines) {
						expect(plain(line.html), rule.id).toBe(line.text);
					}
				}
			}
		}

		expect(css).toContain('.t0{color:');
	}, 60_000);
});
