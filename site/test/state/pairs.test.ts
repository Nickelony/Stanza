import { describe, expect, it } from 'vitest';

import { findConflicts } from '../../src/editorconfig/conflicts.ts';
import { python } from '../../src/languages/python.ts';
import { effectiveState, emptyConfig, isRuleChanged, levelState, replacedBy, withLevel, type Config } from '../../src/state/config.ts';
import { ruleDefaultLabel } from '../../src/state/labels.ts';
import { ruleStatus } from '../../src/state/status.ts';
import { generatePyproject, pyprojectRuleLines } from '../../src/writers/pyproject.ts';
import { realPython } from '../fixtures.ts';

const rule = (id: string) => realPython.rules.find((candidate) => candidate.id === id)!;
const configOf = (changes: Partial<Config>): Config => ({ ...emptyConfig('error'), ...changes });
const written = (changes: Partial<Config>): string => generatePyproject(realPython, configOf(changes));

describe('a half of a Python pair that the level enables', () => {
	it('is off once the form enables its opposite, as the tool takes it out of the level', () => {
		const config = configOf({ rules: { ZA3112: 'error' } });

		expect(replacedBy(rule('ZA3111'), config)).toBe('ZA3112');
		expect(levelState(rule('ZA3111'), config)).toBe('off');
		expect(effectiveState(rule('ZA3111'), config)).toBe('off');
		expect(effectiveState(rule('ZA3112'), config)).toBe('error');
	});

	it('stays on while the opposite is unset, disabled or the rule is explicit', () => {
		expect(effectiveState(rule('ZA3111'), configOf({}))).toBe('error');
		expect(effectiveState(rule('ZA3111'), configOf({ rules: { ZA3112: 'none' } }))).toBe('error');
		expect(effectiveState(rule('ZA3111'), configOf({ rules: { ZA3112: 'error', ZA3111: 'error' } }))).toBe('error');
		expect(replacedBy(rule('ZA3112'), configOf({ rules: { ZA3111: 'error' } }))).toBeNull();
	});

	it('is replaced only where the level holds it, so the clause pair follows its own default half', () => {
		const minimal = configOf({ level: 'minimal', rules: { ZA3201: 'error' } });
		const none = configOf({ level: 'none', rules: { ZA3201: 'error' } });

		expect(effectiveState(rule('ZA3202'), minimal)).toBe('off');
		expect(replacedBy(rule('ZA3202'), minimal)).toBe('ZA3201');
		expect(effectiveState(rule('ZA3202'), none)).toBe('off');
		expect(replacedBy(rule('ZA3202'), none)).toBeNull();
	});

	it('leaves the rules in no pair alone', () => {
		expect(replacedBy(rule('ZA3001'), configOf({ rules: { ZA3112: 'error' } }))).toBeNull();
	});

	it('is a change only when it differs from what the replaced level gives', () => {
		expect(isRuleChanged(rule('ZA3111'), configOf({ rules: { ZA3112: 'error' } }))).toBe(false);
		expect(isRuleChanged(rule('ZA3111'), configOf({ rules: { ZA3112: 'error', ZA3111: 'error' } }))).toBe(true);
		expect(isRuleChanged(rule('ZA3111'), configOf({ rules: { ZA3111: 'error' } }))).toBe(false);
	});

	it('is back on when the level changes, because the visitor settings are dropped', () => {
		const config = withLevel(configOf({ rules: { ZA3112: 'error' } }), 'strong');
		expect(effectiveState(rule('ZA3111'), config)).toBe('error');
	});
});

describe('the table the page writes for a pair', () => {
	it('lists only the enabled half, because the tool turns the other half off', () => {
		expect(written({ rules: { ZA3112: 'error' } })).toBe('[tool.stanza]\nenable = ["ZA3112"]\n');
		expect(written({ level: 'minimal', rules: { ZA3201: 'error' } })).toBe('[tool.stanza]\nlevel = "minimal"\nenable = ["ZA3201"]\n');
		expect(written({ level: 'none', rules: { ZA3112: 'error' } })).toBe('[tool.stanza]\nlevel = "none"\nenable = ["ZA3112"]\n');
	});

	it('lists the default half too when the visitor enables both', () => {
		expect(written({ rules: { ZA3112: 'error', ZA3111: 'error' } })).toBe('[tool.stanza]\nenable = ["ZA3111", "ZA3112"]\n');

		expect(written({ level: 'minimal', rules: { ZA3201: 'error', ZA3202: 'error' } })).toBe(
			'[tool.stanza]\nlevel = "minimal"\nenable = ["ZA3202", "ZA3201"]\n',
		);

		expect(written({ level: 'strong', rules: { ZA3112: 'error', ZA3111: 'error' } })).toBe(
			'[tool.stanza]\nlevel = "strong"\nenable = ["ZA3111", "ZA3112"]\n',
		);
	});

	it('writes no line for the replaced half, and disable for a default half the visitor turns off', () => {
		expect(written({ rules: { ZA3112: 'error', ZA3111: 'none' } })).toBe('[tool.stanza]\nenable = ["ZA3112"]\n');
		expect(written({ rules: { ZA3111: 'none' } })).toBe('[tool.stanza]\ndisable = ["ZA3111"]\n');
		expect(written({ rules: { ZA3112: 'none' } })).toBe('[tool.stanza]\n');
	});

	it('shows the list the rule joins, with both halves in it', () => {
		const both = configOf({ rules: { ZA3112: 'error', ZA3111: 'error' } });

		expect(pyprojectRuleLines(realPython, rule('ZA3111'), both)).toEqual(['enable = ["ZA3111", "ZA3112"]']);
		expect(pyprojectRuleLines(realPython, rule('ZA3111'), configOf({ rules: { ZA3112: 'error' } }))).toEqual([]);
	});
});

describe('what the page says of a half that is replaced', () => {
	const replaced = configOf({ rules: { ZA3112: 'error' } });

	it('names the opposite in the label of the level choice', () => {
		expect(ruleDefaultLabel(rule('ZA3111'), replaced, python)).toBe('Level (Off; ZA3112 is enabled)');
		expect(ruleDefaultLabel(rule('ZA3111'), configOf({}), python)).toBe('Level (Enabled)');
		expect(ruleDefaultLabel(rule('ZA3112'), configOf({}), python)).toBe('Level (Off)');
	});

	it('says in the status box that the opposite owns the gap and enable can list both', () => {
		const status = ruleStatus(python, realPython as never, rule('ZA3111') as never, replaced);

		expect(status.off).toBe(true);
		expect(status.text).toContain('ZA3112 is enabled and owns the gap');
		expect(status.text).toContain('unless enable lists it too');
	});
});

describe('the conflicts of a Python pair', () => {
	it('name the tight half as the owner when the visitor enables both', () => {
		const found = findConflicts(realPython, configOf({ rules: { ZA3112: 'error', ZA3111: 'error' } }));

		expect(found).toHaveLength(1);
		expect(found[0]).toMatchObject({ kind: 'pair', ids: ['ZA3111', 'ZA3112'], owner: 'ZA3112' });
	});

	it('are none while one half replaces the other', () => {
		expect(findConflicts(realPython, configOf({ rules: { ZA3112: 'error' } }))).toEqual([]);
		expect(findConflicts(realPython, configOf({ level: 'minimal', rules: { ZA3201: 'error' } }))).toEqual([]);
		expect(findConflicts(realPython, configOf({}))).toEqual([]);
	});

	it('name the clause pair with its tight half as the owner', () => {
		const found = findConflicts(realPython, configOf({ level: 'minimal', rules: { ZA3201: 'error', ZA3202: 'error' } }));
		expect(found.map((conflict) => [conflict.ids, conflict.owner])).toEqual([[['ZA3201', 'ZA3202'], 'ZA3202']]);
	});
});
