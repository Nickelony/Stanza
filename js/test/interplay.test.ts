import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { Linter } from 'eslint';

import { createConfig, fix, fixWithPreset, lines, lintWithPreset, readPresetRules } from './helpers.ts';

/** Lists the rules that report a source, one entry for each message. */
function reporters(
	code: string,
	rules: Record<string, Linter.RuleEntry>,
	filename = 'file.ts',
	settings: Record<string, unknown> = {},
): (string | null)[] {
	return new Linter().verify(code, createConfig(rules, settings), { filename }).map((message) => message.ruleId);
}

type Entries = Record<string, Linter.RuleEntry>;

/** The ways to enable a pair of removal rules for the units of one half: that half alone, and both halves together. */
function halvesOf(half: string, other: string, label: string): [name: string, rules: Entries][] {
	return [
		[`the ${label} half`, { [half]: 'error' }],
		['both halves', { [half]: 'error', [other]: 'error' }],
	];
}

describe('the rules that keep a run together', () => {
	const statementKeeper = 'keep-statement-runs-together';
	const memberKeeper = 'keep-member-runs-together';
	const statementHalves = halvesOf(statementKeeper, memberKeeper, 'statement');
	const memberHalves = halvesOf(memberKeeper, statementKeeper, 'member');

	for (const [name, keep] of statementHalves) {
		it(`joins the run of each run rule that is on, and leaves the other statements as they are, with ${name}`, () => {
			const rules: Entries = { ...keep, 'separate-invocation-runs': 'error' };

			assert.equal(fix(lines('log(1);', '', 'log(2);', '', 'log(3);'), rules), lines('log(1);', 'log(2);', 'log(3);'));
			assert.equal(fix(lines('log(1);', '', 'log(2);', '', 'log(3);'), keep), lines('log(1);', '', 'log(2);', '', 'log(3);'));
			assert.equal(fix(lines('const a = 1;', '', 'const b = 2;'), rules), lines('const a = 1;', '', 'const b = 2;'));
		});

		it(`writes the blank line a run rule asks for around the run it joins, whatever the order of the rules, with ${name}`, () => {
			const code = lines('start();', 'log(1);', '', 'log(2);', 'stop();');
			const expected = lines('start();', '', 'log(1);', 'log(2);', '', 'stop();');
			const rules = Object.entries({ ...keep, 'separate-invocation-runs': 'error' } as Entries);

			assert.equal(fix(code, Object.fromEntries(rules)), expected);
			assert.equal(fix(code, Object.fromEntries(rules.toReversed())), expected);
		});

		it(`reads the runs of each family of statements: declarations by kind, assertions, awaits and assignments, with ${name}`, () => {
			const rules: Entries = {
				...keep,
				'separate-declaration-runs': 'error',
				'separate-assertion-runs': 'error',
				'separate-await-runs': 'error',
				'separate-assignment-runs': 'error',
			};

			assert.equal(
				fix(lines('const a = 1;', '', 'const b = 2;', '', 'let c = 3;', 'use(c);'), rules),
				lines('const a = 1;', 'const b = 2;', '', 'let c = 3;', 'use(c);'),
			);

			assert.equal(
				fix(lines('go();', '', 'expect(a).toBe(1);', '', 'expect(b).toBe(2);'), rules),
				lines('go();', '', 'expect(a).toBe(1);', 'expect(b).toBe(2);'),
			);

			assert.equal(fix(lines('go();', '', 'await a();', '', 'await b();'), rules), lines('go();', '', 'await a();', 'await b();'));
			assert.equal(fix(lines('go();', '', 'x = 1;', '', 'y = 2;'), rules), lines('go();', '', 'x = 1;', 'y = 2;'));
		});

		it(`keeps the blank line beside a comment, a multi-line statement and a trailing comment, with ${name}`, () => {
			const rules: Entries = { ...keep, 'separate-invocation-runs': 'error' };

			const samples = [
				[lines('log(1);', '', '// the second', 'log(2);', '', 'log(3);'), lines('log(1);', '', '// the second', 'log(2);', 'log(3);')],
				[lines('log(1); // first', '', 'log(2);', '', 'log(3);'), lines('log(1); // first', '', 'log(2);', 'log(3);')],
				[lines('log(1);', '', 'log({', '  x: 1,', '});', '', 'log(2);'), lines('log(1);', '', 'log({', '  x: 1,', '});', '', 'log(2);')],
			] as const;

			for (const [code, expected] of samples) {
				assert.equal(fix(code, rules), expected);
			}
		});
	}

	for (const [name, keep] of memberHalves) {
		it(`joins a run of members that separate-member-groups reads as one, and keeps the groups apart, with ${name}`, () => {
			const rules: Entries = { ...keep, 'separate-member-groups': 'error' };

			assert.equal(
				fix(lines('class A {', '  a = 1;', '', '  b = 2;', '', '  m() {}', '}'), rules),
				lines('class A {', '  a = 1;', '  b = 2;', '', '  m() {}', '}'),
			);

			assert.equal(
				fix(lines('interface A {', '  a: string;', '', '  b: string;', '', '  m(): void;', '}'), rules),
				lines('interface A {', '  a: string;', '  b: string;', '', '  m(): void;', '}'),
			);

			assert.equal(
				fix(lines('type A = {', '  a: string;', '', '  b: string;', '', '  m(): void;', '};'), rules),
				lines('type A = {', '  a: string;', '  b: string;', '', '  m(): void;', '};'),
			);
		});

		it(`reads a member by its modifiers where separate-modifier-groups is on, with ${name}`, () => {
			const code = lines('class A {', '  a = 1;', '', '  b = 2;', '', '  static c = 3;', '', '  static d = 4;', '}');

			assert.equal(
				fix(code, { ...keep, 'separate-member-groups': 'error' }),
				lines('class A {', '  a = 1;', '  b = 2;', '  static c = 3;', '  static d = 4;', '}'),
			);

			assert.equal(
				fix(code, { ...keep, 'separate-member-groups': 'error', 'separate-modifier-groups': 'error' }),
				lines('class A {', '  a = 1;', '  b = 2;', '', '  static c = 3;', '  static d = 4;', '}'),
			);
		});

		it(`keeps the blank line beside a comment, a multi-line member and a trailing comment, with ${name}`, () => {
			const rules: Entries = { ...keep, 'separate-member-groups': 'error' };

			const samples = [
				[
					lines('class A {', '  a = 1;', '', '  // the second', '  b = 2;', '', '  c = 3;', '}'),
					lines('class A {', '  a = 1;', '', '  // the second', '  b = 2;', '  c = 3;', '}'),
				],

				[
					lines('class A {', '  a = 1; // first', '', '  b = 2;', '', '  c = 3;', '}'),
					lines('class A {', '  a = 1; // first', '', '  b = 2;', '  c = 3;', '}'),
				],

				[
					lines('class A {', '  a = 1;', '', '  b = {', '    x: 1,', '  };', '', '  c = 3;', '}'),
					lines('class A {', '  a = 1;', '', '  b = {', '    x: 1,', '  };', '', '  c = 3;', '}'),
				],
			] as const;

			for (const [code, expected] of samples) {
				assert.equal(fix(code, rules), expected);
			}
		});

		it(`writes the blank line a rule asks for around the run it joins, whatever the order of the rules, with ${name}`, () => {
			const code = lines('class A {', '  m() {}', '  a = 1;', '', '  b = 2;', '  n() {}', '}');
			const expected = lines('class A {', '  m() {}', '', '  a = 1;', '  b = 2;', '', '  n() {}', '}');
			const rules = Object.entries({ ...keep, 'separate-member-groups': 'error' } as Entries);

			assert.equal(fix(code, Object.fromEntries(rules)), expected);
			assert.equal(fix(code, Object.fromEntries(rules.toReversed())), expected);
		});
	}

	it('leaves the containers of the other half as written when only one half is enabled', () => {
		const code = lines('log(1);', '', 'log(2);', '', 'log(3);', '', 'class A {', '  a = 1;', '', '  b = 2;', '', '  c = 3;', '}');
		const rules: Entries = { 'separate-invocation-runs': 'error', 'separate-member-groups': 'error' };
		const statements = lines('log(1);', 'log(2);', 'log(3);', '', 'class A {', '  a = 1;', '', '  b = 2;', '', '  c = 3;', '}');
		const members = lines('log(1);', '', 'log(2);', '', 'log(3);', '', 'class A {', '  a = 1;', '  b = 2;', '  c = 3;', '}');
		const both = lines('log(1);', 'log(2);', 'log(3);', '', 'class A {', '  a = 1;', '  b = 2;', '  c = 3;', '}');

		assert.equal(fix(code, { ...rules, 'keep-statement-runs-together': 'error' }), statements);
		assert.equal(fix(code, { ...rules, 'keep-member-runs-together': 'error' }), members);
		assert.equal(fix(code, { ...rules, 'keep-statement-runs-together': 'error', 'keep-member-runs-together': 'error' }), both);
	});

	// The sections of a run by their sizes, the sizes of the sections the rule leaves, and the blank lines it reports.
	// A lone unit beside one section is absorbed, and a lone unit between two sections stays a section.
	const sectionRows: [name: string, sizes: number[], left: number[], reported: number][] = [
		['1|2|2', [1, 2, 2], [3, 2], 1],
		['2|2|1', [2, 2, 1], [2, 3], 1],
		['1|1|2|1|1', [1, 1, 2, 1, 1], [6], 4],
		['2|1|2', [2, 1, 2], [2, 1, 2], 0],
		['2|2|2', [2, 2, 2], [2, 2, 2], 0],
	];

	/** Writes the units one to a line, numbered from 1, with a blank line between sections. */
	function laySections(sizes: number[], write: (number: number) => string): string[] {
		let next = 1;
		return sizes.flatMap((size, index) => [...(index === 0 ? [] : ['']), ...Array.from({ length: size }, () => write(next++))]);
	}

	for (const [name, sizes, left, reported] of sectionRows) {
		it(`leaves the blank line between two sections, absorbing a lone statement unless it sits between two sections, with ${name}`, () => {
			const rules: Entries = { [statementKeeper]: 'error', 'separate-invocation-runs': 'error' };
			const write = (number: number): string => `log(${number});`;

			assert.equal(fix(lines(...laySections(sizes, write)), rules), lines(...laySections(left, write)));
			assert.equal(reporters(lines(...laySections(sizes, write)), rules).length, reported);
		});

		it(`leaves the blank line between two sections, absorbing a lone member unless it sits between two sections, with ${name}`, () => {
			const rules: Entries = { [memberKeeper]: 'error', 'separate-member-groups': 'error' };
			const write = (number: number): string => `  m${number} = ${number};`;
			const wrap = (rows: string[]): string => lines('class A {', ...rows, '}');

			assert.equal(fix(wrap(laySections(sizes, write)), rules), wrap(laySections(left, write)));
			assert.equal(reporters(wrap(laySections(sizes, write)), rules).length, reported);
		});
	}

	it('reads two units on one line as tight, so two such lines are two sections', () => {
		const statements = lines('log(1); log(2);', '', 'log(3); log(4);');
		const members = lines('class A {', '  a = 1; b = 2;', '', '  c = 3; d = 4;', '}');

		assert.equal(fix(statements, { [statementKeeper]: 'error', 'separate-invocation-runs': 'error' }), statements);
		assert.equal(fix(members, { [memberKeeper]: 'error', 'separate-member-groups': 'error' }), members);
	});

	it('is reported by the half of its own kind of container, and by neither for the other half', () => {
		const code = lines('log(1);', '', 'log(2);', '', 'class A {', '  a = 1;', '', '  b = 2;', '', '  c = 3;', '}');
		const rules: Entries = { 'separate-invocation-runs': 'error', 'separate-member-groups': 'error' };

		assert.deepEqual(reporters(code, { ...rules, 'keep-statement-runs-together': 'error' }), ['stanza/keep-statement-runs-together']);

		assert.deepEqual(reporters(code, { ...rules, 'keep-member-runs-together': 'error' }), [
			'stanza/keep-member-runs-together',
			'stanza/keep-member-runs-together',
		]);
	});
});

describe('the rules that gather loose runs', () => {
	const run = ['a();', '', 'b();', '', 'c();', '', 'd();'];
	const statementHalves = halvesOf('gather-loose-statements', 'gather-loose-members', 'statement');
	const memberHalves = halvesOf('gather-loose-members', 'gather-loose-statements', 'member');
	const limitOf = (maxLooseRunSize: number): Record<string, unknown> => ({ stanza: { maxLooseRunSize } });

	for (const [name, gather] of statementHalves) {
		it(`leaves the blank line a rule that adds one asks for, so the run it ends is counted without it, with ${name}`, () => {
			const code = lines('const a = 1;', '', 'const b = 2;', '', 'foo();', '', 'bar();', '', 'baz();');

			assert.equal(fix(code, gather), lines('const a = 1;', 'const b = 2;', 'foo();', 'bar();', 'baz();'));
			assert.equal(fix(code, { ...gather, 'separate-declaration-runs': 'error' }), code);
		});

		it(`keeps the blank line above a jump whose stanza spans two lines once the run is gathered, and only where separate-jump-statements is on, with ${name}`, () => {
			const code = lines('function f() {', ...run.map((line) => (line === '' ? line : `  ${line}`)), '', '  return 1;', '}');

			assert.equal(fix(code, gather), lines('function f() {', '  a();', '  b();', '  c();', '  d();', '  return 1;', '}'));

			assert.equal(
				fix(code, { ...gather, 'separate-jump-statements': 'error' }),
				lines('function f() {', '  a();', '  b();', '  c();', '  d();', '', '  return 1;', '}'),
			);
		});

		it(`gathers a jump below a stanza of one line, because separate-jump-statements asks for nothing there, with ${name}`, () => {
			const rules: Entries = { ...gather, 'separate-jump-statements': 'error' };
			const one = lines('if (a) {', '  go();', '}', '', 'run();', '', 'return 1;');
			const two = lines('if (a) {', '  go();', '}', '', 'first();', '', 'second();', '', 'return 1;');

			assert.equal(fix(one, rules, limitOf(1)), lines('if (a) {', '  go();', '}', '', 'run();', 'return 1;'));
			assert.equal(fix(one, gather, limitOf(1)), lines('if (a) {', '  go();', '}', '', 'run();', 'return 1;'));
			assert.equal(fix(two, rules, limitOf(1)), lines('if (a) {', '  go();', '}', '', 'first();', 'second();', '', 'return 1;'));
			assert.equal(fix(two, gather, limitOf(1)), lines('if (a) {', '  go();', '}', '', 'first();', 'second();', 'return 1;'));
		});

		it(`leaves a run alone when the jump below it keeps its blank line and the rest holds no more than the limit, with ${name}`, () => {
			const code = lines('function f() {', '  a();', '', '  b();', '', '  c();', '', '  return 1;', '}');

			assert.equal(fix(code, { ...gather, 'separate-jump-statements': 'error' }), code);
			assert.equal(fix(code, gather), lines('function f() {', '  a();', '  b();', '  c();', '  return 1;', '}'));
		});

		it(`settles a jump to one text with separate-jump-statements on, in either order of the rules, with ${name}`, () => {
			const code = lines('function f() {', '  a();', '', '  b();', '', '  c();', '', '  d();', '', '  return 1;', '}');
			const gathered = lines('function f() {', '  a();', '  b();', '  c();', '  d();', '', '  return 1;', '}');
			const entries = Object.entries({ ...gather, 'separate-jump-statements': 'error' } as Entries);

			for (const order of [entries, entries.toReversed()]) {
				const rules = Object.fromEntries(order);

				assert.equal(fix(code, rules), gathered);
				assert.equal(fix(gathered, rules), gathered);
				assert.deepEqual(reporters(gathered, rules), []);
			}
		});

		it(`keeps a unit tight against the jump out of the run, so the stanza above the jump stays one line, with ${name}`, () => {
			const code = lines('function f() {', '  a();', '', '  b();', '', '  c();', '', '  d();', '  return 1;', '}');
			assert.deepEqual(reporters(code, { ...gather, 'separate-jump-statements': 'error' }), []);
		});

		it(`lets a unit tight against a neighbor that a rule asks to set apart join the run, with ${name}`, () => {
			const code = lines('foo({', '  x: 1,', '});', 'a();', '', 'b();', '', 'c();', '', 'd();');
			const expected = lines('foo({', '  x: 1,', '});', '', 'a();', 'b();', 'c();', 'd();');

			assert.equal(fix(code, gather), code);
			assert.equal(fix(code, { ...gather, 'separate-multi-line-constructs': 'error' }), expected);
		});

		it(`reports a blank line once when the rule that keeps statement runs together asks for it too, with ${name}`, () => {
			const code = lines('log(1);', '', 'log(2);', '', 'log(3);', '', 'log(4);');
			const rules: Entries = { ...gather, 'keep-statement-runs-together': 'error', 'separate-invocation-runs': 'error' };

			assert.deepEqual(
				reporters(code, rules),
				Array.from({ length: 3 }, () => 'stanza/keep-statement-runs-together'),
			);

			assert.equal(fix(code, rules), lines('log(1);', 'log(2);', 'log(3);', 'log(4);'));
		});

		it(`leaves a container of two units to keep-two-units-together, with ${name}`, () => {
			const code = lines('function f() {', '  a();', '', '  b();', '}');

			assert.deepEqual(reporters(code, { ...gather, 'keep-two-units-together': 'error' }), ['stanza/keep-two-units-together']);
			assert.deepEqual(reporters(code, gather), []);
		});
	}

	for (const [name, gather] of memberHalves) {
		it(`leaves the blank line a rule that adds one asks for, so the run it ends is counted without it, with ${name}`, () => {
			const code = lines('class A {', '  a = 1;', '', '  b = 2;', '', '  c = 3;', '', '  d = 4;', '', '  m() {}', '', '  n() {}', '}');

			assert.equal(fix(code, gather), lines('class A {', '  a = 1;', '  b = 2;', '  c = 3;', '  d = 4;', '  m() {}', '  n() {}', '}'));

			assert.equal(
				fix(code, { ...gather, 'separate-member-groups': 'error' }),
				lines('class A {', '  a = 1;', '  b = 2;', '  c = 3;', '  d = 4;', '', '  m() {}', '', '  n() {}', '}'),
			);
		});

		it(`reports a blank line once when the rule that keeps member runs together asks for it too, with ${name}`, () => {
			const code = lines('class A {', '  a = 1;', '', '  b = 2;', '', '  c = 3;', '', '  d = 4;', '}');
			const rules: Entries = { ...gather, 'keep-member-runs-together': 'error', 'separate-member-groups': 'error' };

			assert.deepEqual(
				reporters(code, rules),
				Array.from({ length: 3 }, () => 'stanza/keep-member-runs-together'),
			);

			assert.equal(fix(code, rules), lines('class A {', '  a = 1;', '  b = 2;', '  c = 3;', '  d = 4;', '}'));
		});

		it(`leaves a container of two members to keep-two-units-together, with ${name}`, () => {
			const code = lines('class A {', '  a = 1;', '', '  b = 2;', '}');

			assert.deepEqual(reporters(code, { ...gather, 'keep-two-units-together': 'error' }), ['stanza/keep-two-units-together']);
			assert.deepEqual(reporters(code, gather), []);
		});
	}

	it('leaves the containers of the other half as written when only one half is enabled', () => {
		const code = lines(...run, '', 'class A {', '  a = 1;', '', '  b = 2;', '', '  c = 3;', '', '  d = 4;', '}');
		const statements = lines('a();', 'b();', 'c();', 'd();', '', 'class A {', '  a = 1;', '', '  b = 2;', '', '  c = 3;', '', '  d = 4;', '}');
		const members = lines(...run, '', 'class A {', '  a = 1;', '  b = 2;', '  c = 3;', '  d = 4;', '}');
		const both = lines('a();', 'b();', 'c();', 'd();', '', 'class A {', '  a = 1;', '  b = 2;', '  c = 3;', '  d = 4;', '}');

		assert.equal(fix(code, { 'gather-loose-statements': 'error' }), statements);
		assert.equal(fix(code, { 'gather-loose-members': 'error' }), members);
		assert.equal(fix(code, { 'gather-loose-statements': 'error', 'gather-loose-members': 'error' }), both);
	});

	it('reads one limit from settings.stanza for both halves, and turns both off with 0', () => {
		const code = lines('a();', '', 'b();', '', 'c();', '', 'class A {', '  a = 1;', '', '  b = 2;', '', '  c = 3;', '}');
		const rules: Entries = { 'gather-loose-statements': 'error', 'gather-loose-members': 'error' };

		assert.equal(fix(code, rules), code);
		assert.equal(fix(code, rules, limitOf(2)), lines('a();', 'b();', 'c();', '', 'class A {', '  a = 1;', '  b = 2;', '  c = 3;', '}'));
		assert.equal(fix(code, rules, limitOf(0)), code);
	});
});

describe('the removal rules together', () => {
	const sample = lines(
		'start();',
		'',
		'log(1);',
		'',
		'log(2);',
		'',
		'log(3);',
		'',
		'log(4);',
		'',
		'function f() {',
		'  a();',
		'',
		'  b();',
		'',
		'  c();',
		'',
		'  d();',
		'',
		'  return 1;',
		'}',
		'',
		'class A {',
		'  a = 1;',
		'',
		'  b = 2;',
		'',
		'  c = 3;',
		'',
		'  d = 4;',
		'',
		'  m() {}',
		'',
		'  n() {}',
		'}',
	);

	const names = [
		'gather-loose-statements',
		'gather-loose-members',
		'keep-statement-runs-together',
		'keep-member-runs-together',
		'separate-invocation-runs',
		'separate-member-groups',
		'separate-jump-statements',
		'separate-multi-line-constructs',
	];

	it('settle to one text in either order of the rules, for every subset of them', () => {
		for (let mask = 1; mask < 2 ** names.length; mask += 1) {
			const entries = names.filter((_, index) => (mask >> index) % 2 === 1).map((name) => [name, 'error'] as const);
			const label = entries.map(([name]) => name).join(', ');
			const forward = Object.fromEntries(entries);
			const fixed = fix(sample, forward);

			assert.equal(fix(sample, Object.fromEntries(entries.toReversed())), fixed, label);
			assert.equal(fix(fixed, forward), fixed, label);
			assert.deepEqual(reporters(fixed, forward), [], label);
		}
	});
});

describe('separate-base-call', () => {
	it('reports a gap once when the guard clauses ask for it too', () => {
		const code = lines('class A extends B {', '  run() {', '    if (!this.ok) return;', '    invariant(this.ok);', '    super.run();', '  }', '}');

		assert.deepEqual(reporters(code, { 'separate-base-call': 'error', 'separate-guard-clauses': 'error' }), ['stanza/separate-base-call']);
		assert.deepEqual(reporters(code, { 'separate-guard-clauses': 'error' }), ['stanza/separate-guard-clauses']);
		assert.deepEqual(reporters(code, { 'separate-base-call': 'error' }), ['stanza/separate-base-call']);
	});

	it('reads the guards by settings.stanza.guardRoots, as separate-guard-clauses does and whether or not it is enabled', () => {
		const code = lines('class A extends B {', '  run() {', '    check(this.ok);', '    super.run();', '    this.a();', '    this.b();', '  }', '}');
		const settings = { stanza: { guardRoots: ['check'] } };

		const reportersWith = (rules: Record<string, Linter.RuleEntry>) =>
			new Linter().verify(code, createConfig(rules, settings), { filename: 'file.ts' }).map((message) => message.ruleId);

		assert.deepEqual(reporters(code, { 'separate-base-call': 'error' }), []);
		assert.deepEqual(reporters(code, { 'separate-base-call': 'error', 'separate-guard-clauses': 'error' }), []);
		assert.deepEqual(reportersWith({ 'separate-base-call': 'error' }), ['stanza/separate-base-call']);

		assert.deepEqual(reportersWith({ 'separate-base-call': 'error', 'separate-guard-clauses': 'error' }), [
			'stanza/separate-guard-clauses',
			'stanza/separate-base-call',
		]);
	});

	it('ends the stanza that separate-jump-statements reads above a jump', () => {
		const code = lines('class A extends B {', '  run() {', '    super.run();', '    this.a();', '    return this.b;', '  }', '}');

		assert.deepEqual(reporters(code, { 'separate-base-call': 'error' }), ['stanza/separate-base-call']);
		assert.deepEqual(reporters(code, { 'separate-base-call': 'error', 'separate-jump-statements': 'error' }), ['stanza/separate-base-call']);
	});
});

describe('separate-modifier-groups', () => {
	it('is reported once where separate-member-groups asks for the same gap', () => {
		const code = lines('class A {', '  a = 1;', '  b = 2;', '  c() {}', '  d() {}', '}');

		assert.deepEqual(reporters(code, { 'separate-member-groups': 'error', 'separate-modifier-groups': 'error' }), ['stanza/separate-member-groups']);
		assert.deepEqual(reporters(code, { 'separate-modifier-groups': 'error' }), ['stanza/separate-modifier-groups']);
	});
});

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

describe('the levels of the rules that remove blank lines', () => {
	const list = lines('const o = {', '  a: 1,', '', '  b: 2,', '};');
	const flat = lines('const o = {', '  a: 1,', '  b: 2,', '};');

	it('puts keep-single-line-elements-together in the minimal, moderate and strong presets and in no other', () => {
		for (const level of ['minimal', 'moderate', 'strong'] as const) {
			assert.ok('keep-single-line-elements-together' in readPresetRules(level), level);
		}

		assert.ok(!('keep-single-line-elements-together' in readPresetRules('none')));
	});

	it('removes the blank line between two single-line elements at the minimal level, where no rule that adds one is on', () => {
		assert.deepEqual(
			lintWithPreset(list, 'minimal').map((message) => message.ruleId),
			['stanza/keep-single-line-elements-together'],
		);

		assert.equal(fixWithPreset(list, 'minimal'), flat);
		assert.equal(fixWithPreset(list, 'moderate'), flat);
		assert.equal(fixWithPreset(list, 'strong'), flat);
	});

	it('leaves a multi-line element as written below the strong level, where separate-multi-line-elements is not on', () => {
		const tight = lines('const o = {', '  a: 1,', '  b: {', '    c: 1,', '  },', '};');
		const apart = lines('const o = {', '  a: 1,', '', '  b: {', '    c: 1,', '  },', '};');
		const mixed = lines('const o = {', '  a: 1,', '', '  b: 2,', '  c: {', '    d: 1,', '  },', '};');

		assert.equal(fixWithPreset(apart, 'minimal'), apart);
		assert.equal(fixWithPreset(tight, 'minimal'), tight);
		assert.equal(fixWithPreset(tight, 'strong'), apart);
		assert.equal(fixWithPreset(mixed, 'minimal'), lines('const o = {', '  a: 1,', '  b: 2,', '  c: {', '    d: 1,', '  },', '};'));
		assert.equal(fixWithPreset(mixed, 'strong'), lines('const o = {', '  a: 1,', '  b: 2,', '', '  c: {', '    d: 1,', '  },', '};'));
	});

	it('keeps both rules that keep a run together out of every preset, and joins a run only where an entry enables them', () => {
		const code = lines('log(1);', '', 'log(2);', '', 'log(3);');

		for (const level of ['none', 'minimal', 'moderate', 'strong'] as const) {
			assert.ok(!('keep-statement-runs-together' in readPresetRules(level)), level);
			assert.ok(!('keep-member-runs-together' in readPresetRules(level)), level);
		}

		assert.equal(fixWithPreset(code, 'strong'), code);
		assert.equal(fixWithPreset(code, 'strong', 'file.ts', true), lines('log(1);', 'log(2);', 'log(3);'));

		assert.deepEqual(
			lintWithPreset(code, 'moderate', 'file.ts', true).map((message) => message.ruleId),
			['stanza/keep-statement-runs-together', 'stanza/keep-statement-runs-together'],
		);

		assert.deepEqual(lintWithPreset(code, 'minimal', 'file.ts', true), []);
	});

	it('puts both rules that gather loose runs in the strong preset and in no level below it', () => {
		for (const name of ['gather-loose-statements', 'gather-loose-members']) {
			assert.ok(name in readPresetRules('strong'), name);

			for (const level of ['none', 'minimal', 'moderate'] as const) {
				assert.ok(!(name in readPresetRules(level)), `${name} at ${level}`);
			}
		}

		const code = lines(
			'class A {',
			'  a = 1;',
			'',
			'  b = 2;',
			'',
			'  c = 3;',
			'',
			'  d = 4;',
			'}',
			'',
			...['a();', '', 'b();', '', 'c();', '', 'd();'],
		);

		assert.equal(fixWithPreset(code, 'moderate'), code);

		assert.equal(
			fixWithPreset(code, 'strong'),
			lines('class A {', '  a = 1;', '  b = 2;', '  c = 3;', '  d = 4;', '}', '', 'a();', 'b();', 'c();', 'd();'),
		);
	});
});

describe('separate-multi-line-jsx-children', () => {
	const tightLines = ['const view = (', '  <div>', '    <A />', '    <B>', '      x', '    </B>', '  </div>', ');'];
	const apartLines = ['const view = (', '  <div>', '    <A />', '', '    <B>', '      x', '    </B>', '  </div>', ');'];
	const tight = lines(...tightLines);
	const apart = lines(...apartLines);
	const file = 'file.tsx';

	it('is reported once beside separate-multi-line-constructs, which leaves the children alone and is left alone by it', () => {
		const code = lines('if (a) {', '  foo();', '}', 'bar();', ...tightLines);
		const both: Record<string, Linter.RuleEntry> = { 'separate-multi-line-constructs': 'error', 'separate-multi-line-jsx-children': 'error' };

		assert.deepEqual(reporters(tight, { 'separate-multi-line-constructs': 'error' }, file), []);
		assert.deepEqual(reporters(tight, both, file), ['stanza/separate-multi-line-jsx-children']);
		assert.deepEqual(reporters(code, { 'separate-multi-line-jsx-children': 'error' }, file), ['stanza/separate-multi-line-jsx-children']);

		assert.deepEqual(reporters(code, both, file), [
			'stanza/separate-multi-line-constructs',
			'stanza/separate-multi-line-constructs',
			'stanza/separate-multi-line-jsx-children',
		]);
	});

	it('writes one text in either order of the two rules, and a second fix changes nothing', () => {
		const code = lines('if (a) {', '  foo();', '}', 'bar();', ...tightLines);
		const entries = Object.entries({ 'separate-multi-line-constructs': 'error', 'separate-multi-line-jsx-children': 'error' } as const);
		const expected = lines('if (a) {', '  foo();', '}', '', 'bar();', '', ...apartLines);

		for (const order of [entries, entries.toReversed()]) {
			const rules = Object.fromEntries(order);

			assert.equal(fix(code, rules, {}, file), expected);
			assert.equal(fix(expected, rules, {}, file), expected);
		}
	});

	it('is enabled at the strong level and not below it', () => {
		assert.equal(fixWithPreset(tight, 'strong', file), apart);
		assert.equal(fixWithPreset(tight, 'moderate', file), tight);
	});
});

describe('the roots', () => {
	it('make a call to a test root a test block that joins no run, whether or not separate-test-blocks is enabled', () => {
		const code = lines("it.todo('a');", "it.todo('b');", 'log(1);');
		const runs: Record<string, Linter.RuleEntry> = { 'separate-invocation-runs': 'error', 'separate-await-runs': 'error' };

		assert.deepEqual(reporters(code, runs), []);
		assert.deepEqual(reporters(code, { ...runs, 'separate-test-blocks': 'error' }), ['stanza/separate-test-blocks']);
		assert.deepEqual(reporters(code, runs, 'file.ts', { stanza: { testRoots: [] } }), ['stanza/separate-invocation-runs']);
	});

	it('make a name that is a test root and an assertion root a test block', () => {
		const code = lines('check(1);', 'check(2);', 'log(1);');
		const settings = { stanza: { assertionRoots: ['check'] } };

		assert.deepEqual(reporters(code, { 'separate-assertion-runs': 'error' }, 'file.ts', settings), ['stanza/separate-assertion-runs']);

		assert.deepEqual(
			reporters(code, { 'separate-assertion-runs': 'error' }, 'file.ts', { stanza: { ...settings.stanza, testRoots: ['check'] } }),
			[],
		);
	});

	it('read an empty list as no roots', () => {
		const code = lines('expect(1);', 'expect(2);', 'log(1);');

		assert.deepEqual(reporters(code, { 'separate-assertion-runs': 'error' }), ['stanza/separate-assertion-runs']);
		assert.deepEqual(reporters(code, { 'separate-assertion-runs': 'error' }, 'file.ts', { stanza: { assertionRoots: [] } }), []);
	});
});

describe('the switch case rules', () => {
	const mixed = lines(
		'switch (x) {',
		'  case 1: return a;',
		'',
		'  case 2: return b;',
		'  case 3:',
		'    foo();',
		'    break;',
		'',
		'  case 4:',
		'    bar();',
		'}',
	);

	const separate = 'separate-multi-line-switch-cases';
	const keepMulti = 'keep-multi-line-switch-cases-together';
	const keepSingle = 'keep-single-line-switch-cases-together';

	const apart = lines(
		'switch (x) {',
		'  case 1: return a;',
		'  case 2: return b;',
		'',
		'  case 3:',
		'    foo();',
		'    break;',
		'',
		'  case 4:',
		'    bar();',
		'}',
	);

	const tight = lines(
		'switch (x) {',
		'  case 1: return a;',
		'  case 2: return b;',
		'  case 3:',
		'    foo();',
		'    break;',
		'  case 4:',
		'    bar();',
		'}',
	);

	it('leave each gap to one rule: the separating half and the single-line rule split the cases between them', () => {
		const rules: Entries = { [separate]: 'error', [keepSingle]: 'error' };

		assert.equal(fix(mixed, rules), apart);
		assert.equal(fix(apart, rules), apart);
		assert.deepEqual(reporters(mixed, rules), [`stanza/${keepSingle}`, `stanza/${separate}`]);
	});

	it('hand the gaps of the tight half to it while it is enabled, so the separating half asks for none of them', () => {
		const both: Entries = { [separate]: 'error', [keepMulti]: 'error' };

		assert.deepEqual(reporters(apart, { [separate]: 'error' }), []);
		assert.deepEqual(reporters(tight, { [separate]: 'error' }), [`stanza/${separate}`, `stanza/${separate}`]);
		assert.deepEqual(reporters(tight, both), []);
		assert.deepEqual(reporters(apart, both), [`stanza/${keepMulti}`, `stanza/${keepMulti}`]);

		assert.equal(
			fix(apart, both),
			lines('switch (x) {', '  case 1: return a;', '  case 2: return b;', '  case 3:', '    foo();', '    break;', '  case 4:', '    bar();', '}'),
		);

		assert.equal(fix(apart, { [keepMulti]: 'error' }), fix(apart, both));
	});

	it('write one text in either order of the three rules, for every subset of them', () => {
		const names = [separate, keepMulti, keepSingle, 'separate-multi-line-constructs'];

		for (let mask = 1; mask < 2 ** names.length; mask += 1) {
			const entries = names.filter((_, index) => (mask >> index) % 2 === 1).map((name) => [name, 'error'] as const);
			const label = entries.map(([name]) => name).join(', ');
			const forward = Object.fromEntries(entries);

			for (const code of [mixed, apart, tight]) {
				const fixed = fix(code, forward);

				assert.equal(fix(code, Object.fromEntries(entries.toReversed())), fixed, label);
				assert.equal(fix(fixed, forward), fixed, label);
				assert.deepEqual(reporters(fixed, forward), [], label);
			}
		}
	});

	it('is judged beside separate-multi-line-constructs, which leaves the cases and judges the statements inside them', () => {
		const code = lines(
			'switch (x) {',
			'  case 1:',
			'    foo();',
			'    if (a) {',
			'      bar();',
			'    }',
			'    break;',
			'  case 2:',
			'    baz();',
			'}',
		);

		const rules: Entries = { 'separate-multi-line-constructs': 'error', [separate]: 'error' };

		assert.deepEqual(reporters(code, rules), [
			'stanza/separate-multi-line-constructs',
			'stanza/separate-multi-line-constructs',
			`stanza/${separate}`,
		]);

		assert.deepEqual(reporters(code, { 'separate-multi-line-constructs': 'error' }), [
			'stanza/separate-multi-line-constructs',
			'stanza/separate-multi-line-constructs',
		]);
	});

	it('enable the separating half at the moderate level, the single-line rule at the minimal level and the tight half in no preset', () => {
		assert.ok(!(separate in readPresetRules('minimal')));
		assert.ok(separate in readPresetRules('moderate'));
		assert.ok(keepSingle in readPresetRules('minimal'));
		assert.ok(!(keepMulti in readPresetRules('strong')));
		assert.ok(keepMulti in readPresetRules('strong', true));
		assert.equal(fixWithPreset(tight, 'minimal'), tight);

		assert.equal(
			fixWithPreset(mixed, 'minimal'),

			lines(
				'switch (x) {',
				'  case 1: return a;',
				'  case 2: return b;',
				'  case 3:',
				'    foo();',
				'    break;',
				'',
				'  case 4:',
				'    bar();',
				'}',
			),
		);

		assert.equal(fixWithPreset(mixed, 'moderate'), apart);
		assert.equal(fixWithPreset(tight, 'strong'), apart);
		assert.equal(fixWithPreset(apart, 'strong', 'file.ts', true), tight);
	});

	it('turn the separating half into the tight half with one more entry over the preset, or with the half off', () => {
		const over: Entries = { ...readPresetRules('moderate'), [keepMulti]: 'error' };
		const off: Entries = { ...readPresetRules('moderate'), [separate]: 'off', [keepMulti]: 'error' };

		assert.equal(fix(apart, over), tight);
		assert.equal(fix(apart, off), tight);
		assert.equal(fix(tight, over), tight);
	});
});

describe('separate-import-groups', () => {
	const code = lines("'use strict';", "import './setup.ts';", "import { a } from 'a';", "import b from './b.ts';", "import 'c';", 'run(a, b);');

	const expected = lines(
		"'use strict';",
		'',
		"import './setup.ts';",
		'',
		"import { a } from 'a';",
		"import b from './b.ts';",
		'',
		"import 'c';",
		'',
		'run(a, b);',
	);

	const names = ['separate-directives', 'separate-import-section', 'separate-import-groups', 'separate-multi-line-constructs'];

	it('asks for the gaps between the imports and leaves the gaps beside the directives and the code to the other rules', () => {
		assert.deepEqual(reporters(code, { 'separate-import-groups': 'error' }), ['stanza/separate-import-groups', 'stanza/separate-import-groups']);
		assert.deepEqual(reporters(code, { 'separate-import-section': 'error' }), ['stanza/separate-import-section']);
		assert.deepEqual(reporters(code, { 'separate-directives': 'error' }), ['stanza/separate-directives']);
	});

	it('writes one text in either order of the rules, for every subset of them, and a second fix changes nothing', () => {
		for (let mask = 1; mask < 2 ** names.length; mask += 1) {
			const entries = names.filter((_, index) => (mask >> index) % 2 === 1).map((name) => [name, 'error'] as const);
			const label = entries.map(([name]) => name).join(', ');
			const forward = Object.fromEntries(entries);
			const fixed = fix(code, forward);

			assert.equal(fix(code, Object.fromEntries(entries.toReversed())), fixed, label);
			assert.equal(fix(fixed, forward), fixed, label);
			assert.deepEqual(reporters(fixed, forward), [], label);
		}

		assert.equal(fix(code, Object.fromEntries(names.map((name) => [name, 'error'] as const))), expected);
	});

	it('leaves a multi-line import beside an import of the same kind to no rule, and one of another kind to this rule', () => {
		const multi = lines('import {', '  a,', '  b,', "} from 'a';", "import { c } from 'c';", "import 'd';", 'run();');
		const rules: Entries = { 'separate-import-groups': 'error', 'separate-multi-line-constructs': 'error', 'separate-import-section': 'error' };

		assert.equal(fix(multi, rules), lines('import {', '  a,', '  b,', "} from 'a';", "import { c } from 'c';", '', "import 'd';", '', 'run();'));
	});

	it('is enabled at the strong level and not below it, and the option stays off in the preset', () => {
		const types = lines("import type { A } from 'a';", "import { b } from 'b';", "import 'c';", 'run();');

		assert.equal(fixWithPreset(types, 'moderate'), lines("import type { A } from 'a';", "import { b } from 'b';", "import 'c';", '', 'run();'));
		assert.equal(fixWithPreset(types, 'strong'), lines("import type { A } from 'a';", "import { b } from 'b';", '', "import 'c';", '', 'run();'));
	});
});

describe('the JSX child rules', () => {
	const file = 'file.tsx';
	const separateMulti = 'separate-multi-line-jsx-children';
	const keepMulti = 'keep-multi-line-jsx-children-together';
	const keepSingle = 'keep-single-line-jsx-children-together';
	const commented = 'separate-commented-jsx-children';

	const sample = lines(
		'const view = (',
		'  <section>',
		'    <A />',
		'',
		'    <B />',
		'    {/* the body */}',
		'    <C />',
		'',
		'    <D>',
		'      <E />',
		'',
		'      <F />',
		'    </D>',
		'',
		'    {/* the footer */}',
		'    <G />',
		'    <H>',
		'      x',
		'    </H>',
		'  </section>',
		');',
	);

	const strong = lines(
		'const view = (',
		'  <section>',
		'    <A />',
		'    <B />',
		'',
		'    {/* the body */}',
		'    <C />',
		'',
		'    <D>',
		'      <E />',
		'      <F />',
		'    </D>',
		'',
		'    {/* the footer */}',
		'    <G />',
		'',
		'    <H>',
		'      x',
		'    </H>',
		'  </section>',
		');',
	);

	const tight = lines(
		'const view = (',
		'  <section>',
		'    <A />',
		'    <B />',
		'',
		'    {/* the body */}',
		'    <C />',
		'    <D>',
		'      <E />',
		'      <F />',
		'    </D>',
		'',
		'    {/* the footer */}',
		'    <G />',
		'    <H>',
		'      x',
		'    </H>',
		'  </section>',
		');',
	);

	it('write the strong level text: the single-line children tight, the comments and the multi-line children apart', () => {
		assert.equal(fixWithPreset(sample, 'strong', file), strong);
		assert.equal(fixWithPreset(strong, 'strong', file), strong);
		assert.deepEqual(lintWithPreset(strong, 'strong', file), []);
	});

	it('are enabled at the strong level and not below it', () => {
		assert.equal(fixWithPreset(sample, 'moderate', file), sample);
		assert.ok(commented in readPresetRules('strong') && keepSingle in readPresetRules('strong'));
		assert.ok(!(commented in readPresetRules('moderate')) && !(keepSingle in readPresetRules('moderate')));
		assert.ok(!(keepMulti in readPresetRules('strong')) && keepMulti in readPresetRules('strong', true));
	});

	it('hand the multi-line gaps to the tight half while it is enabled, whether it comes with the preset or replaces the separating half', () => {
		const over: Entries = { ...readPresetRules('strong'), [keepMulti]: 'error' };
		const off: Entries = { ...readPresetRules('strong'), [separateMulti]: 'off', [keepMulti]: 'error' };

		assert.equal(fix(sample, over, {}, file), tight);
		assert.equal(fix(sample, off, {}, file), tight);
		assert.equal(fix(tight, over, {}, file), tight);
		assert.deepEqual(reporters(tight, over, file), []);
	});

	it('leave a gap beside a multi-line child to the multi-line rule and a gap beside a comment to the commented rule', () => {
		const code = lines('const view = (', '  <div>', '    <A />', '    {/* note */}', '    <B />', '    <C>', '      x', '    </C>', '  </div>', ');');
		const rules: Entries = { [separateMulti]: 'error', [commented]: 'error', [keepSingle]: 'error' };

		assert.deepEqual(reporters(code, rules, file), [`stanza/${commented}`, `stanza/${separateMulti}`]);
		assert.deepEqual(reporters(code, { [commented]: 'error' }, file), [`stanza/${commented}`]);
		assert.deepEqual(reporters(code, { [separateMulti]: 'error' }, file), [`stanza/${separateMulti}`]);
	});

	it('write one text in either order of the rules, for every subset of them, and a second fix changes nothing', () => {
		const names = [separateMulti, keepMulti, keepSingle, commented, 'separate-multi-line-constructs'];

		for (let mask = 1; mask < 2 ** names.length; mask += 1) {
			const entries = names.filter((_, index) => (mask >> index) % 2 === 1).map((name) => [name, 'error'] as const);
			const label = entries.map(([name]) => name).join(', ');
			const forward = Object.fromEntries(entries);

			for (const code of [sample, strong, tight]) {
				const fixed = fix(code, forward, {}, file);

				assert.equal(fix(code, Object.fromEntries(entries.toReversed()), {}, file), fixed, label);
				assert.equal(fix(fixed, forward, {}, file), fixed, label);
				assert.deepEqual(reporters(fixed, forward, file), [], label);
			}
		}
	});

	it('leave an element with text or a space as written, whichever rule is on', () => {
		const text = lines(
			'const view = (',
			'  <div>',
			'    <A />',
			'',
			'    {/* note */}',
			'    <B>',
			'      x',
			'    </B>',
			'    text',
			'  </div>',
			');',
		);

		const space = lines(
			'const view = (',
			'  <div>',
			'    <A />',
			"    {' '}",
			'    {/* note */}',
			'    <B>',
			'      x',
			'    </B>',
			'  </div>',
			');',
		);

		for (const code of [text, space]) {
			assert.equal(fixWithPreset(code, 'strong', file, true), code);
			assert.equal(fix(code, { [keepMulti]: 'error', [keepSingle]: 'error' }, {}, file), code);
		}
	});
});
