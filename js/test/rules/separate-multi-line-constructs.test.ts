import rule from '../../src/rules/separate-multi-line-constructs.ts';
import { createRuleTester, crlf, lines } from '../helpers.ts';

createRuleTester().run('separate-multi-line-constructs', rule as never, {
	valid: [
		{ code: lines('const a = 1;', 'foo(a);', 'bar();') },
		{ code: lines('const a = 1;', '', 'if (a) {', '  foo();', '}', '', 'bar();') },
		{ code: lines('a();', '', '// note', 'if (a) {', '  foo();', '}') },
		{ code: lines('a(); b();', 'c();') },
		{ code: lines('if (a) {', '  foo();', '} bar();') },
		{ code: lines('import {', '  a,', '  b,', "} from 'x';", "import { c } from 'y';") },
		{ code: lines('switch (x) {', '  case 1:', '  case 2:', '    foo();', '    break;', '}') },
		{ code: lines('switch (x) {', '  case 1:', '    foo();', '    break;', '', '  default:', '    bar();', '}') },

		{
			code: lines('switch (x) {', '  case 1:', '    foo();', '    break;', '  default:', '    bar();', '}'),
			name: 'the cases of a switch belong to separate-multi-line-switch-cases',
		},

		{ code: lines('declare function f(a: string): void;', 'declare function f(', '  a: number,', '): void;') },

		{
			code: lines(
				'export function f(a: string): void;',
				'export function f(',
				'  a: number,',
				'): void;',
				'export function f(a: unknown): void {',
				'  g(a);',
				'}',
			),
		},

		{ code: lines('class A {', '  get value() {', '    return 1;', '  }', '  set value(next: number) {', '    log(next);', '  }', '}') },

		{
			code: lines(
				'class A {',
				'  method(a: string): void;',
				'  method(',
				'    a: number,',
				'  ): void;',
				'  method(a: unknown) {',
				'    g(a);',
				'  }',
				'}',
			),
		},

		{ code: lines('interface A {', '  call(a: string): void;', '  call(', '    a: number,', '  ): void;', '}') },
		{ code: lines('const a = (', '  <div>', '    <A />', '    <B>', '      x', '    </B>', '  </div>', ');') },
	],

	invalid: [
		{
			code: lines('const a = 1;', 'if (a) {', '  foo();', '}', 'bar();'),
			output: lines('const a = 1;', '', 'if (a) {', '  foo();', '}', '', 'bar();'),
			errors: 2,
		},

		{
			code: lines('foo();', 'const text = `a', 'b`;', 'bar();'),
			output: lines('foo();', '', 'const text = `a', 'b`;', '', 'bar();'),
			errors: 2,
		},

		{
			code: lines('foo();', '// @ts-expect-error', 'bar(', '  1,', ');'),
			output: lines('foo();', '', '// @ts-expect-error', 'bar(', '  1,', ');'),
			errors: 1,
		},

		{
			code: lines('foo();', '/** Does the work. */', 'function work() {', '  bar();', '}'),
			output: lines('foo();', '', '/** Does the work. */', 'function work() {', '  bar();', '}'),
			errors: 1,
		},

		{
			code: lines('foo(); // note', 'if (a) {', '  bar();', '}'),
			output: lines('foo(); // note', '', 'if (a) {', '  bar();', '}'),
			errors: 1,
		},

		{
			code: lines('foo();', '// note', '', 'if (a) {', '  bar();', '}', '// eslint-disable-next-line no-console', 'console.log(1);'),
			output: lines('foo();', '// note', '', 'if (a) {', '  bar();', '}', '', '// eslint-disable-next-line no-console', 'console.log(1);'),
			errors: 1,
		},

		{
			code: crlf(lines('foo();', 'if (a) {', '  bar();', '}')),
			output: crlf(lines('foo();', '', 'if (a) {', '  bar();', '}')),
			errors: 1,
		},

		{
			code: lines('class A {', '  a = 1;', '  b() {', '    return 1;', '  }', '  c = 2;', '}'),
			output: lines('class A {', '  a = 1;', '', '  b() {', '    return 1;', '  }', '', '  c = 2;', '}'),
			errors: 2,
		},

		{
			code: lines('class A {', '  @Input()', '  name: string;', '  size = 1;', '}'),
			output: lines('class A {', '  @Input()', '  name: string;', '', '  size = 1;', '}'),
			errors: 1,
		},

		{
			code: lines('class A {', '  a = 1;', '  static {', '    init();', '    boot();', '  }', '}'),
			output: lines('class A {', '  a = 1;', '', '  static {', '    init();', '    boot();', '  }', '}'),
			errors: 1,
		},

		{
			code: lines('interface A {', '  a: string;', '  b(', '    x: number,', '  ): void;', '  c: number;', '}'),
			output: lines('interface A {', '  a: string;', '', '  b(', '    x: number,', '  ): void;', '', '  c: number;', '}'),
			errors: 2,
		},

		{
			code: lines('declare module "x" {', '  const a: number;', '  function f(', '    a: string,', '  ): void;', '}'),
			output: lines('declare module "x" {', '  const a: number;', '', '  function f(', '    a: string,', '  ): void;', '}'),
			errors: 1,
		},

		{
			code: lines('switch (x) {', '  case 1:', '    foo();', '    bar(', '      1,', '    );', '    break;', '}'),
			output: lines('switch (x) {', '  case 1:', '    foo();', '', '    bar(', '      1,', '    );', '', '    break;', '}'),
			errors: 2,
		},
	],
});
