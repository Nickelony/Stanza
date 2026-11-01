import { describe, expect, it } from 'vitest';

import { findConflicts } from '../../src/editorconfig/conflicts.ts';
import { importEditorConfig, listedNotes, maxImportLength, maxListedNotes, refusal, sizeRefusal, summarize } from '../../src/editorconfig/import.ts';
import { changedCount, emptyConfig, isRuleChanged, type Config } from '../../src/state/config.ts';
import { generateEditorConfig } from '../../src/writers/editorconfig.ts';
import { catalog, markupConfig, option, realCatalog, realMarkup, rule } from '../fixtures.ts';

const small = catalog(
	[
		rule('ZA0002'),
		rule('ZA0014', { tier: 'suggestion', onByDefault: false }),
		rule('ZA2001', { onByDefault: false, markupOnByDefault: true, readByMarkup: true }),
	],

	[
		option('stanza_max_line_length', { audience: 'both' }),
		option('stanza_named_argument_match', { kind: 'enum', choices: ['allow', 'require'], defaultValue: 'allow' }),
		option('stanza_markup_enabled', { audience: 'markup', kind: 'enum', choices: ['true', 'false'], defaultValue: 'true' }),
		option('stanza_assertion_roots', { kind: 'list', defaultValue: null }),
	],
);

const read = (text: string, current: Config = emptyConfig()) => importEditorConfig(small, text, current);

describe('an imported .editorconfig', () => {
	it('fills in the rules and options of a C# section', () => {
		const result = read(
			['[*.cs]', 'dotnet_diagnostic.ZA0014.severity = warning', 'dotnet_diagnostic.ZA0002.severity = none', 'stanza_max_line_length = 120', ''].join(
				'\n',
			),
		);

		expect(result.refused).toBeNull();
		expect(result.config.rules).toEqual({ ZA0014: 'warning', ZA0002: 'none' });
		expect(result.config.options).toEqual({ stanza_max_line_length: '120' });
		expect(result.skipped).toEqual([]);
		expect(result.rules).toBe(2);
		expect(result.options).toBe(1);
	});

	it('leaves a rule set to default at its default', () => {
		expect(read('[*.cs]\ndotnet_diagnostic.ZA0014.severity = default\n').config.rules).toEqual({});
	});

	it('leaves an option at the value its page gives as the default', () => {
		expect(read('[*.cs]\nstanza_max_line_length = 100\n').config.options).toEqual({});
	});

	it('reads the keys without regard to case, as an .editorconfig does', () => {
		const result = read('[*.CS]\nDOTNET_DIAGNOSTIC.za0014.SEVERITY = Warning\nSTANZA_MAX_LINE_LENGTH = 90\n');

		expect(result.config.rules).toEqual({ ZA0014: 'warning' });
		expect(result.config.options).toEqual({ stanza_max_line_length: '90' });
	});

	it('cuts a trailing # comment from an option and from a severity', () => {
		const result = read('[*.cs]\nstanza_max_line_length = 120 # same as csharpier\ndotnet_diagnostic.ZA0014.severity = warning # team\n');

		expect(result.config.options).toEqual({ stanza_max_line_length: '120' });
		expect(result.config.rules).toEqual({ ZA0014: 'warning' });
		expect(result.skipped).toEqual([]);
	});

	it('cuts a trailing ; comment from an option and from a severity', () => {
		const result = read('[*.cs]\nstanza_max_line_length = 120 ; same as csharpier\ndotnet_diagnostic.ZA0014.severity = warning ; team\n');

		expect(result.config.options).toEqual({ stanza_max_line_length: '120' });
		expect(result.config.rules).toEqual({ ZA0014: 'warning' });
		expect(result.skipped).toEqual([]);
	});

	it('cuts a comment from a section header', () => {
		expect(read('[*.cs] # C# files\ndotnet_diagnostic.ZA0014.severity = warning\n').config.rules).toEqual({ ZA0014: 'warning' });
	});

	it('reads a colon as the separator of a key and its value, as stanza-markup does', () => {
		const result = read('[*.cs]\nstanza_max_line_length: 120\ndotnet_diagnostic.ZA0014.severity : warning\n');

		expect(result.config.options).toEqual({ stanza_max_line_length: '120' });
		expect(result.config.rules).toEqual({ ZA0014: 'warning' });
		expect(result.skipped).toEqual([]);
	});

	it('drops the :severity suffix of a stanza_ value, as stanza-markup does', () => {
		const result = read('[*.cs]\nstanza_max_line_length = 120:suggestion\nstanza_named_argument_match = require : warning # note\n');

		expect(result.config.options).toEqual({ stanza_max_line_length: '120', stanza_named_argument_match: 'require' });
		expect(result.skipped).toEqual([]);
	});

	it('reads a refactoring severity as silent, which Roslyn treats as another name for it', () => {
		const result = read('[*.cs]\ndotnet_diagnostic.ZA0014.severity = refactoring\ndotnet_diagnostic.ZA0002.severity = Refactoring\n');

		expect(result.config.rules).toEqual({ ZA0014: 'silent', ZA0002: 'silent' });
		expect(result.skipped).toEqual([]);
	});

	it('lets a later line win over an earlier one', () => {
		const result = read('[*.cs]\ndotnet_diagnostic.ZA0014.severity = warning\n[*.cs]\ndotnet_diagnostic.ZA0014.severity = error\n');
		expect(result.config.rules).toEqual({ ZA0014: 'error' });
	});

	it('reads a file with a byte order mark, CRLF and old Mac line ends, and blanks', () => {
		const text = '﻿# a comment\r\n\r\n; another\r\n[*.cs]\r\ndotnet_diagnostic.ZA0014.severity = warning\rstanza_max_line_length=90\r\n';
		const result = read(text);

		expect(result.config.rules).toEqual({ ZA0014: 'warning' });
		expect(result.config.options).toEqual({ stanza_max_line_length: '90' });
		expect(result.skipped).toEqual([]);
	});

	it('reads the lines above the first section, which a .globalconfig puts them in', () => {
		expect(read('is_global = true\ndotnet_diagnostic.ZA0014.severity = warning\n').config.rules).toEqual({ ZA0014: 'warning' });
	});

	it.each(['[*]', '[*.cs]', '[*.{cs,vb}]', '[**.cs]', '[**/*.cs]', '[*.{vb, cs}]'])('reads the section %s, which matches C# files', (header) => {
		expect(read(`${header}\ndotnet_diagnostic.ZA0014.severity = warning\n`).config.rules).toEqual({ ZA0014: 'warning' });
	});

	it('reads a markup section and takes the markup keys from it', () => {
		const result = read(
			[
				'[*.cs]',
				'dotnet_diagnostic.ZA2001.severity = warning',
				'[*.{xml,csproj}]',
				'dotnet_diagnostic.ZA2001.severity = warning',
				'stanza_markup_enabled = false',
				'',
			].join('\n'),
		);

		expect(result.config.rules).toEqual({ ZA2001: 'warning' });
		expect(result.config.options).toEqual({ stanza_markup_enabled: 'false' });
		expect(result.config.markupSection).toBe(true);
		expect(result.skipped).toEqual([]);
	});

	it('reads a rule given only in the markup section', () => {
		expect(read('[*.xml]\ndotnet_diagnostic.ZA2001.severity = none\n').config.rules).toEqual({ ZA2001: 'none' });
	});

	it('reads a markup line that says what stanza-markup already applies as no change', () => {
		expect(read('[*.xml]\ndotnet_diagnostic.ZA2001.severity = error\n').config.rules).toEqual({});
	});

	it('reports a rule whose markup line differs from its C# line, and keeps the C# line', () => {
		const result = read('[*.cs]\ndotnet_diagnostic.ZA2001.severity = warning\n[*.xml]\ndotnet_diagnostic.ZA2001.severity = error\n');

		expect(result.config.rules).toEqual({ ZA2001: 'warning' });
		expect(result.skipped).toHaveLength(1);
		expect(result.skipped[0]).toMatchObject({ line: 4 });
		expect(result.skipped[0]!.reason).toMatch(/differs/);
	});

	it('writes the markup section only when the file has one', () => {
		expect(read('[*.cs]\ndotnet_diagnostic.ZA0014.severity = warning\n').config.markupSection).toBe(false);
		expect(read('[*.xml]\nstanza_markup_enabled = false\n').config.markupSection).toBe(true);
	});

	it('keeps the choices of the page that no file holds', () => {
		const current: Config = { ...emptyConfig(), everySetting: true, formatter: 'csharpier', rules: { ZA0002: 'none' } };
		const result = read('[*.cs]\ndotnet_diagnostic.ZA0014.severity = warning\n', current);

		expect(result.config.everySetting).toBe(true);
		expect(result.config.formatter).toBe('csharpier');
		expect(result.config.rules).toEqual({ ZA0014: 'warning' });
	});

	describe('against a level', () => {
		const strong: Config = { ...emptyConfig(), level: 'strong', severity: 'warning' };

		it('keeps the pickers of the page, which no file holds', () => {
			const result = importEditorConfig(realCatalog, '[*.cs]\ndotnet_diagnostic.ZA0003.severity = none\n', { ...strong, formatter: 'csharpier' });

			expect(result.config).toMatchObject({ level: 'strong', severity: 'warning', formatter: 'csharpier' });
			expect(result.config.rules).toEqual({ ZA0003: 'none' });
		});

		it('drops a line that says what the level gives, and counts the others', () => {
			const text = [
				'[*.cs]',
				'dotnet_diagnostic.ZA0003.severity = warning',
				'dotnet_diagnostic.ZA0054.severity = none',
				'dotnet_diagnostic.ZA0014.severity = error',
				'dotnet_diagnostic.ZA0001.severity = suggestion',
				'',
			].join('\n');

			const result = importEditorConfig(realCatalog, text, strong);

			// ZA0003 is strong, so warning is its state; ZA0054 is opt-in, so none is its state; the other two differ.
			expect(result.config.rules).toEqual({ ZA0014: 'error', ZA0001: 'suggestion' });
			expect(result.rules).toBe(2);
			expect(result.skipped).toEqual([]);
		});

		it('keeps a line that switches off a rule the level leaves on in the tool, though the analyzer has it off', () => {
			const result = read('[*.xml]\ndotnet_diagnostic.ZA2001.severity = none\n', { ...emptyConfig(), level: 'minimal' });
			expect(result.config.rules).toEqual({ ZA2001: 'none' });
		});

		it('reads a markup line that says what the tool does already as no change', () => {
			const result = read('[*.xml]\ndotnet_diagnostic.ZA2001.severity = suggestion\n');
			const real = importEditorConfig(realCatalog, '[*.xml]\ndotnet_diagnostic.ZA0909.severity = suggestion\n', emptyConfig());

			expect(result.config.rules).toEqual({});
			expect(real.config.rules).toEqual({});
		});

		it('reads the lines of a file written without a level against the default level', () => {
			const old = ['[*.cs]', 'dotnet_diagnostic.ZA0002.severity = warning', 'dotnet_diagnostic.ZA0003.severity = warning', ''].join('\n');
			const result = importEditorConfig(realCatalog, old, emptyConfig());

			expect(result.config.rules).toEqual({ ZA0002: 'warning', ZA0003: 'warning' });
			expect(result.config.level).toBe('moderate');
		});
	});

	it('does not change the configuration it was given', () => {
		const current = emptyConfig();
		read('[*.cs]\ndotnet_diagnostic.ZA0014.severity = warning\n', current);

		expect(current).toEqual(emptyConfig());
	});

	it('counts the lines of other settings it has no use for, without listing them', () => {
		const result = read(
			'root = true\n[*]\nindent_style = tab\ncsharp_style_var_elsewhere = true:suggestion\n[*.md]\ntrim_trailing_whitespace = false\n',
		);

		expect(result.ignored).toBe(4);
		expect(result.skipped).toEqual([]);
	});
});

describe('an import that skips', () => {
	it('names an unknown rule and an unknown key with their lines', () => {
		const result = read('[*.cs]\ndotnet_diagnostic.ZA9999.severity = warning\nstanza_wide = 3\ndotnet_diagnostic.ZA0014.severity = warning\n');

		expect(result.config.rules).toEqual({ ZA0014: 'warning' });
		expect(result.skipped.map((note) => note.line)).toEqual([2, 3]);
		expect(result.skipped[0]!.reason).toMatch(/no rule/i);
		expect(result.skipped[1]!.reason).toMatch(/no option/i);
	});

	it.each(['loud', 'hidden', '', '# only a comment', 'warning:suggestion', 'true'])(
		'names the severity "%s", which an .editorconfig does not give a rule',

		(value) => {
			const result = read(`[*.cs]\ndotnet_diagnostic.ZA0014.severity = ${value}\n`);

			expect(result.config.rules).toEqual({});
			expect(result.skipped).toHaveLength(1);
			expect(result.skipped[0]).toMatchObject({ line: 2, text: `dotnet_diagnostic.ZA0014.severity = ${value}`.trimEnd() });
		},
	);

	it('names an option value its page does not allow, and says what it allows', () => {
		const result = read('[*.cs]\nstanza_max_line_length = wide\nstanza_named_argument_match = maybe\nstanza_max_line_length = 5000\n');

		expect(result.config.options).toEqual({});
		expect(result.skipped).toHaveLength(3);
		expect(result.skipped[1]!.reason).toContain('allow, require');
	});

	it('names a line that is no setting, and a header it cannot read', () => {
		const result = read('[*.cs]\nthis is not a setting\n= value\nstanza_max_line_length\n[unclosed\ndotnet_diagnostic.ZA0014.severity = warning\n');

		expect(result.skipped.map((note) => note.line)).toEqual([2, 3, 4, 5]);
		expect(result.config.rules).toEqual({ ZA0014: 'warning' });
	});

	it('skips a section scoped to a path or to other files, and names each Stanza line in it by one note', () => {
		const result = read(
			[
				'[tests/**/*.cs]',
				'dotnet_diagnostic.ZA0014.severity = none',
				'stanza_max_line_length = 200',
				'[*.json]',
				'dotnet_diagnostic.ZA0002.severity = none',
				'[*.cs]',
				'dotnet_diagnostic.ZA0014.severity = warning',
				'',
			].join('\n'),
		);

		expect(result.config.rules).toEqual({ ZA0014: 'warning' });
		expect(result.config.options).toEqual({});
		expect(result.skipped.map((note) => note.line)).toEqual([1, 4]);
		expect(result.skipped[0]!.reason).toContain('2 Stanza lines');
		expect(result.skipped[1]!.reason).toContain('1 Stanza line');
	});

	it('says nothing about a section it skips that holds no Stanza line', () => {
		const result = read('[tests/**/*.cs]\nindent_style = tab\n[*.cs]\ndotnet_diagnostic.ZA0014.severity = warning\n');

		expect(result.skipped).toEqual([]);
		expect(result.ignored).toBe(1);
	});

	it('names a value that is only a comment, as empty', () => {
		const result = read('[*.cs]\nstanza_max_line_length = # none yet\nstanza_named_argument_match = ; none yet\n');

		expect(result.config.options).toEqual({});
		expect(result.skipped).toHaveLength(2);
		expect(result.skipped[0]!.reason).toMatch(/empty/i);
		expect(result.skipped[1]!.reason).toMatch(/empty/i);
	});

	it('cuts a long line in the note, not in the reading', () => {
		const result = read(`[*.cs]\nstanza_unknown_${'x'.repeat(5000)} = 1\n`);

		expect(result.skipped).toHaveLength(1);
		expect(result.skipped[0]!.text.length).toBeLessThan(200);
	});
});

describe('an import that cannot be read', () => {
	it('refuses a file with a NUL character, which is not text, and leaves the configuration as it was', () => {
		const current: Config = { ...emptyConfig(), rules: { ZA0002: 'none' } };
		const result = read('[*.cs]\u0000\u0001\u0002binary', current);

		expect(result.refused).toMatch(/text/);
		expect(result.config).toEqual(current);
		expect(result.skipped).toEqual([]);
	});

	it('refuses a file larger than the limit', () => {
		const result = read(`[*.cs]\n${'# padding\n'.repeat(Math.ceil(maxImportLength / 9) + 1)}`);
		expect(result.refused).toMatch(/larger/);
	});

	it('reads an empty file as one with nothing in it', () => {
		const result = read('');

		expect(result.refused).toBeNull();
		expect(result.config).toEqual(emptyConfig());
		expect(result.rules + result.options).toBe(0);
	});

	it('survives text of every shape without throwing', () => {
		let seed = 12345;

		const next = (limit: number): number => {
			seed = (seed * 1103515245 + 12345) & 0x7fffffff;
			return seed % limit;
		};

		const pieces = [
			'[',
			']',
			'[*.cs]',
			'[*.{',
			'}',
			'=',
			' = ',
			'\n',
			'\r\n',
			'\r',
			'#',
			';',
			'dotnet_diagnostic.',
			'ZA0014',
			'.severity',
			'stanza_max_line_length',
			'warning',
			'{',
			',',

			' ',

			'\u0085',
			'\uD800',
			'﻿',
			'ñ',
			'*',
			'**',
			'\\',
			'"',
		];

		for (let round = 0; round < 400; round++) {
			let text = '';

			for (let piece = next(40); piece > 0; piece--) {
				text += pieces[next(pieces.length)];
			}

			expect(() => importEditorConfig(small, text, emptyConfig()), JSON.stringify(text)).not.toThrow();
		}
	});

	it('survives a value that is not a string, as a script that calls it with the wrong thing would pass', () => {
		for (const bad of [null, undefined, 42, {}, []]) {
			const result = importEditorConfig(small, bad as unknown as string, emptyConfig());
			expect(result.refused, String(bad)).not.toBeNull();
		}
	});
});

describe('the report of an import', () => {
	it('says what was applied, skipped and left alone', () => {
		const result = read('[*.cs]\ndotnet_diagnostic.ZA0014.severity = warning\nstanza_wide = 1\nindent_style = tab\nroot = true\n');

		expect(summarize(result, '.editorconfig')).toBe(
			"Imported 1 rule setting and 0 options from .editorconfig. 1 line skipped, listed below. 2 lines of other settings were not Stanza's and left alone.",
		);
	});

	it('says that nothing was skipped', () => {
		expect(summarize(read('[*.cs]\nstanza_max_line_length = 90\n'), 'a')).toBe(
			'Imported 0 rule settings and 1 option from a. No Stanza line was skipped.',
		);
	});

	it('says why a file was refused', () => {
		expect(summarize(refusal(emptyConfig(), sizeRefusal(maxImportLength + 1)!), 'big')).toMatch(
			/^Nothing was imported from big\. The file is larger/,
		);
	});

	it('accepts a file at the limit', () => {
		expect(sizeRefusal(maxImportLength)).toBeNull();
	});

	it('lists every note of a file that skips few lines', () => {
		const result = read('[*.cs]\nstanza_wide = 1\nstanza_narrow = 2\n');
		expect(listedNotes(result.skipped)).toEqual({ shown: result.skipped, more: 0 });
	});

	it('caps the notes of a file of bogus keys and counts the rest', () => {
		const lines = Array.from({ length: maxListedNotes + 50 }, (_, index) => `stanza_bogus_${index} = 1`);
		const result = read(`[*.cs]\n${lines.join('\n')}\n`);

		expect(result.skipped).toHaveLength(maxListedNotes + 50);

		const { shown, more } = listedNotes(result.skipped);

		expect(shown).toEqual(result.skipped.slice(0, maxListedNotes));
		expect(more).toBe(50);
		expect(summarize(result, 'a')).toContain(`${maxListedNotes + 50} lines skipped, the first ${maxListedNotes} listed below.`);
	});
});

describe('an import of a key the tool has no default for', () => {
	// The analyzer default is `all`, and the tool takes the profile's own scope, so the page lists no default for the tool.
	const scoped = catalog(
		[],

		[
			option('stanza_markup_scope', {
				audience: 'both',
				kind: 'enum',
				choices: ['all', 'containers'],
				defaultValue: 'all',
				markupDefaultValue: null,
			}),

			option('stanza_markup_enabled', {
				audience: 'markup',
				kind: 'enum',
				choices: ['true', 'false'],
				defaultValue: null,
				markupDefaultValue: 'true',
			}),
		],
	);

	const readScoped = (text: string) => importEditorConfig(scoped, text, emptyConfig());

	it('reads the analyzer default in the C# section as no change', () => {
		expect(readScoped('[*.cs]\nstanza_markup_scope = all\n').config.options).toEqual({});
	});

	it('reads another value in the C# section as a change', () => {
		expect(readScoped('[*.cs]\nstanza_markup_scope = containers\n').config.options).toEqual({ stanza_markup_scope: 'containers' });
	});

	it('reads the same value in the markup section as a change, because the tool has no default to match', () => {
		expect(readScoped('[*.xml]\nstanza_markup_scope = all\n').config.options).toEqual({ stanza_markup_scope: 'all' });
	});

	it('holds a key only the tool reads against the tool default wherever its line stands', () => {
		expect(readScoped('[*.cs]\nstanza_markup_enabled = false\n').config.options).toEqual({ stanza_markup_enabled: 'false' });
		expect(readScoped('[*.cs]\nstanza_markup_enabled = true\n').config.options).toEqual({});
	});
});

describe('an import of what the page wrote', () => {
	const configs: Config[] = [
		emptyConfig(),
		{ ...emptyConfig(), rules: { ZA0002: 'none', ZA0014: 'suggestion', ZA2001: 'warning' }, options: { stanza_max_line_length: '120' } },

		{
			...emptyConfig(),
			markupSection: false,
			rules: { ZA0014: 'silent' },
			options: { stanza_named_argument_match: 'require', stanza_assertion_roots: 'Verify, Assert' },
		},

		{ ...emptyConfig(), options: { stanza_markup_enabled: 'false' } },
	];

	it.each(configs.map((config, index) => [index, config] as const))('gives back configuration %i', (_index, config) => {
		const text = generateEditorConfig(small, config);
		const back = importEditorConfig(small, text, emptyConfig());

		expect(back.skipped).toEqual([]);
		expect(back.config.rules).toEqual(config.rules);
		expect(back.config.options).toEqual(config.options);
	});

	it('gives back the real catalog with a rule from each group and an option of each kind set', () => {
		const rules = Object.fromEntries(
			realCatalog.groups.map((group, index) => [
				realCatalog.rules.find((candidate) => candidate.group === group)!.id,
				(['error', 'warning', 'suggestion', 'silent', 'none'] as const)[index % 5]!,
			]),
		);

		const options: Record<string, string> = { stanza_max_line_length: '123', stanza_markup_doc_reprint: 'members' };

		const config: Config = {
			...emptyConfig(),
			rules,
			options: Object.fromEntries(Object.entries(options).filter(([key]) => realCatalog.options.some((candidate) => candidate.key === key))),
		};

		const back = importEditorConfig(realCatalog, generateEditorConfig(realCatalog, config), emptyConfig());

		expect(back.skipped).toEqual([]);

		// A rule set to what the level gives, such as ZA0651 at suggestion, is no change, so it does not come back.
		expect(back.config.rules).toEqual(
			Object.fromEntries(
				Object.entries(config.rules).filter(([id]) =>
					isRuleChanged(
						realCatalog.rules.find((rule) => rule.id === id)!,
						config,
					),
				),
			),
		);

		expect(back.config.options).toEqual(config.options);
	});

	it('reports the sections scoped to a path', () => {
		const back = read(
			['[*.cs]', 'dotnet_diagnostic.ZA0014.severity = warning', '', '[tests/**/*.cs]', 'dotnet_diagnostic.ZA0014.severity = none', ''].join('\n'),
		);

		expect(back.refused).toBeNull();
		expect(back.rules).toBe(1);
		expect(back.skipped.some((note) => note.text === '[tests/**/*.cs]')).toBe(true);
		expect(back.config.rules['ZA0014']).toBeDefined();
	});

	it('shows the conflicts of an imported file that enables both halves of two pairs and a rule CSharpier undoes', () => {
		const text = [
			'[*.cs]',
			'dotnet_diagnostic.ZA0198.severity = suggestion',
			'dotnet_diagnostic.ZA0199.severity = suggestion',
			'dotnet_diagnostic.ZA0907.severity = warning',
			'dotnet_diagnostic.ZA0908.severity = warning',
			'dotnet_diagnostic.ZA0151.severity = suggestion',
			'',
		].join('\n');

		const back = importEditorConfig(realCatalog, text, { ...emptyConfig(), formatter: 'csharpier' });
		const found = findConflicts(realCatalog, back.config);

		expect(found.map((conflict) => `${conflict.kind}:${conflict.ids.join('+')}`)).toEqual([
			'pair:ZA0907+ZA0908',
			'pair:ZA0198+ZA0199',
			'formatter:ZA0151',
			'formatter:ZA0199',
		]);
	});

	it.each([
		['the markup section on', true],
		['the markup section off', false],
	])('gives back the file of every setting as no change, with %s', (_name, markupSection) => {
		const config: Config = { ...emptyConfig(), everySetting: true, markupSection };
		const back = importEditorConfig(realCatalog, generateEditorConfig(realCatalog, config), emptyConfig());

		// The page writes `stanza_markup_scope = all`, the analyzer default, where the tool has none, and that line is no change.
		expect(back.config.options).toEqual({});
		expect(back.config.rules).toEqual({});
		expect(changedCount(realCatalog, back.config)).toBe(0);
	});
});

describe('an import across the C# page and the Markup page', () => {
	const file = [
		'[*.cs]',
		'dotnet_diagnostic.ZA0003.severity = warning',
		'dotnet_diagnostic.ZA2002.severity = suggestion',
		'stanza_markup_literals = all',
		'stanza_markup_scope = containers',
		'',
		'[*.xml]',
		'dotnet_diagnostic.ZA2002.severity = suggestion',
		'stanza_markup_enabled = false',
		'',
	].join('\n');

	it('sends a markup rule and a markup key to the Markup page from the C# page, with their lines', () => {
		const result = importEditorConfig(realCatalog, file, emptyConfig());

		expect(result.config.rules).toEqual({ ZA0003: 'warning' });
		expect(result.config.options).toEqual({ stanza_markup_literals: 'all' });

		expect(result.skipped.map((note) => [note.line, note.reason])).toEqual([
			[3, 'ZA2002 belongs to the Markup page; import the file there.'],
			[5, 'stanza_markup_scope belongs to the Markup page; import the file there.'],
			[8, 'ZA2002 belongs to the Markup page; import the file there.'],
			[9, 'stanza_markup_enabled belongs to the Markup page; import the file there.'],
		]);
	});

	it('takes the markup rules and keys on the Markup page, and sends the key that picks the C# literals back', () => {
		const result = importEditorConfig(realMarkup, file, markupConfig({}));

		expect(result.config.rules).toEqual({ ZA2002: 'suggestion' });
		expect(result.config.options).toEqual({ stanza_markup_scope: 'containers', stanza_markup_enabled: 'false' });

		expect(result.skipped.map((note) => [note.line, note.reason])).toEqual([
			[2, 'The page has no rule ZA0003.'],
			[4, 'stanza_markup_literals belongs to the C# page; import the file there.'],
		]);
	});

	it('does not say a markup line belongs elsewhere on a page without a split', () => {
		const result = read('[*.cs]\ndotnet_diagnostic.ZA2002.severity = suggestion\n');
		expect(result.skipped.map((note) => note.reason)).toEqual(['The page has no rule ZA2002.']);
	});

	it('reads a markup rule given in the C# section alone as a change from the default the tool applies', () => {
		expect(importEditorConfig(realMarkup, '[*.cs]\ndotnet_diagnostic.ZA2001.severity = none\n', markupConfig({})).config.rules).toEqual({
			ZA2001: 'none',
		});

		expect(importEditorConfig(realMarkup, '[*.cs]\ndotnet_diagnostic.ZA2001.severity = suggestion\n', markupConfig({})).config.rules).toEqual({});

		expect(importEditorConfig(realMarkup, '[*.cs]\ndotnet_diagnostic.ZA2001.severity = error\n', markupConfig({})).config.rules).toEqual({
			ZA2001: 'error',
		});
	});

	it('keeps the none level of the Markup page, which no file holds', () => {
		const result = importEditorConfig(realMarkup, file, markupConfig({ everySetting: true }));
		expect(result.config).toMatchObject({ level: 'none', everySetting: true });
	});
});
