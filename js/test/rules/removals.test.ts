import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { Linter } from 'eslint';

import gatherLooseMembers from '../../src/rules/gather-loose-members.ts';
import gatherLooseStatements from '../../src/rules/gather-loose-statements.ts';
import { assertGroupFix, createConfig, createRuleTester, crlf, lines, readSuppressedMessages, stable } from '../helpers.ts';

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

	it('stands on the first report of the run, and the others carry none', () => {
		const source = lines(...spaced('a();', 'b();', 'c();', 'd();'));
		const reports = new Linter().verify(source, createConfig(gather), { filename: 'file.ts' });

		assert.equal(reports.length, 3);
		assertGroupFix(source, reports, lines('a();', 'b();', 'c();', 'd();'));
	});

	it('moves to the next report where a directive silences the first, so the run still gathers in one fix', () => {
		const source = lines(...spaced('a();', 'b(/* eslint-disable-line stanza/gather-loose-statements */);', 'c();', 'd();'));
		const linter = new Linter();
		const reports = linter.verify(source, createConfig(gather), { filename: 'file.ts' });

		assert.deepEqual(
			readSuppressedMessages(linter).map((message) => message.fix),
			[undefined],
		);

		assertGroupFix(source, reports, lines('a();', 'b(/* eslint-disable-line stanza/gather-loose-statements */);', 'c();', 'd();'));
		assert.equal(stable(source, gather), lines('a();', 'b(/* eslint-disable-line stanza/gather-loose-statements */);', 'c();', 'd();'));
	});
});
