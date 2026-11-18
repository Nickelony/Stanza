import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { Linter } from 'eslint';

import {
	createConfig,
	fix,
	fixWithPreset,
	halvesOf,
	lines,
	readPresetRules,
	readSuppressedMessages,
	reporters,
	settled,
	type Entries,
} from '../helpers.ts';

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

describe('the yield runs', () => {
	const yieldRuns = 'separate-yield-runs';
	const strong = readPresetRules('strong');
	const generator = (...body: string[]): string => lines('function* f() {', ...body, '}');
	const tight = generator('  start();', '  yield a;', '  yield b;', '  stop();');
	const apart = generator('  start();', '', '  yield a;', '  yield b;', '', '  stop();');

	it('is enabled at the strong level and not below it', () => {
		assert.equal(fixWithPreset(tight, 'strong'), apart);
		assert.equal(fixWithPreset(tight, 'moderate'), tight);
		assert.equal(fixWithPreset(apart, 'strong'), apart);
	});

	it('writes one text in one pass in every order of the rules, beside the rules that read the same statements', () => {
		const sources = [
			tight,
			generator('  const a = 1;', '  yield a;', '  yield* b;', '  later();', '  yield c;', '  return a;'),
			generator('  yield a;', '', '  yield b;', '', '  yield c;', '', '  yield d;', '', '  yield e;'),
			generator('  start();', '  yield a;', '', '  yield b;', '  stop();'),
		];

		for (const source of sources) {
			const entries = Object.entries({ ...strong, 'keep-statement-runs-together': 'error' } as Record<string, Linter.RuleEntry>);
			const forward = settled(source, Object.fromEntries(entries));

			assert.equal(settled(source, Object.fromEntries(entries.toReversed())), forward);
		}
	});

	it('is read by the rule that keeps a run together, which joins the run while the rule is enabled', () => {
		const spaced = generator('  start();', '  yield a;', '', '  yield b;', '  stop();');
		const keep: Entries = { [yieldRuns]: 'error', 'keep-statement-runs-together': 'error' };

		assert.equal(settled(spaced, keep), apart);
		assert.equal(settled(spaced, { [yieldRuns]: 'error' }), generator('  start();', '', '  yield a;', '', '  yield b;', '', '  stop();'));
		assert.equal(fix(spaced, { 'keep-statement-runs-together': 'error' }), spaced);
	});

	it('leaves a gap to the first rule of the owner order that asks for it, as the other run rules do', () => {
		const code = generator('  start();', '  yield a;', '  yield b;', '  stop({', '    c,', '  });');

		assert.deepEqual(reporters(code, { [yieldRuns]: 'error' }), [`stanza/${yieldRuns}`, `stanza/${yieldRuns}`]);

		assert.deepEqual(reporters(code, { [yieldRuns]: 'error', 'separate-multi-line-constructs': 'error' }), [
			`stanza/${yieldRuns}`,
			'stanza/separate-multi-line-constructs',
		]);
	});

	it('is silenced by an eslint-disable comment, which leaves the gap as written', () => {
		const directive = `// eslint-disable-next-line stanza/${yieldRuns}`;
		const rules: Entries = { [yieldRuns]: 'error' };
		const silenced = generator('  start();', `  ${directive}`, '  yield a;', '  yield b;', '  stop();');
		const linter = new Linter();

		linter.verify(silenced, createConfig(rules), { filename: 'file.ts' });

		assert.deepEqual(
			readSuppressedMessages(linter).map((message) => message.ruleId),
			[`stanza/${yieldRuns}`],
		);

		assert.equal(fix(silenced, rules), generator('  start();', `  ${directive}`, '  yield a;', '  yield b;', '', '  stop();'));

		const everywhere = lines(
			`/* eslint-disable stanza/${yieldRuns} */`,
			'function* f() {',
			'  start();',
			'  yield a;',
			'  yield b;',
			'  stop();',
			'}',
		);

		assert.equal(fix(everywhere, rules), everywhere);
		assert.deepEqual(new Linter().verify(everywhere, createConfig(rules), { filename: 'file.ts' }), []);
	});
});
