import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { parseForESLint } from '@typescript-eslint/parser';
import { Linter } from 'eslint';
import tseslint from 'typescript-eslint';

import rule from '../../src/rules/keep-accumulator-with-loop.ts';
import { createConfig, createRuleTester, crlf, fix, lines, readPresetRules, readSilencedRuleIds, stable } from '../helpers.ts';

createRuleTester().run('keep-accumulator-with-loop', rule as never, {
	valid: [
		{ code: lines('let total = 0;', 'for (const item of items) {', '  total += item;', '}', 'use(total);') },

		{
			code: lines('check(messages);', 'const client = new Client();', '', 'for (const message of messages) {', '  client.send(message);', '}'),
			name: 'a method that is no builder only uses the object',
		},

		{
			code: lines('check(items);', 'let total = start;', '', 'for (const item of items) {', '  total += item;', '}'),
			name: 'a seed that reads a variable is no seed',
		},

		{
			code: lines('check(items);', 'const list = [first];', '', 'for (const item of items) {', '  list.push(item);', '}'),
			name: 'an element that reads a variable',
		},

		{
			code: lines('check(items);', 'const max = Number.MAX_VALUE;', '', 'for (const item of items) {', '  max = item;', '}'),
			name: 'a member of a global reads it',
		},

		{
			code: lines('check(items);', 'let total = 0;', '', '// add them up', 'for (const item of items) {', '  total += item;', '}'),
			name: 'a comment keeps the blank line',
		},

		{
			code: lines('check(items);', 'let total = 0; // sum', '', 'for (const item of items) {', '  total += item;', '}'),
			name: 'a trailing comment keeps the blank line',
		},

		{
			code: lines('check(items);', 'const list = [', '  0,', '];', '', 'for (const item of items) {', '  list.push(item);', '}'),
			name: 'a multi-line seed',
		},

		{ code: lines('check(items);', 'let a = 0, b = 0;', '', 'for (const item of items) {', '  a += item;', '}'), name: 'two declarators' },

		{
			code: lines('check(items);', 'let total = 0;', '', 'for (const item of items) {', '  log(total, item);', '}'),
			name: 'a loop that only reads the name',
		},

		{
			code: lines('check(items);', 'let total = 0;', '', 'for (let index = 0; index < items.length; total++) {', '  log(index);', '}'),
			name: 'only the body of the loop counts',
		},

		{ code: lines('check(items);', 'let total = 0;', '', 'if (items) {', '  total += 1;', '}'), name: 'an if is no loop' },

		{
			code: lines('check(items);', 'total += 0;', '', 'for (const item of items) {', '  total += item;', '}'),
			name: 'a compound assignment is no seed',
		},
	],

	invalid: [
		{
			code: lines('check(items);', 'let total = 0;', '', 'for (const item of items) {', '  total += item.price;', '}', 'use(total);'),
			output: lines('check(items);', 'let total = 0;', 'for (const item of items) {', '  total += item.price;', '}', 'use(total);'),
			errors: [{ messageId: 'extraBlankLine' }],
		},

		{
			code: lines(
				'function f(queue) {',
				'  check(queue);',
				'  const seen = new Set<string>();',
				'',
				'',
				'  while (queue.length > 0) {',
				'    const next = queue.pop();',
				'    if (next) seen.add(next);',
				'  }',
				'  return seen;',
				'}',
			),

			output: lines(
				'function f(queue) {',
				'  check(queue);',
				'  const seen = new Set<string>();',
				'  while (queue.length > 0) {',
				'    const next = queue.pop();',
				'    if (next) seen.add(next);',
				'  }',
				'  return seen;',
				'}',
			),

			errors: 1,
		},

		{
			code: lines('check(source);', 'const map = new Map();', '', 'for (const key in source) {', '  map.set(key, source[key]);', '}'),
			output: lines('check(source);', 'const map = new Map();', 'for (const key in source) {', '  map.set(key, source[key]);', '}'),
			errors: 1,
		},

		{
			code: lines('check(items);', 'let count = 0;', '', 'do {', '  count++;', '} while (more());'),
			output: lines('check(items);', 'let count = 0;', 'do {', '  count++;', '} while (more());'),
			errors: 1,
		},

		{
			code: lines('check(items);', 'result = undefined;', '', 'for (const item of items) {', '  result = item;', '}'),
			output: lines('check(items);', 'result = undefined;', 'for (const item of items) {', '  result = item;', '}'),
			errors: 1,
		},

		{
			code: lines('check(items);', 'let sum = BigInt(0);', '', 'for (const item of items) {', '  [sum] = [sum + item];', '}'),
			output: lines('check(items);', 'let sum = BigInt(0);', 'for (const item of items) {', '  [sum] = [sum + item];', '}'),
			errors: 1,
		},

		{
			code: lines('check(items);', 'const out = { total: 0, names: [] };', '', 'for (const item of items) {', '  out = merge(out, item);', '}'),
			output: lines('check(items);', 'const out = { total: 0, names: [] };', 'for (const item of items) {', '  out = merge(out, item);', '}'),
			errors: 1,
		},

		{
			code: lines(
				'check(items);',
				'const list: string[] = [];',
				'',
				'for (const item of items) {',
				'  list.unshift(item);',
				'  list.splice(0, 1);',
				'}',
			),

			output: lines(
				'check(items);',
				'const list: string[] = [];',
				'for (const item of items) {',
				'  list.unshift(item);',
				'  list.splice(0, 1);',
				'}',
			),

			errors: 1,
		},

		{
			code: crlf(lines('check(items);', 'let total = 0;', '', 'for (const item of items) {', '  total += item;', '}')),
			output: crlf(lines('check(items);', 'let total = 0;', 'for (const item of items) {', '  total += item;', '}')),
			errors: 1,
		},
	],
});

describe('keep-accumulator-with-loop beside the rules that add a blank line', () => {
	const tight = lines(
		'function f(items) {',
		'  check(items);',
		'',
		'  const list = [];',
		'  for (const item of items) {',
		'    list.push(item);',
		'  }',
		'',
		'  return list;',
		'}',
	);

	const apart = lines(
		'function f(items) {',
		'  check(items);',
		'  const list = [];',
		'',
		'  for (const item of items) {',
		'    list.push(item);',
		'  }',
		'  return list;',
		'}',
	);

	it('wins the gap above the loop over separate-multi-line-constructs, and settles', () => {
		const rules = { 'separate-multi-line-constructs': 'error', 'keep-accumulator-with-loop': 'error' } as const;

		assert.equal(stable(apart, rules), tight);
		assert.equal(stable(tight, rules), tight);
	});

	it('leaves the opening seed tight with its loop without inserting a leading blank line', () => {
		const code = lines(
			'function f(items) {',
			'  const list = [];',
			'',
			'  for (const item of items) {',
			'    list.push(item);',
			'  }',
			'  use(list);',
			'}',
		);

		const rules = { 'separate-multi-line-constructs': 'error', 'keep-accumulator-with-loop': 'error' } as const;

		assert.equal(
			stable(code, rules),
			lines('function f(items) {', '  const list = [];', '  for (const item of items) {', '    list.push(item);', '  }', '', '  use(list);', '}'),
		);
	});

	it('settles at the strong level with every opt-in rule enabled', () => {
		const rules = {
			...readPresetRules('strong', true),
		} as const;

		const fixed = stable(apart, rules);

		assert.ok(fixed.includes('  const list = [];\n  for (const item of items) {'), fixed);
	});

	it('reads the kept pair as one stanza for separate-jump-statements', () => {
		const code = lines('function f(items) {', '  let total = 0;', '', '  while (more()) total++;', '  return total;', '}');
		const rules = { 'separate-jump-statements': 'error', 'keep-accumulator-with-loop': 'error' } as const;

		assert.equal(stable(code, rules), lines('function f(items) {', '  let total = 0;', '  while (more()) total++;', '', '  return total;', '}'));
	});
});

describe('keep-accumulator-with-loop over a run of seeds', () => {
	const keeper = { 'keep-accumulator-with-loop': 'error' } as const;
	const strong = { ...readPresetRules('strong'), ...keeper };

	it('keeps every seed of a run the loop builds with the loop, and separates the group above', () => {
		const source = lines(
			'function f(xs) {',
			'prepare();',
			'let sum = 0;',
			'let count = 0;',
			'',
			'for (const x of xs) { sum += x; count++; }',
			'return sum / count;',
			'}',
		);

		const expected = lines(
			'function f(xs) {',
			'prepare();',
			'',
			'let sum = 0;',
			'let count = 0;',
			'for (const x of xs) { sum += x; count++; }',
			'',
			'return sum / count;',
			'}',
		);

		assert.equal(stable(source, strong), expected);
		assert.equal(stable(source.replace('let sum = 0;\n', 'let sum = 0;\n\n'), strong), expected);
	});

	it('leaves a seed the loop does not build apart from the loop, with the seeds above it', () => {
		const source = lines(
			'function f(xs) {',
			'prepare();',
			'let sum = 0;',
			'let count = 0;',
			'',
			'for (const x of xs) { sum += x; }',
			'return sum / count;',
			'}',
		);

		const expected = lines(
			'function f(xs) {',
			'prepare();',
			'',
			'let sum = 0;',
			'let count = 0;',
			'',
			'for (const x of xs) { sum += x; }',
			'return sum / count;',
			'}',
		);

		assert.equal(stable(source, strong), expected);
	});

	for (const body of [
		'const later = () => { total += item; };',
		'function later() { total += item; }',
		'const later = function () { total++; };',
		'class Later { method() { total++; } }',
		'const Later = class { method() { total++; } };',
		'let total = item; total++;',
		'{ let total = item; total++; }',
		'const total = new Set(); total.add(item);',
		'for (let total of items) { total++; }',
		'try { throw item; } catch (total) { total++; }',
		'values[total] = item;',
	]) {
		it(`leaves a seed apart from a loop that only reads it or builds another binding of its name: ${body}`, () => {
			const source = lines('let total = 0;', '', 'for (const item of items) { ' + body + ' }', 'use(total);');
			assert.equal(stable(source, keeper), source);
		});
	}

	for (const body of [
		'if (item) { total += item; }',
		'for (const child of item) { total += child; }',
		'{ let total = item; total++; } total += item;',
		'const later = () => total++; total += item;',
		'([total!] = [item]);',
	]) {
		it(`joins a seed to a loop that builds it as it runs: ${body}`, () => {
			const source = lines('let total = 0;', '', 'for (const item of items) { ' + body + ' }', 'use(total);');
			assert.equal(stable(source, keeper), source.replace('\n\n', '\n'));
		});
	}

	it('reports a silenced gap without a fix, so the eslint-disable comment counts as used', () => {
		const source = lines(
			'/* eslint-disable stanza/keep-accumulator-with-loop */',
			'let total = 0;',
			'',
			'for (const item of items) { total += item; }',
		);

		assert.deepEqual(readSilencedRuleIds(source, keeper), ['stanza/keep-accumulator-with-loop']);
		assert.equal(fix(source, keeper), source);
	});
});

describe('keep-accumulator-with-loop over many seeds', () => {
	it('reads each statement of a loop a number of times linear in the seeds above it, so a loop is walked once and not once for each seed', () => {
		const counts: number[] = [];

		for (const size of [200, 400]) {
			let reads = 0;

			const parser = {
				...tseslint.parser,

				parseForESLint(code: string, options: Parameters<typeof parseForESLint>[1]) {
					const parsed = parseForESLint(code, options);
					const [declaration] = parsed.ast.body;
					const loop = declaration?.type === 'FunctionDeclaration' ? declaration.body.body.at(-1) : undefined;

					for (const statement of loop?.type === 'ForOfStatement' && loop.body.type === 'BlockStatement' ? loop.body.body : []) {
						if (statement.type === 'ExpressionStatement') {
							const { expression } = statement;

							Object.defineProperty(statement, 'expression', {
								get: () => {
									reads += 1;
									return expression;
								},
							});
						}
					}

					return parsed;
				},
			};

			const seeds = Array.from({ length: size }, (_, index) => '  let s' + index + ' = 0;');
			const body = Array.from({ length: size }, (_, index) => '    s' + index + ' += x;');
			const source = lines('function f(list) {', ...seeds, '  for (const x of list) {', ...body, '  }', '}');
			const first = createConfig({ 'keep-accumulator-with-loop': 'error' })[0];

			new Linter().verify(source, [{ ...first, languageOptions: { ...first?.languageOptions, parser } }], { filename: 'file.ts' });
			counts.push(reads);
		}

		assert.ok((counts[0] ?? 0) > 0);
		assert.ok((counts[1] ?? 0) <= (counts[0] ?? 0) * 2.5, 'reads grew from ' + counts[0] + ' to ' + counts[1] + ' as the seeds doubled');
	});
});
