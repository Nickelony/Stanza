import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import rule from '../../src/rules/keep-assertion-with-binding.ts';
import { createRuleTester, crlf, fix, lines, readPresetRules, readSilencedRuleIds, stable } from '../helpers.ts';

createRuleTester().run('keep-assertion-with-binding', rule as never, {
	valid: [
		{ code: lines('function f(id) {', '  const order = load(id);', '  assert(order);', '  ship(order);', '}') },

		{
			code: lines('function f(id) {', '  const order = load(id);', '', '  // must exist', '  assert(order);', '  ship(order);', '}'),
			name: 'a comment keeps the blank line',
		},

		{
			code: lines('function f(id) {', '  const order = load(id); // load', '', '  assert(order);', '  ship(order);', '}'),
			name: 'a trailing comment keeps the blank line',
		},

		{
			code: lines('function f(id) {', '  const order = load(id);', '', '  assert.equal(order, 3);', '  ship(order);', '}'),
			name: 'a comparison narrows nothing',
		},

		{
			code: lines('function f(id) {', '  const order = load(id);', '', '  expect(order).toBe(3);', '  ship(order);', '}'),
			name: 'a matcher narrows nothing',
		},

		{
			code: lines('function f(id) {', '  const order = load(id);', '', '  assert(order === null);', '  ship(order);', '}'),
			name: 'an equality with null narrows nothing',
		},

		{
			code: lines('function f(id) {', '  const order = load(id);', '', '  assert(id);', '  ship(order);', '}'),
			name: 'the assertion narrows another name',
		},

		{ code: lines('function f(id) {', '  const order = load(id);', '', '  check(order);', '  ship(order);', '}'), name: 'a call of no root' },

		{
			code: lines('function f(id) {', '  const order = load(id);', '', '  assert(order);', '  assert.equal(order.id, id);', '}'),
			name: 'an assertion that leads the assert phase',
		},

		{
			code: lines('function f(id) {', '  const order = load(id);', '', '  assert(order);', '  invariant(order.ready);', '}'),
			name: 'a guard call below leads the assert phase too',
		},

		{
			code: lines('function f(id) {', '  const order = load(id);', '', '  assert(', '    order,', '  );', '  ship(order);', '}'),
			name: 'a multi-line assertion',
		},
	],

	invalid: [
		{
			code: lines('function f(id) {', '  audit(id);', '  const order = load(id);', '', '  assert(order);', '  ship(order);', '}'),
			output: lines('function f(id) {', '  audit(id);', '  const order = load(id);', '  assert(order);', '  ship(order);', '}'),
			errors: [{ messageId: 'extraBlankLine' }],
		},

		{
			code: lines('audit(id);', 'const order = load({', '  id,', '});', '', 'invariant(order !== undefined && order.ready);', 'ship(order);'),
			output: lines('audit(id);', 'const order = load({', '  id,', '});', 'invariant(order !== undefined && order.ready);', 'ship(order);'),
			errors: 1,
			name: 'a multi-line binding and an and',
		},

		{
			code: lines('audit(id);', 'const { order } = load(id);', '', 'assert.ok(order instanceof Order);', 'ship(order);'),
			output: lines('audit(id);', 'const { order } = load(id);', 'assert.ok(order instanceof Order);', 'ship(order);'),
			errors: 1,
		},

		{
			code: lines('audit(id);', 'order = load(id);', '', "assert(typeof order === 'object');", 'ship(order);'),
			output: lines('audit(id);', 'order = load(id);', "assert(typeof order === 'object');", 'ship(order);'),
			errors: 1,
		},

		{
			code: lines('audit(id);', 'const order = load(id);', '', 'assert(null != order);', 'ship(order);'),
			output: lines('audit(id);', 'const order = load(id);', 'assert(null != order);', 'ship(order);'),
			errors: 1,
		},

		{
			code: lines('audit(id);', 'const order = load(id);', '', 'assert(order);'),
			output: lines('audit(id);', 'const order = load(id);', 'assert(order);'),
			errors: 1,
			name: 'an assertion that closes its container',
		},

		{
			code: crlf(lines('audit(id);', 'const order = load(id);', '', 'check(order != null);', 'ship(order);')),
			output: crlf(lines('audit(id);', 'const order = load(id);', 'check(order != null);', 'ship(order);')),
			settings: { stanza: { assertionRoots: ['check'] } },
			errors: 1,
		},
	],
});

describe('keep-assertion-with-binding beside the rules that add a blank line', () => {
	const code = lines('function f(id) {', '  audit(id);', '  const order = load({', '    id,', '  });', '', '  assert(order);', '  ship(order);', '}');

	const tight = lines(
		'function f(id) {',
		'  audit(id);',
		'',
		'  const order = load({',
		'    id,',
		'  });',
		'  assert(order);',
		'  ship(order);',
		'}',
	);

	it('wins the gap below a multi-line binding over separate-multi-line-constructs, and settles', () => {
		const rules = { 'separate-multi-line-constructs': 'error', 'keep-assertion-with-binding': 'error' } as const;
		assert.equal(stable(code, rules), tight);
	});

	it('settles at the strong level with every opt-in rule enabled', () => {
		const fixed = stable(code, readPresetRules('strong', true));
		assert.ok(fixed.includes('  });\n  assert(order);'), fixed);
	});
});

describe('keep-assertion-with-binding under a directive', () => {
	const keeper = { 'keep-assertion-with-binding': 'error' } as const;

	it('reports a silenced gap without a fix, so the eslint-disable comment counts as used', () => {
		const source = lines(
			'/* eslint-disable stanza/keep-assertion-with-binding */',
			'function f(id) {',
			'  const order = load(id);',
			'',
			'  assert(order);',
			'  ship(order);',
			'}',
		);

		assert.deepEqual(readSilencedRuleIds(source, keeper), ['stanza/keep-assertion-with-binding']);
		assert.equal(fix(source, keeper), source);
	});
});
