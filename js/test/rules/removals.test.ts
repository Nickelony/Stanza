import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { Linter } from 'eslint';

import { everyReportGroupSize } from '../../src/createGroupFix.ts';
import gatherLooseMembers from '../../src/rules/gather-loose-members.ts';
import gatherLooseStatements from '../../src/rules/gather-loose-statements.ts';
import { applyFix, assertGroupFix, createConfig, createRuleTester, crlf, lines, readSuppressedMessages, stable } from '../helpers.ts';

const tester = createRuleTester();

/** Reads the units as a loose run: each one followed by a blank line, but the last. */
function spaced(...units: string[]): string[] {
	return units.flatMap((unit, index) => (index === units.length - 1 ? [unit] : [unit, '']));
}

/** Sets the size a loose run may hold before the gather rules join it. */
function limit(maxLooseRunSize: unknown): { stanza: { maxLooseRunSize: unknown } } {
	return { stanza: { maxLooseRunSize } };
}

tester.run('gather-loose-statements', gatherLooseStatements as never, {
	valid: [
		{ code: lines(...spaced('a();', 'b();', 'c();')), name: 'a run at the limit stays' },
		{ code: lines('a();', 'b();', 'c();', 'd();', 'e();'), name: 'a run with no blank line has nothing to gather' },
		{ code: lines(...spaced('a();', 'b();', 'c();', 'd();', 'e();')), settings: limit(0), name: 'a limit of 0 turns the rule off' },
		{ code: lines(...spaced('a();', 'b();', 'c();', 'd();')), settings: limit(1000), name: 'a limit of 1000 is the greatest' },
		{ code: lines(...spaced('a();', 'b();', 'c();', 'd();', 'e();')), settings: limit(5) },

		{
			code: lines('a();', '', 'b();', '', '// note', 'c();', '', 'd();', '', 'e();'),
			name: 'a comment ends a run',
		},

		{
			code: lines('a();', '', 'b();', '', 'c();', '', 'd({', '  x: 1,', '});', '', 'e();'),
			name: 'a multi-line statement ends a run',
		},

		{
			code: lines('a();', 'b();', '', 'c();', '', 'd();', '', 'e();'),
			name: 'a statement tight against its other neighbor stays out of the run',
		},

		{ code: lines("'use strict';", '', 'a();', '', 'b();', '', 'c();'), name: 'a directive is no statement of the run' },

		{
			code: lines("import a from 'a';", '', "import b from 'b';", '', "import c from 'c';", '', "import d from 'd';"),
			name: 'imports are no statements',
		},

		{ code: lines(...spaced('a();', 'b();', 'c();'), 'd();', '', 'e();'), name: 'the units beside the tight pair are runs of one and of two' },

		{
			code: lines('class A {', ...spaced('  a = 1;', '  b = 2;', '  c = 3;', '  d = 4;'), '}'),
			name: 'the members of a class belong to the sibling rule',
		},

		{
			code: lines('interface A {', ...spaced('  a: string;', '  b: string;', '  c: string;', '  d: string;'), '}'),
			name: 'the members of an interface belong to the sibling rule',
		},

		{
			code: lines('type A = {', ...spaced('  a: string;', '  b: string;', '  c: string;', '  d: string;'), '};'),
			name: 'the members of a type literal belong to the sibling rule',
		},
	],

	invalid: [
		{
			code: lines(...spaced('a();', 'b();', 'c();', 'd();')),
			output: lines('a();', 'b();', 'c();', 'd();'),
			errors: 3,
		},

		{
			code: lines(...spaced('a();', 'b();', 'c();', 'd();', 'e();')),
			output: lines('a();', 'b();', 'c();', 'd();', 'e();'),
			errors: 4,
		},

		{
			code: lines('a();', '', '', 'b();', '', 'c();', '', 'd();'),
			output: lines('a();', 'b();', 'c();', 'd();'),
			errors: 3,
			name: 'every blank line of a gap goes',
		},

		{
			code: crlf(lines(...spaced('a();', 'b();', 'c();', 'd();'))),
			output: crlf(lines('a();', 'b();', 'c();', 'd();')),
			errors: 3,
		},

		{
			code: lines('a();', '', 'b();', '', 'c();', '', 'd();', '', 'e();'),
			output: lines('a();', 'b();', 'c();', 'd();', 'e();'),
			settings: limit(2),
			errors: 4,
		},

		{
			code: lines('a();', 'b();', '', 'c();', '', 'd();', '', 'e();', '', 'f();'),
			output: lines('a();', 'b();', '', 'c();', 'd();', 'e();', 'f();'),
			errors: 3,
			name: 'the unit that is tight above the run stays out of it',
		},

		{
			code: lines('function f() {', ...spaced('  a();', '  b();', '  c();', '  d();'), '}'),
			output: lines('function f() {', '  a();', '  b();', '  c();', '  d();', '}'),
			errors: 3,
		},

		{
			code: lines('switch (x) {', '  case 1:', ...spaced('    a();', '    b();', '    c();', '    d();'), '}'),
			output: lines('switch (x) {', '  case 1:', '    a();', '    b();', '    c();', '    d();', '}'),
			errors: 3,
		},

		{
			code: lines('class A {', '  static {', ...spaced('    a();', '    b();', '    c();', '    d();'), '  }', '}'),
			output: lines('class A {', '  static {', '    a();', '    b();', '    c();', '    d();', '  }', '}'),
			errors: 3,
			name: 'the statements of a static block are judged',
		},

		{
			code: lines('a();', '', 'b();', '', 'c();', '', 'd(); // note', '', 'e();'),
			output: lines('a();', 'b();', 'c();', 'd(); // note', '', 'e();'),
			errors: 3,
			name: 'a trailing comment ends the run',
		},
	],
});

tester.run('gather-loose-members', gatherLooseMembers as never, {
	valid: [
		{ code: lines('class A {', ...spaced('  a = 1;', '  b = 2;', '  c = 3;'), '}'), name: 'a run at the limit stays' },
		{ code: lines('class A {', '  a = 1;', '  b = 2;', '  c = 3;', '  d = 4;', '}'), name: 'a run with no blank line has nothing to gather' },

		{
			code: lines('class A {', ...spaced('  a = 1;', '  b = 2;', '  c = 3;', '  d = 4;'), '}'),
			settings: limit(0),
			name: 'a limit of 0 turns the rule off',
		},

		{ code: lines('class A {', ...spaced('  a = 1;', '  b = 2;', '  c = 3;', '  d = 4;'), '}'), settings: limit(4) },

		{
			code: lines('class A {', '  a = 1;', '', '  b = 2;', '', '  // note', '  c = 3;', '', '  d = 4;', '', '  e = 5;', '}'),
			name: 'a comment ends a run',
		},

		{
			code: lines('class A {', '  a = 1;', '', '  b = 2;', '', '  c() {', '    go();', '  }', '', '  d = 4;', '', '  e = 5;', '}'),
			name: 'a multi-line member ends a run',
		},

		{
			code: lines('class A {', '  a = 1;', '  b = 2;', '', '  c = 3;', '', '  d = 4;', '', '  e = 5;', '}'),
			name: 'a member tight against its other neighbor stays out of the run',
		},

		{ code: lines(...spaced('a();', 'b();', 'c();', 'd();')), name: 'the statements of a file belong to the sibling rule' },

		{
			code: lines('function f() {', ...spaced('  a();', '  b();', '  c();', '  d();'), '}'),
			name: 'the statements of a function belong to the sibling rule',
		},

		{
			code: lines('class A {', '  static {', ...spaced('    a();', '    b();', '    c();', '    d();'), '  }', '}'),
			name: 'the statements of a static block belong to the sibling rule',
		},
	],

	invalid: [
		{
			code: lines('class A {', ...spaced('  a = 1;', '  b = 2;', '  c = 3;', '  d = 4;'), '}'),
			output: lines('class A {', '  a = 1;', '  b = 2;', '  c = 3;', '  d = 4;', '}'),
			errors: 3,
		},

		{
			code: lines('interface A {', ...spaced('  a: string;', '  b: string;', '  c: string;', '  d: string;'), '}'),
			output: lines('interface A {', '  a: string;', '  b: string;', '  c: string;', '  d: string;', '}'),
			errors: 3,
		},

		{
			code: lines('type A = {', ...spaced('  a: string;', '  b: string;', '  c: string;', '  d: string;'), '};'),
			output: lines('type A = {', '  a: string;', '  b: string;', '  c: string;', '  d: string;', '};'),
			errors: 3,
			name: 'the members of a type literal are judged',
		},

		{
			code: crlf(lines('class A {', ...spaced('  a = 1;', '  b = 2;', '  c = 3;', '  d = 4;'), '}')),
			output: crlf(lines('class A {', '  a = 1;', '  b = 2;', '  c = 3;', '  d = 4;', '}')),
			errors: 3,
		},

		{
			code: lines('class A {', ...spaced('  a = 1;', '  b = 2;', '  c = 3;'), '}'),
			output: lines('class A {', '  a = 1;', '  b = 2;', '  c = 3;', '}'),
			settings: limit(2),
			errors: 2,
		},

		{
			code: lines('class A {', '  a = 1;', '  b = 2;', '', '  c = 3;', '', '  d = 4;', '', '  e = 5;', '', '  f = 6;', '}'),
			output: lines('class A {', '  a = 1;', '  b = 2;', '', '  c = 3;', '  d = 4;', '  e = 5;', '  f = 6;', '}'),
			errors: 3,
			name: 'the member that is tight above the run stays out of it',
		},

		{
			code: lines('class A {', '  a = 1;', '', '  b = 2;', '', '  c = 3;', '', '  d = 4; // note', '', '  e = 5;', '}'),
			output: lines('class A {', '  a = 1;', '  b = 2;', '  c = 3;', '  d = 4; // note', '', '  e = 5;', '}'),
			errors: 3,
			name: 'a trailing comment ends the run',
		},
	],
});

describe('the fix of a gathered run', () => {
	const gather = { 'gather-loose-statements': 'error' } as const;

	it('stands on each report of the run, so applying any one of them gathers the run', () => {
		const source = lines(...spaced('a();', 'b();', 'c();', 'd();'));
		const config = createConfig(gather);
		const reports = new Linter().verify(source, config, { filename: 'file.ts' });

		assert.equal(reports.length, 3);
		assertGroupFix(source, reports, lines('a();', 'b();', 'c();', 'd();'));

		for (const report of reports) {
			const fixed = applyFix(source, report.fix as never);
			assert.deepEqual(new Linter().verify(fixed, config, { filename: 'file.ts' }), []);
		}
	});

	it('stands on the reports that no directive silences, so the run still gathers in one fix', () => {
		const source = lines(...spaced('a();', 'b(/* eslint-disable-line stanza/gather-loose-statements */);', 'c();', 'd();'));
		const linter = new Linter();
		const reports = linter.verify(source, createConfig(gather), { filename: 'file.ts' });

		assert.equal(readSuppressedMessages(linter).length, 1);
		assert.equal(reports.length, 2);

		assertGroupFix(source, reports, lines('a();', 'b(/* eslint-disable-line stanza/gather-loose-statements */);', 'c();', 'd();'));
		assert.equal(stable(source, gather), lines('a();', 'b(/* eslint-disable-line stanza/gather-loose-statements */);', 'c();', 'd();'));
	});

	it('stands on the report that opens a long run only, so the cost of a run stays linear in its length', () => {
		const size = everyReportGroupSize + 2;
		const source = lines(...spaced(...Array.from({ length: size }, (_, index) => `call${index}();`)));
		const reports = new Linter().verify(source, createConfig(gather), { filename: 'file.ts' });

		assert.equal(reports.length, size - 1);
		assert.equal(reports.filter((report) => report.fix !== undefined).length, 1);
		assert.ok(reports[0]?.fix);
		assert.equal(stable(source, gather), lines(...Array.from({ length: size }, (_, index) => `call${index}();`)));
	});

	it('stands on every report of a run of the size where each still carries it', () => {
		const source = lines(...spaced(...Array.from({ length: everyReportGroupSize + 1 }, (_, index) => `call${index}();`)));
		const reports = new Linter().verify(source, createConfig(gather), { filename: 'file.ts' });

		assert.equal(reports.length, everyReportGroupSize);
		assert.ok(reports.every((report) => report.fix !== undefined));
	});
});

describe('a disable directive in the gap of a run', () => {
	const gather = { 'gather-loose-statements': 'error' } as const;

	/** Builds a config that reports an unused directive and removes it, as ESLint does by default, with `no-console` and `no-eval` on for a directive that is used. */
	function createDefaultConfig(rules: Record<string, Linter.RuleEntry>): Linter.Config[] {
		return [
			...createConfig(rules).map((entry) => ({ ...entry, linterOptions: {} })),
			{ files: ['**/*.ts'], rules: { 'no-console': 'error', 'no-eval': 'error' } },
		];
	}

	/** Fixes a source under the default options, checking that one pass of fixes leaves nothing to report. */
	function settleUnderDefaults(source: string, rules: Record<string, Linter.RuleEntry>): string {
		const linter = new Linter();
		const options = { filename: 'file.ts', stats: true };
		const result = linter.verifyAndFix(source, createDefaultConfig(rules), options);

		assert.deepEqual(result.messages, []);
		assert.ok(linter.getFixPassCount() <= 1, `needed ${linter.getFixPassCount()} fix passes`);

		return result.output;
	}

	it('leaves the same text whether or not ESLint removes it as unused, where it names no report of the rule', () => {
		const source = lines('a();', '', 'b();', '', '// eslint-disable-next-line stanza/gather-loose-statements', 'c();', '', 'd();');
		const kept = new Linter().verifyAndFix(source, createConfig(gather), { filename: 'file.ts' }).output;
		const removed = new Linter().verifyAndFix(source, createDefaultConfig(gather), { filename: 'file.ts' }).output;

		assert.equal(kept, lines('a();', 'b();', '// eslint-disable-next-line stanza/gather-loose-statements', 'c();', 'd();'));

		// ESLint leaves the space the comment stood in.
		assert.equal(removed, lines('a();', 'b();', ' ', 'c();', 'd();'));
	});

	it('does not end the run where another rule uses it', () => {
		const source = lines('a();', '', 'b();', '', '// eslint-disable-next-line no-console', 'console.log(1);', '', 'd();');
		assert.equal(settleUnderDefaults(source, gather), lines('a();', 'b();', '// eslint-disable-next-line no-console', 'console.log(1);', 'd();'));
	});

	it('does not end the run where it trails the statement above the gap', () => {
		const source = lines(...spaced('a();', 'console.log(1); // eslint-disable-line no-console', 'c();', 'd();'));
		assert.equal(settleUnderDefaults(source, gather), lines('a();', 'console.log(1); // eslint-disable-line no-console', 'c();', 'd();'));
	});

	it('does not end the run of a keep rule either', () => {
		const rules = { 'keep-statement-runs-together': 'error', 'separate-declaration-runs': 'error' } as const;
		const source = lines('const a = 1;', '', '// eslint-disable-next-line no-eval', 'const b = eval(a);', '', 'const c = 3;', '', 'foo(a, b, c);');

		assert.equal(
			settleUnderDefaults(source, rules),
			lines('const a = 1;', '// eslint-disable-next-line no-eval', 'const b = eval(a);', 'const c = 3;', '', 'foo(a, b, c);'),
		);
	});

	it('does not end the run of the members of a type either', () => {
		const source = lines('class A {', '  a = 1;', '', '  b = 2;', '', '  // eslint-disable-next-line no-eval', '  c = eval(1);', '', '  d = 4;', '}');

		assert.equal(
			settleUnderDefaults(source, { 'gather-loose-members': 'error' }),
			lines('class A {', '  a = 1;', '  b = 2;', '  // eslint-disable-next-line no-eval', '  c = eval(1);', '  d = 4;', '}'),
		);
	});

	it('ends the run where a comment stands beside it, as a comment does', () => {
		const source = lines('a();', '', 'b();', '', '// note', '// eslint-disable-next-line no-console', 'console.log(1);', '', 'd();');
		assert.equal(settleUnderDefaults(source, gather), source);
	});
});
