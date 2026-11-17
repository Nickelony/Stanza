import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import rule from '../../src/rules/keep-guard-clauses-together.ts';
import { createRuleTester, crlf, fix, lines, readPresetRules, readSilencedRuleIds, stable } from '../helpers.ts';

createRuleTester().run('keep-guard-clauses-together', rule as never, {
	valid: [
		{ code: lines('function f(a, b) {', '  if (!a) return;', '  if (!b) return;', '  run();', '}') },

		{
			code: lines('function f(a, b) {', '  if (!a) return;', '', '  // b comes second', '  if (!b) return;', '  run();', '}'),
			name: 'a comment keeps the blank line',
		},

		{
			code: lines('function f(a, b) {', '  if (!a) return; // a', '', '  if (!b) return;', '  run();', '}'),
			name: 'a trailing comment keeps the blank line',
		},

		{
			code: lines('function f(a, b) {', '  if (!a) return;', '', '  if (!b) return;', '  else run();', '  go();', '}'),
			name: 'an if with an else is no guard',
		},

		{
			code: lines('function f(a, b) {', '  if (!a) return;', '', '  if (!b) {', '    log(b);', '    return;', '  }', '  go();', '}'),
			name: 'an if with two statements is no guard',
		},

		{ code: lines('function f(a) {', '  if (!a) return;', '', '  run();', '  go();', '}') },
		{ code: lines('function f(a) {', '  log(a);', '', '  if (!a) return;', '  go();', '}') },
		{ code: lines('function f(a, b) {', '  check(a);', '', '  check(b);', '  run();', '}'), name: 'a call no guard root lists is no guard' },
	],

	invalid: [
		{
			code: lines('if (!a) throw error;', '', 'if (!b) throw error;', 'run();'),
			output: lines('if (!a) throw error;', 'if (!b) throw error;', 'run();'),
			errors: 1,
			name: 'the top level of a file',
		},

		{
			code: lines('switch (kind) {', '  case 1:', '    if (!a) break;', '', '    if (!b) break;', '    run();', '}'),
			output: lines('switch (kind) {', '  case 1:', '    if (!a) break;', '    if (!b) break;', '    run();', '}'),
			errors: 1,
			name: 'a switch case',
		},

		{
			code: lines('function f(a, b) {', '  run();', '  if (!a) return;', '', '  if (!b) return;', '  go();', '}'),
			output: lines('function f(a, b) {', '  run();', '  if (!a) return;', '  if (!b) return;', '  go();', '}'),
			errors: [{ messageId: 'extraBlankLine' }],
		},

		{
			code: lines('function f(a, b) {', '  if (!a) {', '    return;', '  }', '', '', '  if (!b) {', '    throw new Error();', '  }', '  go();', '}'),
			output: lines('function f(a, b) {', '  if (!a) {', '    return;', '  }', '  if (!b) {', '    throw new Error();', '  }', '  go();', '}'),
			errors: 1,
		},

		{
			code: lines('for (const a of list) {', '  if (!a) continue;', '', '  if (a.done) break;', '', '  invariant(a.ok);', '  use(a);', '}'),
			output: lines('for (const a of list) {', '  if (!a) continue;', '  if (a.done) break;', '  invariant(a.ok);', '  use(a);', '}'),
			errors: 2,
		},

		{
			code: lines('function f(a, b) {', '  check(a);', '', '  check(b);', '  run();', '}'),
			output: lines('function f(a, b) {', '  check(a);', '  check(b);', '  run();', '}'),
			settings: { stanza: { guardRoots: ['check'] } },
			errors: 1,
		},

		{
			code: crlf(lines('function f(a, b) {', '  if (!a) return;', '', '  if (!b) return;', '  run();', '}')),
			output: crlf(lines('function f(a, b) {', '  if (!a) return;', '  if (!b) return;', '  run();', '}')),
			errors: 1,
		},
	],
});

describe('keep-guard-clauses-together beside the rules that add a blank line', () => {
	const tight = lines('function f(a, b) {', '  if (!a) {', '    return;', '  }', '  if (!b) {', '    return;', '  }', '', '  run();', '  go();', '}');
	const apart = lines('function f(a, b) {', '  if (!a) {', '    return;', '  }', '', '  if (!b) {', '    return;', '  }', '  run();', '  go();', '}');

	it('wins the gap between two braced guards over separate-multi-line-constructs, and settles', () => {
		const rules = { 'separate-multi-line-constructs': 'error', 'keep-guard-clauses-together': 'error' } as const;

		assert.equal(stable(apart, rules), tight);
		assert.equal(stable(tight, rules), tight);
		assert.equal(fix(apart, { 'separate-multi-line-constructs': 'error' }), apart.replace('  }\n  run', '  }\n\n  run'));
	});

	it('settles at the strong level with the rule enabled', () => {
		const rules = { ...readPresetRules('strong'), 'keep-guard-clauses-together': 'error' } as const;
		const fixed = stable(apart, rules);

		assert.ok(fixed.includes('  }\n  if (!b) {'), fixed);
	});
});

describe('keep-guard-clauses-together beside assertions', () => {
	const keeper = { 'keep-guard-clauses-together': 'error' } as const;

	it('leaves the blank line above a multi-line assertion', () => {
		const source = lines('function f() {', ' assert(value);', '', ' assert(', '  other,', ' );', ' use(value);', '}');
		assert.equal(stable(source, keeper), source);
	});

	it('joins two single-line assertions, which are guards', () => {
		const source = lines('function f() {', ' assert(value);', '', ' assert(other);', ' use(value);', '}');
		assert.equal(stable(source, keeper), source.replace('assert(value);\n\n', 'assert(value);\n'));
	});

	it('reports a silenced gap without a fix, so the eslint-disable comment counts as used', () => {
		const source = lines(
			'/* eslint-disable stanza/keep-guard-clauses-together */',
			'function f(a, b) {',
			'  if (!a) return;',
			'',
			'  if (!b) return;',
			'  run();',
			'}',
		);

		assert.deepEqual(readSilencedRuleIds(source, keeper), ['stanza/keep-guard-clauses-together']);
		assert.equal(fix(source, keeper), source);
	});
});
