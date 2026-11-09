import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import { parseLevels } from '../../build/readers/levels.ts';
import { ExtractionError } from '../../build/readers/source.ts';
import { findConflicts } from '../../src/editorconfig/conflicts.ts';
import { changedCount, effectiveState, levelState, withLevel } from '../../src/state/config.ts';
import {
	addsPresetFile,
	defaultFormatter,
	defaultLevel,
	defaultPackageSeverity,
	enabledAt,
	enabledNote,
	formatters,
	levels,
	packageSeverities,
	type Formatter,
	type Level,
	type PackageSeverity,
} from '../../src/state/levels.ts';
import { config, realCatalog, realMarkup, repositoryRoot, rule } from '../fixtures.ts';

describe('the level table', () => {
	const file = 'levels.txt';

	it('reads a level and a CSharpier mark for each rule, past the comments and blank lines', () => {
		const rows = parseLevels(
			file,
			['# A note.', '', 'ZA0001 moderate', 'ZA0012 strong   csharpier-off', 'ZA0142\topt-in csharpier-on', 'ZA0054 opt-in', ''].join('\r\n'),
		);

		expect([...rows]).toEqual([
			['ZA0001', { level: 'moderate', csharpier: null }],
			['ZA0012', { level: 'strong', csharpier: 'off' }],
			['ZA0142', { level: 'opt-in', csharpier: 'on' }],
			['ZA0054', { level: 'opt-in', csharpier: null }],
		]);
	});

	it.each([
		['a row of the wrong shape', 'ZA0001', /line 1: "ZA0001" is not/],
		['a level that does not exist', 'ZA0001 huge', /the level "huge" of ZA0001 is none of/],
		['a mark that does not exist', 'ZA0001 strong csharpier-maybe', /is not "<ID> <level>/],
		['a rule listed twice', 'ZA0001 strong\nZA0001 minimal', /line 2: ZA0001 has two rows/],
		['no rule at all', '# only a note', /no rule was found/],
	])('fails with the file and the line on %s', (_what, text, message) => {
		expect(() => parseLevels(file, text)).toThrow(ExtractionError);
		expect(() => parseLevels(file, text)).toThrow(message);
		expect(() => parseLevels(file, text)).toThrow(/^levels\.txt: /);
	});

	it('gives every rule of the real pages its level, and the marks of the table', () => {
		const text = readFileSync(join(repositoryRoot, 'src/Nickelony.Stanza.Analyzers.Package/levels.txt'), 'utf8');
		const rows = parseLevels('levels.txt', text);

		expect(realCatalog.rules.map((candidate) => candidate.id).sort()).toEqual([...rows.keys()].sort());

		for (const candidate of realCatalog.rules) {
			expect({ level: candidate.level, csharpier: candidate.csharp!.csharpier }, candidate.id).toEqual(rows.get(candidate.id));
		}
	});

	it('gives a markup rule no row in the table, because no level holds it', () => {
		const text = readFileSync(join(repositoryRoot, 'src/Nickelony.Stanza.Analyzers.Package/levels.txt'), 'utf8');
		const rows = parseLevels('levels.txt', text);

		for (const candidate of realMarkup.rules) {
			expect(rows.has(candidate.id), candidate.id).toBe(false);
			expect(candidate.level, candidate.id).toBe('no-level');
		}
	});

	it('marks as undone by CSharpier each rule it turns off', () => {
		const off = realCatalog.rules.filter((candidate) => candidate.csharp!.csharpier === 'off');

		expect(off.length).toBeGreaterThan(0);

		for (const candidate of off) {
			expect(candidate.csharp!.undoneByCSharpier, candidate.id).toBe(true);
		}
	});
});

/** Reads the `dotnet_diagnostic` lines of a preset file the package ships, or `null` where the package adds none. */
function presetLines(name: string): Map<string, string> | null {
	const path = join(repositoryRoot, 'src/Nickelony.Stanza.Analyzers.Package/build/presets', `${name}.globalconfig`);

	if (!existsSync(path)) {
		return null;
	}

	const lines = new Map<string, string>();

	for (const match of readFileSync(path, 'utf8').matchAll(/^dotnet_diagnostic\.(ZA\d{4})\.severity = (\w+)$/gm)) {
		lines.set(match[1]!, match[2]!);
	}

	return lines;
}

describe('filling the form from a level', () => {
	const combinations = levels.flatMap((level) =>
		packageSeverities.flatMap((severity) => formatters.map((formatter): [Level, PackageSeverity, Formatter] => [level, severity, formatter])),
	);

	it.each(combinations)('gives each rule the state the %s level shipped at %s with the %s formatter gives it', (level, severity, formatter) => {
		const picks = config({ level, severity, formatter });

		// The package adds a file for the level and, at strong with csharpier, the overlay over it; the moderate level at suggestion adds none.
		const file = level === 'none' ? presetLines('none') : presetLines(`${level}.${severity}`);
		const overlay = level === 'strong' && formatter === 'csharpier' ? presetLines(`csharpier.${severity}`) : null;

		for (const candidate of realCatalog.rules) {
			const written = overlay?.get(candidate.id) ?? file?.get(candidate.id) ?? (candidate.onByDefault ? 'suggestion' : 'none');
			const expected = written === 'none' ? 'off' : written;
			const state = levelState(candidate, picks);

			expect(state === 'none' ? 'off' : state, `${candidate.id} at ${level}`).toBe(expected);
			expect(effectiveState(candidate, picks)).toBe(state);
		}
	});

	const countAt = (level: Level, formatter: Formatter): number => realCatalog.rules.filter((rule) => enabledAt(rule, level, formatter)).length;

	it('nests the levels, with the opt-in rules in none of them', () => {
		const counts = levels.map((level) => countAt(level, 'stanza-format'));

		expect(counts[0]).toBe(0);
		expect(counts[1]).toBeLessThan(counts[2]!);
		expect(counts[2]).toBeLessThan(counts[3]!);
		expect(counts[3]).toBeLessThan(realCatalog.rules.length);

		for (const candidate of realCatalog.rules.filter((other) => other.level === 'opt-in' && other.csharp!.csharpier === null)) {
			expect(enabledAt(candidate, 'strong', 'stanza-format'), candidate.id).toBe(false);
			expect(enabledAt(candidate, 'strong', 'csharpier'), candidate.id).toBe(false);
		}
	});

	it('changes only the strong level for the csharpier formatter', () => {
		for (const level of ['minimal', 'moderate', 'none'] as const) {
			expect(countAt(level, 'csharpier'), level).toBe(countAt(level, 'stanza-format'));
		}

		const turnedOn = realCatalog.rules.filter((candidate) => candidate.csharp!.csharpier === 'on');
		const turnedOff = realCatalog.rules.filter((candidate) => candidate.csharp!.csharpier === 'off');

		expect(countAt('strong', 'csharpier')).toBe(countAt('strong', 'stanza-format') + turnedOn.length - turnedOff.length);
	});

	it('drops the rules set against another level when a level is picked, and keeps the pickers and the options', () => {
		const before = config({
			level: 'moderate',
			severity: 'warning',
			formatter: 'csharpier',
			rules: { ZA0001: 'none' },
			options: { stanza_max_line_length: '120' },
		});

		const after = withLevel(before, 'strong');

		expect(after).toEqual({ ...before, level: 'strong', rules: {} });
		expect(changedCount(realCatalog, after)).toBe(1);
	});

	it('counts the delta as the rules and options that differ from the level', () => {
		let picks = config({ level: 'strong' });

		expect(changedCount(realCatalog, picks)).toBe(0);

		// ZA0001 is on at strong, so a setting that says suggestion changes nothing; none turns it off; ZA0054 is opt-in, so any severity enables it.
		picks = { ...picks, rules: { ZA0001: 'suggestion', ZA0002: 'none', ZA0054: 'suggestion', ZA0055: 'none' } };

		expect(changedCount(realCatalog, picks)).toBe(2);
	});

	it('warns of no conflict for the halves a level enables, and none with a formatter at strong', () => {
		for (const level of levels) {
			for (const formatter of formatters) {
				expect(findConflicts(realCatalog, config({ level, formatter })), `${level} ${formatter}`).toEqual([]);
			}
		}
	});
});

describe('the properties file', () => {
	it('adds a preset file for every pick but the moderate level at suggestion', () => {
		expect(addsPresetFile('moderate', 'suggestion')).toBe(false);
		expect(addsPresetFile('moderate', 'warning')).toBe(true);
		expect(addsPresetFile('strong', 'suggestion')).toBe(true);
	});
});

describe('a fixture rule', () => {
	it('is enabled from its level and by no lower one', () => {
		const strong = rule('ZA0003', { onByDefault: false });

		expect(enabledAt(strong, 'moderate', 'stanza-format')).toBe(false);
		expect(enabledAt(strong, 'strong', 'stanza-format')).toBe(true);
		expect(enabledAt(rule('ZA0001'), 'minimal', 'stanza-format')).toBe(false);
		expect(enabledAt(rule('ZA0001', { level: 'minimal' }), 'minimal', 'stanza-format')).toBe(true);
	});
});

describe('the note on which level enables a rule', () => {
	const find = (id: string) => realCatalog.rules.find((candidate) => candidate.id === id)!;

	it('names the level of a plain rule whatever the formatter', () => {
		for (const formatter of formatters) {
			expect(enabledNote(find('ZA0001'), formatter)).toBe(' Enabled from the moderate level.');
			expect(enabledNote(find('ZA0054'), formatter)).toBe(' No level enables it.');
		}
	});

	it('says the csharpier formatter enables an opt-in rule it marks on, and no level does for the other formatter', () => {
		const marked = find('ZA0152');

		expect(marked.csharp!.csharpier).toBe('on');
		expect(enabledNote(marked, 'stanza-format')).toBe(' No level enables it.');
		expect(enabledNote(marked, 'csharpier')).toBe(' No level enables it; the csharpier formatter enables it at the strong level.');
	});

	it('says the csharpier formatter turns off a strong rule it marks off, and the level enables it for the other formatter', () => {
		const marked = find('ZA0151');

		expect(marked.csharp!.csharpier).toBe('off');
		expect(enabledNote(marked, 'stanza-format')).toBe(' Enabled from the strong level.');
		expect(enabledNote(marked, 'csharpier')).toBe(' Enabled from the strong level; the csharpier formatter turns it off at the strong level.');
	});

	it('agrees with the rules the strong level enables under each formatter', () => {
		for (const candidate of realCatalog.rules.filter((other) => other.csharp!.csharpier !== null)) {
			const note = enabledNote(candidate, 'csharpier');

			expect(
				note.includes(candidate.csharp!.csharpier === 'on' ? 'enables it at the strong level' : 'turns it off at the strong level'),
				candidate.id,
			).toBe(true);

			expect(enabledAt(candidate, 'strong', 'csharpier'), candidate.id).toBe(candidate.csharp!.csharpier === 'on');
		}
	});
});

describe('a rule that no level holds', () => {
	const unleveled = rule('ZA2001', { level: 'no-level', onByDefault: false, markupOnByDefault: true, readByMarkup: true });

	it('is enabled at no level, whatever the formatter', () => {
		for (const level of levels) {
			for (const formatter of formatters) {
				expect(enabledAt(unleveled, level, formatter), `${level} ${formatter}`).toBe(false);
				expect(enabledAt(realMarkup.rules[1]!, level, formatter), `${level} ${formatter}`).toBe(false);
			}
		}
	});

	it('says that no level enables it', () => {
		for (const formatter of formatters) {
			expect(enabledNote(unleveled, formatter)).toBe(' No level enables it.');
		}
	});

	it('starts on in the tool whatever level is picked, as the form drops the rules set against the old level', () => {
		for (const level of levels) {
			expect(levelState(unleveled, withLevel(config({ rules: { ZA2001: 'warning' } }), level)), level).toBe('suggestion');
			expect(effectiveState(unleveled, withLevel(config({}), level)), level).toBe('suggestion');
		}
	});
});

describe('the package properties', () => {
	const targets = readFileSync(join(repositoryRoot, 'src/Nickelony.Stanza.Analyzers.Package/build/Nickelony.Stanza.Analyzers.targets'), 'utf8');

	/** Reads what the targets file accepts for a property from the `STANZA001` check on it. */
	const accepted = (property: string): string[] => {
		const check = targets.match(new RegExp(String.raw`<Error\s+Condition="([^"]*_Stanza${property}\)[^"]*)"\s+Code="STANZA001"`));
		return [...check![1]!.matchAll(/!= '([^']+)'/g)].map((match) => match[1]!);
	};

	/** Reads what the targets file uses for a property while the project sets none. */
	const fallback = (property: string): string =>
		targets.match(new RegExp(String.raw`<_Stanza${property} Condition="'\$\(_Stanza${property}\)' == ''">([^<]+)<`))![1]!;

	it('accept the values and default to the ones the site offers', () => {
		// The page orders the levels from none upward, and the package lists them in its own order.
		expect(accepted('Level').toSorted()).toEqual([...levels].toSorted());
		expect(accepted('Severity')).toEqual([...packageSeverities]);
		expect(accepted('Formatter')).toEqual([...formatters]);
		expect(fallback('Level')).toBe(defaultLevel);
		expect(fallback('Severity')).toBe(defaultPackageSeverity);
		expect(fallback('Formatter')).toBe(defaultFormatter);
	});
});
