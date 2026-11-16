import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { Linter } from 'eslint';

import rule from '../../src/rules/keep-guard-with-binding.ts';
import { createConfig, createRuleTester, crlf, fix, fixOneByOne, lines, readPresetRules, readSilencedRuleIds, stable } from '../helpers.ts';

createRuleTester().run('keep-guard-with-binding', rule as never, {
	valid: [
		{ code: lines('function f(id) {', '  const user = load(id);', '  if (!user) return;', '  run(user);', '}') },

		{
			code: lines('function f(id) {', '  const user = load(id);', '', '  // no user, no work', '  if (!user) return;', '  run(user);', '}'),
			name: 'a comment keeps the blank line',
		},

		{
			code: lines('function f(id) {', '  const user = load(id); // load', '', '  if (!user) return;', '  run(user);', '}'),
			name: 'a trailing comment keeps the blank line',
		},

		{
			code: lines('function f(id) {', '  const user = load(id);', '', '  if (!id) return;', '  run(user);', '}'),
			name: 'the guard tests another name',
		},

		{
			code: lines('function f(order) {', '  const customer = load(order);', '', '  if (!order.customer) return;', '  run(customer);', '}'),
			name: 'a property is no variable',
		},

		{
			code: lines('function f(id) {', '  const user = load(id);', '', '  if (!user) return;', '  else run(user);', '  go();', '}'),
			name: 'an if with an else is no guard',
		},

		{
			code: lines('function f(id) {', '  const user = load(id);', '', '  if (!user) {', '    log(id);', '    return;', '  }', '  run(user);', '}'),
			name: 'an if with two statements is no guard',
		},

		{
			code: lines('function f(id) {', '  const user = load(id);', '', '  invariant(user);', '  run(user);', '}'),
			name: 'a guard helper call is no if',
		},

		{
			code: lines('function f(id) {', '  let user;', '', '  if (!user) return;', '  run(user);', '}'),
			name: 'a declarator without a value binds nothing',
		},

		{
			code: lines('function f(id) {', '  user.name = load(id);', '', '  if (!user) return;', '  run(user);', '}'),
			name: 'a member target binds nothing',
		},

		{ code: lines('const user = load(id);', '', 'if (!user) throw error;', 'run(user);'), name: 'the top level of a file is not judged' },
	],

	invalid: [
		{
			code: lines('function f(id) {', '  audit(id);', '  const user = load(id);', '', '  if (!user) throw new Error();', '  run(user);', '}'),
			output: lines('function f(id) {', '  audit(id);', '  const user = load(id);', '  if (!user) throw new Error();', '  run(user);', '}'),
			errors: [{ messageId: 'extraBlankLine' }],
		},

		{
			code: lines(
				'function f(id) {',
				'  const user = load({',
				'    id,',
				'  });',
				'',
				'  if (user === undefined) {',
				'    return;',
				'  }',
				'  run(user);',
				'}',
			),

			output: lines(
				'function f(id) {',
				'  const user = load({',
				'    id,',
				'  });',
				'  if (user === undefined) {',
				'    return;',
				'  }',
				'  run(user);',
				'}',
			),

			errors: 1,
			name: 'a binding that spans several lines',
		},

		{
			code: lines(
				'function f(id) {',
				'  const { name, [key]: { size }, ...rest } = load(id);',
				'',
				'  if (size > limit) return;',
				'  run(name, rest);',
				'}',
			),

			output: lines(
				'function f(id) {',
				'  const { name, [key]: { size }, ...rest } = load(id);',
				'  if (size > limit) return;',
				'  run(name, rest);',
				'}',
			),

			errors: 1,
		},

		{
			code: lines('function f(list) {', '  let [first = null] = list;', '', '  if (!first) return;', '  run(first);', '}'),
			output: lines('function f(list) {', '  let [first = null] = list;', '  if (!first) return;', '  run(first);', '}'),
			errors: 1,
		},

		{
			code: lines('function f(id) {', '  user = find(id);', '', '  if (!user) return;', '  run(user);', '}'),
			output: lines('function f(id) {', '  user = find(id);', '  if (!user) return;', '  run(user);', '}'),
			errors: 1,
		},

		{
			code: lines('for (const id of ids) {', '  const user = load(id);', '', '  if (!isActive(user)) continue;', '  run(user);', '}'),
			output: lines('for (const id of ids) {', '  const user = load(id);', '  if (!isActive(user)) continue;', '  run(user);', '}'),
			errors: 1,
		},

		{
			code: crlf(lines('function f(id) {', '  const user = load(id);', '', '  if (!user) return;', '  run(user);', '}')),
			output: crlf(lines('function f(id) {', '  const user = load(id);', '  if (!user) return;', '  run(user);', '}')),
			errors: 1,
		},
	],
});

describe('keep-guard-with-binding beside the rules that add a blank line', () => {
	const tight = lines(
		'function f(id) {',
		'  audit(id);',
		'',
		'  const user = load(id);',
		'  if (!user) {',
		'    return;',
		'  }',
		'',
		'  run(user);',
		'}',
	);

	const apart = lines(
		'function f(id) {',
		'  audit(id);',
		'  const user = load(id);',
		'',
		'  if (!user) {',
		'    return;',
		'  }',
		'  run(user);',
		'}',
	);

	it('wins the gap above a braced guard over separate-multi-line-constructs, and settles', () => {
		const rules = { 'separate-multi-line-constructs': 'error', 'keep-guard-with-binding': 'error' } as const;

		assert.equal(stable(apart, rules), tight);
		assert.equal(stable(tight, rules), tight);

		assert.equal(
			fix(tight, { 'separate-multi-line-constructs': 'error' }),
			apart.replace('  audit(id);\n', '  audit(id);\n\n').replace('  }\n  run', '  }\n\n  run'),
		);
	});

	it('joins a chain of guards below the binding with keep-guard-clauses-together', () => {
		const code = lines(
			'function f(id) {',
			'  audit(id);',
			'  const user = load(id);',
			'',
			'  if (!user) {',
			'    return;',
			'  }',
			'',
			'  if (!user.active) {',
			'    return;',
			'  }',
			'  run(user);',
			'}',
		);

		const rules = { 'separate-multi-line-constructs': 'error', 'keep-guard-with-binding': 'error', 'keep-guard-clauses-together': 'error' } as const;

		assert.equal(
			stable(code, rules),

			lines(
				'function f(id) {',
				'  audit(id);',
				'',
				'  const user = load(id);',
				'  if (!user) {',
				'    return;',
				'  }',
				'  if (!user.active) {',
				'    return;',
				'  }',
				'',
				'  run(user);',
				'}',
			),
		);
	});

	it('settles at the strong level with the rule enabled', () => {
		const rules = { ...readPresetRules('strong'), 'keep-guard-with-binding': 'error' } as const;
		const fixed = stable(apart, rules);

		assert.ok(fixed.includes('  const user = load(id);\n  if (!user) {'), fixed);
	});
});

describe('keep-guard-with-binding beside the checked stanza', () => {
	const keeper = { 'keep-guard-with-binding': 'error' } as const;

	it('leaves a binding apart from a multi-line invariant call below it, and separates the guard call from the flow below', () => {
		const source = lines('function f() {', ' const value = get();', '', ' invariant(', '  value != null,', ' );', ' use(value);', '}');
		assert.equal(stable(source, { ...keeper, 'separate-checked-stanzas': 'error' }), source.replace(' );\n', ' );\n\n'));
	});

	it('joins a binding to a multi-line if guard below it, and separates the stanza they form', () => {
		const source = lines('function f() {', ' const value = get();', '', ' if (value == null) {', '  return;', ' }', ' use(value);', '}');
		const expected = lines('function f() {', ' const value = get();', ' if (value == null) {', '  return;', ' }', '', ' use(value);', '}');

		assert.equal(stable(source, { ...keeper, 'separate-checked-stanzas': 'error' }), expected);
	});

	it('joins every binding a guard checks without separate-checked-stanzas', () => {
		const source = lines('function f() {', ' const a = getA();', '', ' const b = getB();', ' if (!a || !b) return;', ' use(a, b);', '}');
		assert.equal(stable(source, keeper), source.replace('getA();\n\n', 'getA();\n'));
	});

	it('joins the bindings that later guards read, and leaves the blank line between the guards', () => {
		const source = lines(
			'function f() {',
			' prepare();',
			' const a = getA();',
			'',
			' const b = getB();',
			'',
			' if (!a) return;',
			'',
			' if (!b) return;',
			' use(a, b);',
			'}',
		);

		const tight = lines(
			'function f() {',
			' prepare();',
			' const a = getA();',
			' const b = getB();',
			' if (!a) return;',
			'',
			' if (!b) return;',
			' use(a, b);',
			'}',
		);

		const separated = lines(
			'function f() {',
			' prepare();',
			'',
			' const a = getA();',
			' const b = getB();',
			' if (!a) return;',
			'',
			' if (!b) return;',
			'',
			' use(a, b);',
			'}',
		);

		assert.equal(stable(source, keeper), tight);
		assert.equal(stable(source, { ...keeper, 'separate-checked-stanzas': 'error' }), separated);
	});

	it('leaves a binding the guard does not read apart from the checked bindings below it', () => {
		const source = lines(
			'function f() {',
			' const unrelated = getOther();',
			'',
			' const a = getA();',
			'',
			' const b = getB();',
			' if (!a || !b) return;',
			' use(a, b);',
			'}',
		);

		assert.equal(stable(source, keeper), source.replace('getA();\n\n', 'getA();\n'));
	});

	it('hands its gap back where an eslint-disable comment silences it, so the checked stanza reads a shadowed callback as no read', () => {
		const source = lines(
			'/* eslint-disable stanza/keep-guard-with-binding */',
			'function f(items) {',
			'audit();',
			'const value = load();',
			'',
			'if (items.some(value => value.id)) return;',
			'work();',
			'}',
		);

		const rules = { ...keeper, 'separate-checked-stanzas': 'error' } as const;

		assert.equal(stable(source, rules), source.replace('return;\nwork', 'return;\n\nwork'));
	});

	it('reports a silenced gap without a fix, so the eslint-disable comment counts as used', () => {
		const source = lines(
			'/* eslint-disable stanza/keep-guard-with-binding */',
			'function f(id) {',
			'  const user = load(id);',
			'',
			'  if (!user) return;',
			'  run(user);',
			'}',
		);

		assert.deepEqual(readSilencedRuleIds(source, keeper), ['stanza/keep-guard-with-binding']);
		assert.equal(fix(source, keeper), source);
	});

	it('words a silenced report without claiming a blank line, since it shows only where inline configuration is off', () => {
		const source = lines(
			'function f(id) {',
			'  audit(id);',
			'  const order = find(id);',
			'  // eslint-disable-next-line stanza/keep-guard-with-binding',
			'  if (!order) return;',
			'  send(order);',
			'}',
		);

		const config = createConfig(keeper).map((entry) => ({ ...entry, linterOptions: { noInlineConfig: true } }));
		const reports = new Linter().verify(source, config, { filename: 'file.ts' }).filter((report) => report.ruleId !== null);

		assert.deepEqual(
			reports.map((report) => [report.ruleId, report.line, report.message, report.fix]),
			[['stanza/keep-guard-with-binding', 5, 'An eslint-disable comment silences this rule here, so the gap stays as written.', undefined]],
		);
	});

	it('uses a directive on a tight gap too under the default report of unused directives, so every fix order keeps it', () => {
		const source = lines(
			'function f(id) {',
			'  const order = find(id);',
			'  if (!order) { // eslint-disable-line stanza/keep-guard-with-binding',
			'    return;',
			'  }',
			'  send(order);',
			'}',
		);

		const expected = source.replace('find(id);\n', 'find(id);\n\n').replace('  }\n', '  }\n\n');
		const config = createConfig({ ...readPresetRules('strong'), ...keeper }).map((entry) => ({ ...entry, linterOptions: {} }));
		const linter = new Linter();
		const result = linter.verifyAndFix(source, config, { filename: 'file.ts' });

		assert.equal(result.output, expected);
		assert.deepEqual(result.messages, []);

		assert.deepEqual(
			linter.verify(source, config, { filename: 'file.ts' }).map((message) => message.ruleId),
			['stanza/separate-multi-line-constructs', 'stanza/separate-multi-line-constructs'],
		);

		assert.equal(fixOneByOne(source, config, 'first'), expected);
		assert.equal(fixOneByOne(source, config, 'last'), expected);
	});
});
