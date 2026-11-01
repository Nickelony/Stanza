import { describe, expect, it } from 'vitest';

import { csharp } from '../../src/languages/csharp.ts';
import { markup } from '../../src/languages/markup.ts';
import { python } from '../../src/languages/python.ts';
import { ruleStatus, statusSentence } from '../../src/state/status.ts';
import { config, markupConfig, option, rule, sample } from '../fixtures.ts';

describe('the status of a rule', () => {
	const moderate = rule('ZA0002');
	const strong = rule('ZA0014', { onByDefault: false });

	it('says that a rule the level enables reports at the picked severity, with no line to write', () => {
		expect(ruleStatus(csharp, sample, moderate, config({}))).toEqual({
			text: 'Suggestion. Reports as a suggestion in the IDE; a build never reports it. Enabled from the moderate level.',
			line: null,
			off: false,
		});

		expect(ruleStatus(csharp, sample, moderate, config({ severity: 'error' })).text).toBe(
			'Error. Reports as an error, which fails a build. Enabled from the moderate level.',
		);
	});

	it('says that a rule the level leaves off reports nothing until a line enables it', () => {
		expect(ruleStatus(csharp, sample, strong, config({}))).toEqual({
			text: 'Off at this level: the rule reports nothing until a line enables it. Enabled from the strong level.',
			line: null,
			off: true,
		});
	});

	it('gives the line of a rule the visitor set, and the severity it reports at', () => {
		expect(ruleStatus(csharp, sample, strong, config({ rules: { ZA0014: 'warning' } }))).toEqual({
			text: 'Warning. Reports as a warning in the IDE and in a build. Enabled from the strong level.',
			line: 'dotnet_diagnostic.ZA0014.severity = warning',
			off: false,
		});
	});

	it('words a silent rule and a disabled one apart', () => {
		const silent = ruleStatus(csharp, sample, moderate, config({ rules: { ZA0002: 'silent' } }));
		const none = ruleStatus(csharp, sample, moderate, config({ rules: { ZA0002: 'none' } }));

		expect(silent.text).toMatch(/^Silent\. Enabled without a report; the rule still owns its gap or construct in a pair\./);
		expect(silent.off).toBe(false);
		expect(none.text).toMatch(/^Disabled: the rule reports nothing, and a bulk key never reaches it\./);
		expect(none.line).toBe('dotnet_diagnostic.ZA0002.severity = none');
		expect(none.off).toBe(true);
	});

	it('names the level that never enables an opt-in rule', () => {
		expect(ruleStatus(csharp, sample, rule('ZA0054', { level: 'opt-in', onByDefault: false }), config({ level: 'strong' })).text).toBe(
			'Off at this level: the rule reports nothing until a line enables it. No level enables it.',
		);
	});

	it('adds that stanza-markup applies a rule it enables by default, until the visitor sets one', () => {
		const markup = rule('ZA2001', { onByDefault: false, markupOnByDefault: true, readByMarkup: true });

		expect(ruleStatus(csharp, sample, markup, config({})).text).toBe(
			'Off at this level: the rule reports nothing until a line enables it. Enabled from the strong level. stanza-markup applies it unless a file disables it.',
		);

		expect(ruleStatus(csharp, sample, markup, config({ rules: { ZA2001: 'error' } })).text).toBe(
			'Error. Reports as an error, which fails a build. Enabled from the strong level.',
		);
	});

	it('adds that CSharpier undoes the layout of a rule', () => {
		expect(ruleStatus(csharp, sample, rule('ZA0001', { undoneByCSharpier: true }), config({})).text).toMatch(
			/ CSharpier undoes the layout this rule writes\.$/,
		);
	});

	it('says what the csharpier formatter does to the rule at the strong level', () => {
		const undone = rule('ZA0001', { level: 'strong', csharpier: 'off' });
		const status = ruleStatus(csharp, sample, undone, config({ level: 'strong', formatter: 'csharpier' }));

		expect(status.off).toBe(true);
		expect(status.text).toMatch(/Enabled from the strong level; the csharpier formatter turns it off at the strong level\.$/);
	});

	it('gives the line of the section that differs, so a rule only stanza-markup applies has none while that section is off', () => {
		const markup = rule('ZA2001', { onByDefault: false, markupOnByDefault: true, readByMarkup: true });

		expect(ruleStatus(csharp, sample, markup, config({ rules: { ZA2001: 'none' } })).line).toBe('dotnet_diagnostic.ZA2001.severity = none');
		expect(ruleStatus(csharp, sample, markup, config({ rules: { ZA2001: 'none' }, markupSection: false })).line).toBeNull();
	});

	it('words a rule of a language with no severity by enabled or disabled, with the state said once', () => {
		const enabled = ruleStatus(python, sample, moderate, config({ severity: 'error' }));

		expect(enabled.text).toBe('Enabled. stanza-py reports what the rule finds, and fix writes it. Enabled from the moderate level.');

		expect(ruleStatus(python, sample, moderate, config({ severity: 'error', rules: { ZA0002: 'none' } })).text).toMatch(
			/^Disabled: the rule reports nothing./,
		);
	});
});

describe('the status of a rule on the Markup page', () => {
	const markupRule = { level: 'no-level', onByDefault: false, readByMarkup: true } as const;
	const applied = rule('ZA2001', { ...markupRule, markupOnByDefault: true });
	const unapplied = rule('ZA2002', markupRule);

	it('names no level, and says that stanza-markup applies a rule it applies by default', () => {
		expect(ruleStatus(markup, sample, applied, markupConfig({}))).toEqual({
			text: 'Suggestion. Reports as a suggestion in the analyzer, in the IDE and never in a build; stanza-markup applies the rule at any severity. stanza-markup applies it unless a file disables it.',
			line: 'dotnet_diagnostic.ZA2001.severity = suggestion',
			off: false,
		});
	});

	it('says that a rule off by default reports nothing until a line enables it', () => {
		expect(ruleStatus(markup, sample, unapplied, markupConfig({}))).toEqual({
			text: 'Off: the rule reports nothing until a line enables it.',
			line: null,
			off: true,
		});
	});

	it('gives the line of each rule the visitor set', () => {
		const enabled = ruleStatus(markup, sample, unapplied, markupConfig({ rules: { ZA2002: 'warning' } }));
		const disabled = ruleStatus(markup, sample, applied, markupConfig({ rules: { ZA2001: 'none' } }));

		expect(enabled.line).toBe('dotnet_diagnostic.ZA2002.severity = warning');
		expect(enabled.text).toMatch(/^Warning\. Reports as a warning in the analyzer/);
		expect(disabled.line).toBe('dotnet_diagnostic.ZA2001.severity = none');
		expect(disabled.text).toBe('Disabled: the rule reports nothing, in the analyzer or in stanza-markup.');
	});

	it('adds that CSharpier undoes the layout of ZA2009', () => {
		const slash = rule('ZA2009', { ...markupRule, undoneByCSharpier: true });
		expect(ruleStatus(markup, sample, slash, markupConfig({})).text).toMatch(/ CSharpier undoes the layout this rule writes\.$/);
	});
});

describe('the status of an option', () => {
	const maxLength = option('stanza_max_line_length');
	const written = 'dotnet_diagnostic.x = y';

	it('says that an option the visitor left alone stays at its default, and whether a line is written for it', () => {
		expect(statusSentence(maxLength, config({}), null)).toBe('Left at its default; no line is written.');
		expect(statusSentence(maxLength, config({}), written)).toBe('Left at its default; Every setting is on, so it is written.');
	});

	it('says that a value other than the default is a change, with or without a line', () => {
		const changed = config({ options: { stanza_max_line_length: '120' } });

		expect(statusSentence(maxLength, changed, written)).toBe('Set to a value other than the default.');
		expect(statusSentence(maxLength, changed, null)).toBe('Set to a value other than the default.');
	});

	it('says that a value equal to the default is no change, and whether a line is written for it', () => {
		const same = config({ options: { stanza_max_line_length: '100' } });

		expect(statusSentence(maxLength, same, null)).toBe('Set to the default value; no line is written.');
		expect(statusSentence(maxLength, same, written)).toBe('Set to the default value; Every setting is on, so it is written.');
	});
});
