import { describe, expect, it } from 'vitest';

import {
	changedCount,
	checkOption,
	effectiveState,
	emptyConfig,
	isEnabledByFile,
	isOptionChanged,
	isRuleChanged,
	optionValueAt,
	ruleDeltaIn,
	withOption,
	withoutChanges,
	withRule,
} from '../../src/state/config.ts';
import { optionId } from '../../src/state/model.ts';
import { catalog, markupConfig, option, realMarkup, sample } from '../fixtures.ts';

describe('a rule setting', () => {
	const two = sample.rules[0]!;
	const fourteen = sample.rules[1]!;

	it('counts a setting that differs from the level, and no setting that says what the level says', () => {
		const set = (id: string, severity: 'suggestion' | 'warning' | 'none'): ReturnType<typeof emptyConfig> => ({
			...emptyConfig(),
			rules: { [id]: severity },
		});

		// ZA0002 is moderate, so the default level enables it as a suggestion; ZA0014 is strong, so the default level leaves it off.
		expect(isRuleChanged(two, emptyConfig())).toBe(false);
		expect(isRuleChanged(two, set('ZA0002', 'suggestion'))).toBe(false);
		expect(isRuleChanged(two, set('ZA0002', 'warning'))).toBe(true);
		expect(isRuleChanged(two, set('ZA0002', 'none'))).toBe(true);
		expect(isRuleChanged(fourteen, set('ZA0014', 'none'))).toBe(false);
		expect(isRuleChanged(fourteen, set('ZA0014', 'suggestion'))).toBe(true);
	});

	it('reads the state from the level, and from the setting once there is one', () => {
		expect(effectiveState(two, emptyConfig())).toBe('suggestion');
		expect(effectiveState(fourteen, emptyConfig())).toBe('off');
		expect(effectiveState(fourteen, { ...emptyConfig(), rules: { ZA0014: 'silent' } })).toBe('silent');
		expect(effectiveState(two, { ...emptyConfig(), rules: { ZA0002: 'none' } })).toBe('none');
		expect(effectiveState(fourteen, { ...emptyConfig(), level: 'strong', severity: 'error' })).toBe('error');
	});

	it('counts what differs from the level and the defaults', () => {
		const config = {
			...emptyConfig(),
			rules: { ZA0002: 'warning' as const, ZA0014: 'suggestion' as const },
			options: { stanza_max_line_length: '100', stanza_doc_indent_size: '2' },
		};

		expect(changedCount(sample, config)).toBe(3);
	});
});

describe('a markup rule on the page with no levels', () => {
	const [applied, unapplied] = realMarkup.rules;

	it('starts from what the tool applies, for the analyzer as well, since no level holds the rule', () => {
		expect(effectiveState(applied!, markupConfig({}))).toBe('suggestion');
		expect(effectiveState(unapplied!, markupConfig({}))).toBe('off');
		expect(isRuleChanged(applied!, markupConfig({}))).toBe(false);
	});

	it('counts a setting that differs from that start, and none that says what it already says', () => {
		const set = (id: string, severity: 'suggestion' | 'warning' | 'none') => markupConfig({ rules: { [id]: severity } });

		expect(isRuleChanged(applied!, set('ZA2001', 'suggestion'))).toBe(false);
		expect(isRuleChanged(applied!, set('ZA2001', 'warning'))).toBe(true);
		expect(isRuleChanged(applied!, set('ZA2001', 'none'))).toBe(true);
		expect(isRuleChanged(unapplied!, set('ZA2002', 'none'))).toBe(false);
		expect(isRuleChanged(unapplied!, set('ZA2002', 'suggestion'))).toBe(true);
	});

	it('writes a line for the analyzer wherever the rule is on, and for the tool only where it differs from the tool', () => {
		expect(ruleDeltaIn(applied!, markupConfig({}), 'analyzer')).toBe('suggestion');
		expect(ruleDeltaIn(applied!, markupConfig({}), 'markup')).toBeNull();
		expect(ruleDeltaIn(applied!, markupConfig({ rules: { ZA2001: 'none' } }), 'analyzer')).toBeNull();
		expect(ruleDeltaIn(applied!, markupConfig({ rules: { ZA2001: 'none' } }), 'markup')).toBe('none');
		expect(ruleDeltaIn(unapplied!, markupConfig({ rules: { ZA2002: 'error' } }), 'analyzer')).toBe('error');
		expect(ruleDeltaIn(unapplied!, markupConfig({ rules: { ZA2002: 'error' } }), 'markup')).toBe('error');
	});

	it('is enabled by the file wherever it is on, since the page writes its line', () => {
		expect(isEnabledByFile(applied!, markupConfig({}))).toBe(true);
		expect(isEnabledByFile(unapplied!, markupConfig({}))).toBe(false);
		expect(isEnabledByFile(applied!, markupConfig({ rules: { ZA2001: 'none' } }))).toBe(false);
	});
});

describe('an option value', () => {
	it('leaves an empty input at the default', () => {
		expect(checkOption(sample.options[0]!, '  ')).toEqual({ ok: true, value: null });
	});

	it('accepts a whole number inside the bounds and normalizes it', () => {
		expect(checkOption(sample.options[0]!, '120')).toEqual({ ok: true, value: '120' });
		expect(checkOption(sample.options[0]!, '007')).toEqual({ ok: true, value: '7' });
		expect(checkOption(sample.options[0]!, '1000')).toEqual({ ok: true, value: '1000' });
	});

	it('refuses a number outside the bounds, a decimal, and text', () => {
		for (const raw of ['1001', '-1', '1.5', 'wide']) {
			expect(checkOption(sample.options[0]!, raw).ok, raw).toBe(false);
		}
	});

	it('accepts a word the page allows beside the numbers', () => {
		expect(checkOption(sample.options[2]!, 'tab')).toEqual({ ok: true, value: 'tab' });
		expect(checkOption(sample.options[2]!, 'space').ok).toBe(false);
	});

	it('accepts only the values of an enum', () => {
		expect(checkOption(sample.options[1]!, 'body')).toEqual({ ok: true, value: 'body' });
		expect(checkOption(sample.options[1]!, 'Body').ok).toBe(false);
	});

	it('trims a list, drops empty entries, and refuses a character that ends an .editorconfig value', () => {
		expect(checkOption(sample.options[3]!, ' Verify ,, Assert ')).toEqual({ ok: true, value: 'Verify, Assert' });
		expect(checkOption(sample.options[3]!, 'Verify # note').ok).toBe(false);
		expect(checkOption(sample.options[3]!, 'a;b').ok).toBe(false);
	});

	it('accepts only the words of a list whose page closes the set, and any text for a list that does not', () => {
		const sources = option('named-argument-sources', {
			kind: 'list',
			choices: ['project', 'stdlib', 'installed'],
			defaultValue: 'project, stdlib, installed',
		});

		expect(checkOption(sources, ' stdlib ,, project ')).toEqual({ ok: true, value: 'stdlib, project' });
		expect(checkOption(sources, 'project, vendored')).toEqual({ ok: false, reason: 'Use project, stdlib, installed, separated by commas.' });
		expect(checkOption(sources, '  ')).toEqual({ ok: true, value: null });
		expect(checkOption(sample.options[3]!, 'anything, goes')).toEqual({ ok: true, value: 'anything, goes' });
	});

	it('refuses a tab, a line separator and any other control character inside a value', () => {
		const controls = [0x09, 0x0b, 0x1b, 0x7f, 0x85, 0x2028, 0x2029].map((code) => `a${String.fromCharCode(code)}b`);

		for (const raw of controls) {
			for (const target of [sample.options[1]!, sample.options[3]!]) {
				expect(checkOption(target, raw).ok, `${target.key} ${raw.charCodeAt(1)}`).toBe(false);
			}
		}

		expect(checkOption(sample.options[3]!, 'a b').ok).toBe(true);
	});
});

describe('two options of one key on different rules', () => {
	const scoped = (scope: string) =>
		option('objectMembers', { kind: 'enum', choices: ['true', 'false'], defaultValue: 'true', scope, rules: [scope], global: false });

	const first = scoped('keep-single');
	const second = scoped('separate-multi');
	const both = catalog([], [first, second]);
	const set = { ...emptyConfig(), options: { 'separate-multi.objectMembers': 'false' } };

	it('reads and counts each by the rule it belongs to', () => {
		expect(optionValueAt(first, set)).toBe('true');
		expect(optionValueAt(second, set)).toBe('false');
		expect(isOptionChanged(first, set)).toBe(false);
		expect(isOptionChanged(second, set)).toBe(true);
		expect(changedCount(both, set)).toBe(1);
		expect(changedCount(both, { ...set, options: { ...set.options, 'keep-single.objectMembers': 'false' } })).toBe(2);
	});

	it('is set apart from the other by the identity, which the key alone does not give', () => {
		expect(optionId(first)).toBe('keep-single.objectMembers');
		expect(optionId(second)).toBe('separate-multi.objectMembers');
		expect(optionId(option('assertionRoots'))).toBe('assertionRoots');
		expect(withOption(emptyConfig(), optionId(first), 'false').options).toEqual({ 'keep-single.objectMembers': 'false' });
	});
});

describe('a change to the configuration', () => {
	const base = { ...emptyConfig(), level: 'strong' as const, severity: 'error' as const, everySetting: true, markupSection: false };

	it('sets a rule and leaves the others and the original configuration as they were', () => {
		const before = { ...base, rules: { ZA0001: 'none' as const } };
		const after = withRule(before, 'ZA0002', 'warning');

		expect(after.rules).toEqual({ ZA0001: 'none', ZA0002: 'warning' });
		expect(before.rules).toEqual({ ZA0001: 'none' });
		expect(after).toMatchObject({ level: 'strong', severity: 'error', everySetting: true, markupSection: false });
	});

	it('puts a rule back to the level with default', () => {
		const before = { ...base, rules: { ZA0001: 'none' as const, ZA0002: 'warning' as const } };

		expect(withRule(before, 'ZA0001', 'default').rules).toEqual({ ZA0002: 'warning' });
		expect(before.rules).toEqual({ ZA0001: 'none', ZA0002: 'warning' });
	});

	it('sets an option and puts it back to its default with null', () => {
		const before = { ...base, options: { maxLength: '100' } };
		const set = withOption(before, 'jsxChildren', 'false');

		expect(set.options).toEqual({ maxLength: '100', jsxChildren: 'false' });
		expect(withOption(set, 'maxLength', null).options).toEqual({ jsxChildren: 'false' });
		expect(before.options).toEqual({ maxLength: '100' });
	});

	it('drops every rule and option and keeps the pickers and the checkboxes', () => {
		const before = { ...base, rules: { ZA0001: 'none' as const }, options: { maxLength: '100' } };

		expect(withoutChanges(before)).toEqual(base);
		expect(before.rules).toEqual({ ZA0001: 'none' });
	});
});
