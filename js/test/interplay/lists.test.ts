import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { Linter } from 'eslint';

import { fix, lines, lintWithPreset, readPresetRules } from '../helpers.ts';

describe('the list rules', () => {
	it('leave a pair of elements to one rule: the multi-line pair adds a blank line and the single-line pair removes it', () => {
		const code = lines('const o = {', '  a: 1,', '', '  b: 2,', '  c: {', '    d: 1,', '  },', '};');
		const rules = { 'separate-multi-line-elements': 'error', 'keep-single-line-elements-together': 'error' } as const;

		assert.equal(fix(code, rules), lines('const o = {', '  a: 1,', '  b: 2,', '', '  c: {', '    d: 1,', '  },', '};'));
		assert.equal(fix(fix(code, rules), rules), fix(code, rules));
	});

	// Each sample holds a blank line between two single-line elements, then a multi-line element right after them.
	const samples = [
		{
			option: 'objectMembers',
			joined: lines('const o = {', '  a: 1,', '  b: 2,', '', '  c: {', '    d: 1,', '  },', '};'),
			code: lines('const o = {', '  a: 1,', '', '  b: 2,', '  c: {', '    d: 1,', '  },', '};'),
			tight: lines('const o = {', '  a: 1,', '  b: 2,', '  c: {', '    d: 1,', '  },', '};'),
			apart: lines('const o = {', '  a: 1,', '', '  b: 2,', '', '  c: {', '    d: 1,', '  },', '};'),
		},

		{
			option: 'arrayElements',
			joined: lines('const a = [', '  1,', '  2,', '', '  [', '    3,', '  ],', '];'),
			code: lines('const a = [', '  1,', '', '  2,', '  [', '    3,', '  ],', '];'),
			tight: lines('const a = [', '  1,', '  2,', '  [', '    3,', '  ],', '];'),
			apart: lines('const a = [', '  1,', '', '  2,', '', '  [', '    3,', '  ],', '];'),
		},

		{
			option: 'enumMembers',
			joined: lines('enum E {', '  A = 1,', '  B = 2,', '', '  C = f(', '    3,', '  ),', '}'),
			code: lines('enum E {', '  A = 1,', '', '  B = 2,', '  C = f(', '    3,', '  ),', '}'),
			tight: lines('enum E {', '  A = 1,', '  B = 2,', '  C = f(', '    3,', '  ),', '}'),
			apart: lines('enum E {', '  A = 1,', '', '  B = 2,', '', '  C = f(', '    3,', '  ),', '}'),
		},

		{
			option: 'callArguments',
			joined: lines('create(', '  a,', '  b,', '', '  (key) => {', '    warn(key);', '  },', ');'),
			code: lines('create(', '  a,', '', '  b,', '  (key) => {', '    warn(key);', '  },', ');'),
			tight: lines('create(', '  a,', '  b,', '  (key) => {', '    warn(key);', '  },', ');'),
			apart: lines('create(', '  a,', '', '  b,', '', '  (key) => {', '    warn(key);', '  },', ');'),
		},

		{
			option: 'parameters',
			joined: lines('function make(', '  a: string,', '  b: string,', '', '  c: {', '    d: number;', '  },', ') {}'),
			code: lines('function make(', '  a: string,', '', '  b: string,', '  c: {', '    d: number;', '  },', ') {}'),
			tight: lines('function make(', '  a: string,', '  b: string,', '  c: {', '    d: number;', '  },', ') {}'),
			apart: lines('function make(', '  a: string,', '', '  b: string,', '', '  c: {', '    d: number;', '  },', ') {}'),
		},
	] as const;

	for (const { option, code, joined, tight, apart } of samples) {
		it(`lets ${option} turn one rule off while the other judges the same kind of list`, () => {
			const both = { 'separate-multi-line-elements': 'error', 'keep-single-line-elements-together': 'error' } as const;
			const off: Linter.RuleEntry = ['error', { [option]: false }];

			assert.equal(fix(code, both), joined);
			assert.equal(fix(code, { ...both, 'keep-single-line-elements-together': off }), apart);
			assert.equal(fix(code, { ...both, 'separate-multi-line-elements': off }), tight);
			assert.equal(fix(code, { ...both, 'keep-single-line-elements-together': off, 'separate-multi-line-elements': off }), code);
		});
	}

	it('leaves the other kinds of list to the rule when one option turns a kind off', () => {
		const code = lines('const o = {', '  a: 1,', '', '  b: 2,', '};', 'const a = [', '  1,', '', '  2,', '];');
		const rules: Record<string, Linter.RuleEntry> = { 'keep-single-line-elements-together': ['error', { objectMembers: false }] };

		assert.equal(fix(code, rules), lines('const o = {', '  a: 1,', '', '  b: 2,', '};', 'const a = [', '  1,', '  2,', '];'));
	});

	it('lets an entry over the minimal preset turn a kind off, and the strong preset keeps the other rule', () => {
		for (const { option, code, tight, apart } of samples) {
			const minimal = { ...readPresetRules('minimal'), 'keep-single-line-elements-together': ['error', { [option]: false }] } as Record<
				string,
				Linter.RuleEntry
			>;

			const strong = { ...readPresetRules('strong'), 'keep-single-line-elements-together': ['error', { [option]: false }] } as Record<
				string,
				Linter.RuleEntry
			>;

			assert.equal(fix(code, minimal), code, option);
			assert.equal(fix(code, readPresetRules('minimal')), tight, option);
			assert.equal(fix(code, strong), apart, option);
		}
	});

	it('are off in the moderate preset and on in the strong preset', () => {
		const code = lines('const o = {', '  a: 1,', '  b: {', '    c: 1,', '  },', '};');

		assert.deepEqual(
			lintWithPreset(code, 'moderate').map((message) => message.ruleId),
			[],
		);

		assert.deepEqual(
			lintWithPreset(code, 'strong').map((message) => message.ruleId),
			['stanza/separate-multi-line-elements'],
		);

		assert.ok('separate-multi-line-elements' in readPresetRules('strong'));
	});
});
