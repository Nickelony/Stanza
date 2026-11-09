import { describe, expect, it } from 'vitest';

import { csharp } from '../../src/languages/csharp.ts';
import { markup } from '../../src/languages/markup.ts';
import { optionDefaultLabel, ruleDefaultLabel, summaryText } from '../../src/state/labels.ts';
import { config, option, rule } from '../fixtures.ts';

describe('the summary of the form', () => {
	it('names the level and the changes against it', () => {
		expect(summaryText('moderate', 0)).toBe('Moderate, no changes');
		expect(summaryText('moderate', 1)).toBe('Moderate plus 1 change');
		expect(summaryText('strong', 2)).toBe('Strong plus 2 changes');
		expect(summaryText('none', 3)).toBe('None plus 3 changes');
	});

	it('counts the changes alone on a page with no levels', () => {
		expect(summaryText(null, 0)).toBe('No changes');
		expect(summaryText(null, 1)).toBe('1 change');
		expect(summaryText(null, 4)).toBe('4 changes');
	});
});

describe('the label of a rule that follows the level', () => {
	const moderate = rule('ZA0002');
	const strong = rule('ZA0014', { onByDefault: false });

	it('names the severity the level gives the rule', () => {
		expect(ruleDefaultLabel(moderate, config({}), csharp)).toBe('Level (Suggestion)');
		expect(ruleDefaultLabel(moderate, config({ severity: 'warning' }), csharp)).toBe('Level (Warning)');
		expect(ruleDefaultLabel(moderate, config({ severity: 'error' }), csharp)).toBe('Level (Error)');
	});

	it('says Off for a rule the level leaves off', () => {
		expect(ruleDefaultLabel(strong, config({}), csharp)).toBe('Level (Off)');
		expect(ruleDefaultLabel(moderate, config({ level: 'none' }), csharp)).toBe('Level (Off)');
		expect(ruleDefaultLabel(moderate, config({ level: 'minimal' }), csharp)).toBe('Level (Off)');
	});

	it('follows the level at which the rule is on', () => {
		expect(ruleDefaultLabel(strong, config({ level: 'strong', severity: 'warning' }), csharp)).toBe('Level (Warning)');
	});

	it('says that stanza-markup applies a rule the level leaves off', () => {
		expect(ruleDefaultLabel(rule('ZA2001', { onByDefault: false, markupOnByDefault: true, readByMarkup: true }), config({}), csharp)).toBe(
			'Level (Off; on in stanza-markup)',
		);
	});

	it('follows what the csharpier formatter does to the strong level', () => {
		const undone = rule('ZA0001', { level: 'strong', csharpier: 'off' });
		const added = rule('ZA0054', { level: 'opt-in', onByDefault: false, csharpier: 'on' });

		expect(ruleDefaultLabel(undone, config({ level: 'strong' }), csharp)).toBe('Level (Suggestion)');
		expect(ruleDefaultLabel(undone, config({ level: 'strong', formatter: 'csharpier' }), csharp)).toBe('Level (Off)');
		expect(ruleDefaultLabel(added, config({ level: 'strong' }), csharp)).toBe('Level (Off)');
		expect(ruleDefaultLabel(added, config({ level: 'strong', formatter: 'csharpier' }), csharp)).toBe('Level (Suggestion)');
	});
});

describe('the label of a markup rule that follows the default', () => {
	const applied = rule('ZA2001', { level: 'no-level', onByDefault: false, markupOnByDefault: true, readByMarkup: true });
	const unapplied = rule('ZA2002', { level: 'no-level', onByDefault: false, readByMarkup: true });
	const none = config({ level: 'none' });

	it('names what the tool applies without a line, and no level', () => {
		expect(ruleDefaultLabel(applied, none, markup)).toBe('Default (Suggestion)');
		expect(ruleDefaultLabel(unapplied, none, markup)).toBe('Default (Off)');
	});

	it('names the default whatever the visitor set for the rule', () => {
		expect(ruleDefaultLabel(applied, config({ level: 'none', rules: { ZA2001: 'none' } }), markup)).toBe('Default (Suggestion)');
	});
});

describe('the label of an option left at its default', () => {
	it('names the default, and says plain Default for a derived one', () => {
		expect(optionDefaultLabel(option('stanza_max_line_length', { defaultValue: '100' }))).toBe('Default (100)');
		expect(optionDefaultLabel(option('stanza_assertion_roots', { defaultValue: null }))).toBe('Default');
	});

	it('says that stanza-markup uses another default', () => {
		expect(optionDefaultLabel(option('stanza_a', { audience: 'both', defaultValue: '100', markupDefaultValue: null }))).toBe('Default (100; varies)');

		expect(
			optionDefaultLabel(option('stanza_b', { audience: 'both', defaultValue: '100', markupDefaultValue: null, markupDefaultByProfile: true })),
		).toBe('Default (100; per profile)');
	});
});
