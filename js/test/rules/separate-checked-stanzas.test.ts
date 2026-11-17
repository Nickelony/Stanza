import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { createConfig, fix, fixOneByOne, lines, readPresetRules, readSilencedRuleIds, settled, stable } from '../helpers.ts';

const rule = { 'separate-checked-stanzas': 'error' } as const;

describe('separate-checked-stanzas', () => {
	it('sets a guard chain apart at the top level of a file', () => {
		const code = lines('validate(order);', 'if (order.closed) throw error;', 'send(order);');
		assert.equal(settled(code, rule), lines('validate(order);', '', 'if (order.closed) throw error;', '', 'send(order);'));
	});

	it('sets a guard chain apart in a switch case, and the bindings it reads with it', () => {
		const code = lines(
			'switch (kind) {',
			'  case 1:',
			'    validate(order);',
			'    const user = load(order);',
			'    if (!user) break;',
			'    send(user);',
			'    break;',
			'}',
		);

		assert.equal(
			settled(code, rule),

			lines(
				'switch (kind) {',
				'  case 1:',
				'    validate(order);',
				'',
				'    const user = load(order);',
				'    if (!user) break;',
				'',
				'    send(user);',
				'    break;',
				'}',
			),
		);
	});

	it('leaves the gap below the guards that open a block to separate-guard-clauses, and writes the one below the guards that open a switch case', () => {
		const block = lines('function f(a) {', '  if (!a) return;', '  run(a);', '  go(a);', '}');
		const switchCase = lines('switch (kind) {', '  case 1:', '    if (!kind) break;', '    run(kind);', '    go(kind);', '}');

		assert.equal(
			stable(block, { ...rule, 'separate-guard-clauses': 'error' }),
			lines('function f(a) {', '  if (!a) return;', '', '  run(a);', '  go(a);', '}'),
		);

		assert.equal(
			stable(switchCase, rule),
			lines('switch (kind) {', '  case 1:', '    if (!kind) break;', '', '    run(kind);', '    go(kind);', '}'),
		);
	});

	it('reads a file with a shebang', () => {
		const code = lines('#!/usr/bin/env node', 'const order = load();', 'if (!order) throw error;', 'send(order);');
		assert.equal(stable(code, rule), lines('#!/usr/bin/env node', 'const order = load();', 'if (!order) throw error;', '', 'send(order);'));
	});

	it('sets a mid-block single-line guard chain apart on both sides', () => {
		const code = lines(
			'function ship(order) {',
			'  validate(order);',
			'  if (order.closed) return;',
			'  if (!order.items.length) return false;',
			'  send(order);',
			'}',
		);

		assert.equal(
			stable(code, rule),

			lines(
				'function ship(order) {',
				'  validate(order);',
				'',
				'  if (order.closed) return;',
				'  if (!order.items.length) return false;',
				'',
				'  send(order);',
				'}',
			),
		);
	});

	it('includes the contiguous bindings the chain reads and leaves unread bindings above', () => {
		const code = lines(
			'function ship() {',
			'  const service = create();',
			'  const a = service.find();',
			'  const b = other();',
			'  if (!a || !b) return;',
			'  send(a, b);',
			'}',
		);

		assert.equal(
			stable(code, rule),

			lines(
				'function ship() {',
				'  const service = create();',
				'',
				'  const a = service.find();',
				'  const b = other();',
				'  if (!a || !b) return;',
				'',
				'  send(a, b);',
				'}',
			),
		);
	});

	it('keeps two checked bindings whole before a multi-line guard without the keep-run rule', () => {
		const code = lines(
			'function ship() {',
			'  prepare();',
			'  const a = first();',
			'  const b = second();',
			'  if (!a || !b) {',
			'    return;',
			'  }',
			'  use(a, b);',
			'}',
		);

		const expected = lines(
			'function ship() {',
			'  prepare();',
			'',
			'  const a = first();',
			'  const b = second();',
			'  if (!a || !b) {',
			'    return;',
			'  }',
			'',
			'  use(a, b);',
			'}',
		);

		const rules = { ...rule, 'separate-multi-line-constructs': 'error', 'keep-guard-with-binding': 'error' } as const;

		assert.equal(stable(code, rules), expected);
	});

	it('keeps bindings read by different guards in the same checked chain', () => {
		const code = lines(
			'function ship() {',
			'  prepare();',
			'  const a = first();',
			'  const b = second();',
			'  if (!a) return;',
			'  if (!b) return;',
			'  use(a, b);',
			'}',
		);

		const expected = lines(
			'function ship() {',
			'  prepare();',
			'',
			'  const a = first();',
			'  const b = second();',
			'  if (!a) return;',
			'  if (!b) return;',
			'',
			'  use(a, b);',
			'}',
		);

		const rules = { ...rule, 'separate-declaration-runs': 'error', 'keep-guard-with-binding': 'error' } as const;

		assert.equal(stable(code, rules), expected);
	});

	it('separates an opening chain with the opening-guard rule both on and off', () => {
		const code = lines('function ship(order) {', '  if (order.closed) return;', '  if (!order.items.length) return;', '  send(order);', '}');
		const expected = lines('function ship(order) {', '  if (order.closed) return;', '  if (!order.items.length) return;', '', '  send(order);', '}');

		assert.equal(stable(code, rule), expected);
		assert.equal(stable(code, { ...rule, 'separate-guard-clauses': 'error' }), expected);
	});

	it('separates only above a narrowing assertion stanza', () => {
		const code = lines('function ship() {', '  prepare();', '  const x = find();', '  assert(x != null);', '  use(x);', '}');
		assert.equal(stable(code, rule), lines('function ship() {', '  prepare();', '', '  const x = find();', '  assert(x != null);', '  use(x);', '}'));
	});

	it('keeps a mixed narrowing and result assertion run in its test phase', () => {
		const code = lines(
			'function test() {',
			'  log();',
			'  const product = get();',
			'  assert(product != null);',
			'  assert(product.id === 1);',
			'  cleanup();',
			'}',
		);

		assert.equal(stable(code, rule), code);
	});

	it('sets an all-narrowing assertion chain apart above only', () => {
		const code = lines(
			'function test() {',
			'  prepare();',
			'  const x = find();',
			'  const y = other();',
			'  assert(x != null);',
			'  assert(y != null);',
			'  use(x, y);',
			'}',
		);

		const expected = lines(
			'function test() {',
			'  prepare();',
			'',
			'  const x = find();',
			'  const y = other();',
			'  assert(x != null);',
			'  assert(y != null);',
			'  use(x, y);',
			'}',
		);

		assert.equal(stable(code, rule), expected);
	});

	it('keeps a checked narrowing-assertion binding group whole without the keep-run rule', () => {
		const code = lines(
			'function test() {',
			'  prepare();',
			'  const x = find();',
			'  const y = other();',
			'',
			'  assert(x != null);',
			'  assert(y != null);',
			'  use(x, y);',
			'}',
		);

		const expected = lines(
			'function test() {',
			'  prepare();',
			'',
			'  const x = find();',
			'  const y = other();',
			'  assert(x != null);',
			'  assert(y != null);',
			'  use(x, y);',
			'}',
		);

		const rules = { ...rule, 'separate-declaration-runs': 'error', 'keep-assertion-with-binding': 'error' } as const;

		assert.equal(stable(code, rules), expected);
	});

	it('leaves a plain result assertion alone', () => {
		const code = lines('function test() {', '  const result = run();', '  assert(result.status === 200);', '  cleanup();', '}');
		assert.equal(stable(code, rule), code);
	});

	it('keeps a comment above the chain in place', () => {
		const code = lines('function ship(order) {', '  prepare();', '  // Reject closed orders.', '  if (order.closed) return;', '  send(order);', '}');

		assert.equal(
			stable(code, rule),
			lines('function ship(order) {', '  prepare();', '  // Reject closed orders.', '  if (order.closed) return;', '', '  send(order);', '}'),
		);
	});

	it('separates a following jump when its own rule does not claim the gap', () => {
		const code = lines('function ship(order) {', '  prepare();', '  if (order.closed) return;', '  return order;', '}');

		assert.equal(
			stable(code, { ...rule, 'separate-jump-statements': 'error' }),
			lines('function ship(order) {', '  prepare();', '', '  if (order.closed) return;', '', '  return order;', '}'),
		);
	});

	it('ends a declaration run at the checked stanza border', () => {
		const code = lines('function ship() {', '  const service = create();', '  const x = service.find();', '  if (!x) return;', '  use(x);', '}');

		const rules = {
			...rule,
			'separate-declaration-runs': 'error',
			'keep-statement-runs-together': 'error',
			'keep-guard-with-binding': 'error',
		} as const;

		assert.equal(
			stable(code, rules),
			lines('function ship() {', '  const service = create();', '', '  const x = service.find();', '  if (!x) return;', '', '  use(x);', '}'),
		);
	});
});

describe('separate-checked-stanzas at a barrier', () => {
	for (const comment of ['// Verification phase.', '// @ts-expect-error', '// eslint-disable-next-line no-console']) {
		it(`ends a narrowing-assertion stanza at the comment ${comment}`, () => {
			const source = lines('function f() {', ' log();', ' const value = get();', ' assert(value != null);', comment, ' assert(value.id === 1);', '}');
			assert.equal(stable(source, rule), source.replace(' log();\n', ' log();\n\n'));
		});
	}

	it('ends a narrowing-assertion stanza at a multi-line assertion, and keeps the stanza above it', () => {
		const source = lines(
			'function f() {',
			' log();',
			' const value = get();',
			' assert(value);',
			' assert(',
			'  value.id === 1,',
			' );',
			' done();',
			'}',
		);

		assert.equal(stable(source, rule), source.replace(' log();\n', ' log();\n\n'));
	});

	it('keeps an unread binding apart from the chain below when a guard only reads a callback parameter of the same name', () => {
		const source = lines(
			'function f(items) {',
			'audit();',
			'const value = load();',
			'',
			'const used = load();',
			'',
			'if (items.some(value => value.id)) return;',
			'',
			'if (!used) return;',
			'work();',
			'}',
		);

		const expected = lines(
			'function f(items) {',
			'audit();',
			'const value = load();',
			'',
			'const used = load();',
			'if (items.some(value => value.id)) return;',
			'if (!used) return;',
			'',
			'work();',
			'}',
		);

		const rules = { ...rule, 'keep-guard-with-binding': 'error', 'keep-guard-clauses-together': 'error' } as const;
		const config = createConfig(rules);

		for (const pick of ['first', 'last'] as const) {
			assert.equal(
				fixOneByOne(source, config, pick, (text) => assert.ok(text.includes('const value = load();\n\nconst used'))),
				expected,
			);
		}

		assert.equal(stable(source, rules), expected);
	});

	it('reports nothing where an eslint-disable comment silences it', () => {
		const source = lines(
			'/* eslint-disable stanza/separate-checked-stanzas */',
			'function f(order) {',
			'  validate(order);',
			'  if (order.closed) return;',
			'  send(order);',
			'}',
		);

		assert.deepEqual(readSilencedRuleIds(source, rule), ['stanza/separate-checked-stanzas', 'stanza/separate-checked-stanzas']);
		assert.equal(fix(source, rule), source);
	});
});

describe('separate-checked-stanzas beside a dependent chain', () => {
	it('moves the edge above a checked binding that a dependent chain keeps to the gap above the chain', () => {
		const source = lines('function f() {', 'prepare();', 'const a = load();', '', 'const b = build(a);', 'if (!b) return;', 'use(b);', '}');
		const expected = lines('function f() {', 'prepare();', '', 'const a = load();', 'const b = build(a);', 'if (!b) return;', '', 'use(b);', '}');

		assert.equal(stable(source, { ...rule, 'keep-dependent-bindings-together': 'error' }), expected);
	});
});

describe('separate-checked-stanzas beside the jump rule', () => {
	it('ends the stanza above a jump at its lower border, so the jump below one line stays tight in any fix order', () => {
		const source = lines(
			'function f(id) {',
			'audit(id);',
			'const order = find(id);',
			'if (!order) return null;',
			'const total = sum(order);',
			'return total;',
			'}',
		);

		const expected = lines(
			'function f(id) {',
			'audit(id);',
			'',
			'const order = find(id);',
			'if (!order) return null;',
			'',
			'const total = sum(order);',
			'return total;',
			'}',
		);

		assert.equal(settled(source, { ...readPresetRules('strong'), ...rule }), expected);
	});
});

describe('separate-checked-stanzas with guardRoots', () => {
	const settings = (...roots: string[]) => ({ stanza: { guardRoots: roots } });

	it('reads a guardRoots call below an if guard as part of the chain, and separates the chain from the flow below', () => {
		const source = lines(
			'function f(id) {',
			'const order = find(id);',
			'if (!order) return;',
			"invariant(order.items.length > 0, 'empty');",
			'send(order);',
			'}',
		);

		const expected = source.replace("'empty');\n", "'empty');\n\n");

		assert.equal(settled(source, rule, settings('invariant')), expected);
		assert.equal(settled(source, { ...rule, 'keep-guard-clauses-together': 'error' }, settings('invariant')), expected);
	});

	it('reads a guardRoots call that narrows its argument as a guard, which gets a blank line below as the C# and Python rules write', () => {
		const source = lines('function f(id) {', 'const a = load(id);', 'check(a);', 'use(a);', '}');
		assert.equal(settled(source, rule, settings('check')), source.replace('check(a);\n', 'check(a);\n\n'));
	});

	it('reads a call of an assertionRoots name that narrows as an assertion, which gets no blank line below', () => {
		const source = lines('function f(id) {', 'log(id);', 'const a = load(id);', 'assert(a);', 'use(a);', '}');
		assert.equal(settled(source, rule), source.replace('log(id);\n', 'log(id);\n\n'));
	});
});

describe('separate-checked-stanzas in a container of two single-line statements', () => {
	it('separates the guard from the statement below whether or not separate-guard-clauses is enabled, which judges no such container', () => {
		const source = lines('function g(a) {', '  if (!a) return;', '  send(a);', '}');
		const expected = source.replace('return;\n', 'return;\n\n');

		assert.equal(settled(source, rule), expected);
		assert.equal(settled(source, { ...rule, 'separate-guard-clauses': 'error' }), expected);
	});
});
