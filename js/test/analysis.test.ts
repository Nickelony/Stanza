import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { Linter } from 'eslint';

import { createConfig, fix, fixWithPreset, lines, lintWithPreset, readPresetRules, readShape } from './helpers.ts';

describe('one owner per gap', () => {
	it('reports a gap that several rules ask for once, under the first rule in the owner order', () => {
		const code = lines('const a = 1;', 'const b = 2;', 'if (a) {', '  run();', '}');
		const messages = lintWithPreset(code, 'strong');

		assert.deepEqual(
			messages.map((message) => message.ruleId),
			['stanza/separate-multi-line-constructs'],
		);
	});

	it('hands the gap to the next rule that asks for it when the first one is off', () => {
		const code = lines('const a = 1;', 'const b = 2;', 'if (a) {', '  run();', '}');
		const { 'separate-multi-line-constructs': _, ...rules } = readPresetRules('strong');
		const messages = new Linter().verify(code, createConfig(rules), { filename: 'file.ts' });

		assert.deepEqual(
			messages.map((message) => message.ruleId),
			['stanza/separate-declaration-runs'],
		);
	});

	it('fixes only the gaps of the rules that are on', () => {
		const code = lines('const a = 1;', 'const b = 2;', 'run(a);', 'run(b);');
		assert.equal(fix(code, { 'separate-invocation-runs': 'error' }), lines('const a = 1;', 'const b = 2;', '', 'run(a);', 'run(b);'));
	});

	it('writes the same text with the rules in any order', () => {
		const code = lines('const a = 1;', 'const b = 2;', 'run(a);', 'run(b);', 'if (a) {', '  run();', '}', 'return b;');
		const rules = Object.entries(readPresetRules('strong'));
		const expected = fixWithPreset(code, 'strong');

		assert.equal(fix(code, Object.fromEntries([...rules].reverse())), expected);
		assert.equal(fix(code, Object.fromEntries([...rules.slice(3), ...rules.slice(0, 3)])), expected);
	});
});

describe('a container of two single-line units', () => {
	const cases: Record<string, string> = {
		'a guard and a call': lines('function f(a) {', '  if (!a) return;', '  run(a);', '}'),
		'a declaration and a jump': lines('function f(a) {', '  const b = g(a);', '  return b;', '}'),
		'two test blocks': lines('describe("a", () => {', '  it.todo("b");', '  it.todo("c");', '});'),
		'two assertions after nothing': lines('it("a", () => {', '  expect(a).toBe(1);', '  expect(b).toBe(2);', '});'),
		'two members of different kinds': lines('class A {', '  a = 1;', '  run() {}', '}'),
		'a declaration and a call in a case': lines('switch (x) {', '  case 1:', '    const b = g();', '    run(b);', '}'),
	};

	for (const [name, code] of Object.entries(cases)) {
		it(`asks for nothing between ${name}`, () => {
			assert.deepEqual(lintWithPreset(code, 'strong'), []);
		});
	}

	it('removes the blank line that stands there, and keeps it once a comment or a long unit stands there', () => {
		assert.equal(
			fixWithPreset(lines('function f(a) {', '  if (!a) return;', '', '  run(a);', '}'), 'strong'),
			lines('function f(a) {', '  if (!a) return;', '  run(a);', '}'),
		);

		assert.equal(
			fixWithPreset(lines('function f(a) {', '  if (!a) return;', '', '  // go', '  run(a);', '}'), 'strong'),
			lines('function f(a) {', '  if (!a) return;', '', '  // go', '  run(a);', '}'),
		);
	});
});

describe('the quirks', () => {
	const samples: Record<string, string> = {
		'a line that starts with a parenthesis': lines('const a = 1', ';(function () {', '  run()', '})()', 'foo()'),
		'a line that starts with a bracket': lines('const a = 1', ';[1, 2].forEach((x) => {', '  run(x)', '})', 'foo()'),
		'a line that starts with a template literal': lines('const a = tag', ';`x`.length', 'foo()', 'if (a) {', '  run()', '}'),
		'a template literal with blank lines': lines('const a = `x', '', '', 'y`;', 'run(a);', 'run(a);'),
		'a trailing comment': lines('run(); // note', 'if (a) {', '  run();', '}', 'run();'),
		'a decorated member': lines('class A {', '  @Input()', '  name = "";', '  size = 1;', '  @Output() done = 1;', '}'),
		'a label': lines('outer: for (const a of xs) {', '  for (const b of ys) {', '    if (b) continue outer;', '    run(a, b);', '  }', '}', 'run();'),
		'a try statement': lines('run();', 'try {', '  run();', '} catch {', '  run();', '} finally {', '  run();', '}', 'run();'),
		'a generator': lines('function* g() {', '  yield 1;', '  yield 2;', '  const a = yield 3;', '  yield a;', '}'),
		'using declarations': lines('{', '  using a = open();', '  using b = open();', '  await using c = open();', '  run(a, b, c);', '}'),

		'an ambient module': lines(
			'declare module "x" {',
			'  export const a: number;',
			'  export const b: number;',
			'  export function f(): void;',
			'  export function f(a: string): void;',
			'}',
		),

		'a default export': lines("import a from 'a';", 'export default function run() {', '  a();', '}', 'export const b = 1;'),
		'a getter and a setter': lines('class A {', '  get a() {', '    return 1;', '  }', '  set a(v: number) {', '    run(v);', '  }', '  b = 1;', '}'),
		overloads: lines('function f(a: string): void;', 'function f(a: number): void;', 'function f(a: unknown) {', '  run(a);', '}', 'f(1);'),

		'a derived class': lines(
			'class A extends B {',
			'  constructor(a) {',
			'    super(a);',
			'    this.a = a;',
			'    this.b = a;',
			'  }',
			'  dispose() {',
			'    this.c();',
			'    this.d();',
			'    super.dispose();',
			'  }',
			'}',
		),

		'lists with blank lines': lines(
			'const o = {',
			'  a: 1,',
			'',
			'  b: {',
			'    c: [',
			'      1,',
			'',
			'      2,',
			'    ],',
			'  },',
			'',
			'  d() {',
			'    return f(',
			'      a,',
			'',
			'      () => {',
			'        run();',
			'      },',
			'    );',
			'  },',
			'};',
		),

		'a loose run': lines('const a = 1;', '', 'const b = 2;', '', 'const c = 3;', '', 'const d = 4;', '', 'return a;'),

		'modifier groups': lines(
			'class A {',
			'  static a = 1;',
			'  static b = 2;',
			'  readonly c = 3;',
			'  readonly d = 4;',
			'  #e = 5;',
			'  #f = 6;',
			'}',
		),

		'a fall-through case': lines('switch (x) {', '  case 1:', '  case 2:', '    run();', '    break;', '  default:', '    stop();', '}'),
	};

	for (const [name, code] of Object.entries(samples)) {
		it(`keeps the syntax tree and settles on ${name}`, () => {
			const fixed = fixWithPreset(code, 'strong');

			assert.equal(readShape(fixed), readShape(code));
			assert.equal(fixWithPreset(fixed, 'strong'), fixed);
			assert.deepEqual(lintWithPreset(fixed, 'strong'), []);
		});
	}

	it('leaves the lines of a template literal as they are', () => {
		const code = lines('const a = `x', '', '', 'y`;', 'run(a);', 'run(a);');
		assert.equal(fixWithPreset(code, 'strong'), lines('const a = `x', '', '', 'y`;', '', 'run(a);', 'run(a);'));
	});

	it('moves the blank line above the comment that leads a node, and past a trailing comment', () => {
		assert.equal(
			fixWithPreset(lines('run(); // note', '// @ts-expect-error', 'if (a) {', '  run();', '}'), 'moderate'),
			lines('run(); // note', '', '// @ts-expect-error', 'if (a) {', '  run();', '}'),
		);
	});

	it('keeps the signatures of an overloaded function together', () => {
		const code = lines(
			'function f(',
			'  a: string,',
			'): void;',
			'function f(',
			'  a: number,',
			'): void;',
			'function f(a: unknown) {',
			'  run(a);',
			'}',
		);

		assert.equal(fixWithPreset(code, 'strong'), code);
	});

	it('keeps an empty case with the case it falls through to', () => {
		const code = lines('switch (x) {', '  case 1:', '  case 2: {', '    run();', '    break;', '  }', '}');
		assert.equal(fixWithPreset(code, 'strong'), code);
	});
});
