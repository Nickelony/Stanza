import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import rule from '../../src/rules/keep-jump-with-binding.ts';
import { createRuleTester, crlf, fix, lines, readPresetRules, readSilencedRuleIds, settled, stable } from '../helpers.ts';

createRuleTester().run('keep-jump-with-binding', rule as never, {
	valid: [
		{ code: lines('function f(a) {', '  log(a);', '', '  const b = load(a);', '  return b;', '}') },
		{ code: lines('function f(a) {', '  log(a);', '  const b = load(a);', '', '  return b;', '}'), name: 'a binding inside a stanza is not alone' },

		{
			code: lines('function f(a) {', '  log(a);', '', '  const b = load(a);', '', '  // the result', '  return b;', '}'),
			name: 'a comment keeps the blank line',
		},

		{
			code: lines('function f(a) {', '  log(a);', '', '  const b = load(a); // load', '', '  return b;', '}'),
			name: 'a trailing comment keeps the blank line',
		},

		{ code: lines('function f(a) {', '  log(a);', '', '  const b = load(a);', '', '  return a;', '}'), name: 'the jump uses another name' },
		{ code: lines('function f(a) {', '  log(a);', '', '  const b = load(a);', '', '  return;', '}'), name: 'a bare return uses no name' },
		{ code: lines('for (const a of list) {', '  log(a);', '', '  const b = load(a);', '', '  continue;', '}'), name: 'a continue uses no name' },
		{ code: lines('function f(a) {', '  log(a);', '', '  const b = load(a);', '', '  return {', '    b,', '  };', '}'), name: 'a multi-line jump' },

		{
			code: lines('function f(a) {', '  log(a);', '', '  const b = load({', '    a,', '  });', '', '  return b;', '}'),
			name: 'a multi-line binding',
		},

		{ code: lines('function f(a) {', '  log(a);', '', '  run(a);', '', '  return a;', '}'), name: 'a call binds nothing' },
		{ code: lines('function f(a) {', '  log(a);', '', '  const b = load(a);', '', '  return a.b;', '}'), name: 'a property is no variable' },
	],

	invalid: [
		{
			code: lines('function f(a) {', '  log(a);', '', '  const b = load(a);', '', '  return b;', '}'),
			output: lines('function f(a) {', '  log(a);', '', '  const b = load(a);', '  return b;', '}'),
			errors: [{ messageId: 'extraBlankLine' }],
		},

		{
			code: lines('function f(a) {', '  const error = new Failure(a);', '', '', '  throw wrap(error);', '  log(a);', '}'),
			output: lines('function f(a) {', '  const error = new Failure(a);', '  throw wrap(error);', '  log(a);', '}'),
			errors: 1,
			name: 'a binding that opens its block',
		},

		{
			code: lines('log(a);', '', 'result = { ...a };', '', 'throw result;'),
			output: lines('log(a);', '', 'result = { ...a };', 'throw result;'),
			errors: 1,
		},

		{
			code: crlf(lines('function f(a) {', '  log(a);', '', '  const [b] = a;', '', '  return b;', '}')),
			output: crlf(lines('function f(a) {', '  log(a);', '', '  const [b] = a;', '  return b;', '}')),
			errors: 1,
		},
	],
});

describe('keep-jump-with-binding beside separate-jump-statements', () => {
	const rules = { 'separate-jump-statements': 'error', 'keep-jump-with-binding': 'error' } as const;

	it('keeps a lone binding with its jump, and settles', () => {
		const code = lines('function f(a) {', '  log(a);', '  run(a);', '', '  const b = load(a);', '', '  return b;', '}');
		assert.equal(stable(code, rules), lines('function f(a) {', '  log(a);', '  run(a);', '', '  const b = load(a);', '  return b;', '}'));
	});

	it('keeps the jump apart from a stanza of two lines that ends with the binding', () => {
		const code = lines('function f(a) {', '  log(a);', '  const b = load(a);', '', '  return b;', '}');

		assert.equal(stable(code, rules), code);
		assert.equal(stable(lines('function f(a) {', '  log(a);', '  const b = load(a);', '  return b;', '}'), rules), code);
	});

	it('settles at the strong level with every opt-in rule enabled', () => {
		const code = lines('function f(a) {', '  log(a);', '  run(a);', '', '  const b = load(a);', '', '  return b;', '}');
		const fixed = stable(code, readPresetRules('strong', true));

		assert.ok(fixed.includes('  const b = load(a);\n  return b;'), fixed);
	});

	it('joins a binding whose upper gap a separation keeps out of every loose run at the strong level', () => {
		const code = lines('function f() {', "  print('test');", '', "  print('more');", '', '  const total = load();', '', '  return total;', '}');
		assert.equal(settled(code, { ...readPresetRules('strong'), 'keep-jump-with-binding': 'error' }), code.replace('load();\n\n', 'load();\n'));
	});
});

describe('keep-jump-with-binding under a directive', () => {
	const keeper = { 'keep-jump-with-binding': 'error' } as const;

	it('reports a silenced gap without a fix, so the eslint-disable comment counts as used', () => {
		const source = lines(
			'/* eslint-disable stanza/keep-jump-with-binding */',
			'function f(a) {',
			'  log(a);',
			'',
			'  const b = load(a);',
			'',
			'  return b;',
			'}',
		);

		assert.deepEqual(readSilencedRuleIds(source, keeper), ['stanza/keep-jump-with-binding']);
		assert.equal(fix(source, keeper), source);
	});
});
