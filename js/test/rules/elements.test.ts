import keepSingleLine from '../../src/rules/keep-single-line-elements-together.ts';
import separateMultiLine from '../../src/rules/separate-multi-line-elements.ts';
import { createRuleTester, crlf, lines } from '../helpers.ts';

const tester = createRuleTester();

const only = (option: string): Record<string, boolean> => ({
	objectMembers: false,
	arrayElements: false,
	enumMembers: false,
	callArguments: false,
	parameters: false,
	[option]: true,
});

tester.run('separate-multi-line-elements', separateMultiLine as never, {
	valid: [
		{ code: lines('const o = {', '  a: 1,', '  b: 2,', '};'), name: 'two single-line members stay tight' },
		{ code: lines('const o = {', '  a: {', '    b: 1,', '  },', '', '  c: 2,', '};') },
		{ code: lines('const o = { a: {', '  b: 1,', '}, c: 2 };'), name: 'elements that share a line are joined' },
		{ code: lines('run(a, () => {', '  b();', '});'), name: 'a callback that hugs the call' },
		{ code: lines('run(() => {', '  b();', '}, 100);'), name: 'a second argument on the closing line' },

		{
			code: lines('const o = {', '  a: 1,', '  b: {', '    c: 1,', '  },', '};'),
			options: [{ objectMembers: false }],
			name: 'an option turns a list off',
		},

		{ code: lines('const [a, b] = [', '  1,', '  2,', '];') },
		{ code: lines('foo(', '  a,', '  b,', ');') },
		{ code: lines('class A {', '  a = {', '    b: 1,', '  };', '}'), name: 'a single member is no pair' },
		{ code: lines('const o = {', '  a: 1,', '  [key]: 2,', '  ...rest,', '};') },
	],

	invalid: [
		{
			code: lines('const o = {', '  name: "a",', '  tags: {', '    x: 1,', '  },', '  count: 3,', '};'),
			output: lines('const o = {', '  name: "a",', '', '  tags: {', '    x: 1,', '  },', '', '  count: 3,', '};'),
			errors: 2,
		},

		{
			code: lines('const handlers = [', '  new ConfigurationHandler(', '    kind,', '  ),', '  new ValidationHandler(', '    kind,', '  ),', '];'),

			output: lines(
				'const handlers = [',
				'  new ConfigurationHandler(',
				'    kind,',
				'  ),',
				'',
				'  new ValidationHandler(',
				'    kind,',
				'  ),',
				'];',
			),

			errors: 1,
		},

		{
			code: lines('create(', '  "Application",', '  true,', '  (key, value) => {', '    warn(key, value);', '  },', ');'),
			output: lines('create(', '  "Application",', '  true,', '', '  (key, value) => {', '    warn(key, value);', '  },', ');'),
			errors: 1,
		},

		{
			code: lines('const a = new Thing(', '  one,', '  {', '    two: 2,', '  },', '  three,', ');'),
			output: lines('const a = new Thing(', '  one,', '', '  {', '    two: 2,', '  },', '', '  three,', ');'),
			errors: 2,
		},

		{
			code: lines('function make(', '  name: string,', '  options: {', '    deep: boolean;', '  },', '  last: number,', ') {}'),
			output: lines('function make(', '  name: string,', '', '  options: {', '    deep: boolean;', '  },', '', '  last: number,', ') {}'),
			errors: 2,
		},

		{
			code: lines('class A {', '  constructor(', '    private a: string,', '    private b: {', '      c: number;', '    },', '  ) {}', '}'),
			output: lines('class A {', '  constructor(', '    private a: string,', '', '    private b: {', '      c: number;', '    },', '  ) {}', '}'),
			errors: 1,
		},

		{
			code: lines('enum E {', '  A = 1,', '  B = f(', '    2,', '  ),', '  C = 3,', '}'),
			output: lines('enum E {', '  A = 1,', '', '  B = f(', '    2,', '  ),', '', '  C = 3,', '}'),
			errors: 2,
		},

		{
			code: lines('type F = (', '  a: string,', '  b: {', '    c: number;', '  },', ') => void;'),
			output: lines('type F = (', '  a: string,', '', '  b: {', '    c: number;', '  },', ') => void;'),
			errors: 1,
		},

		{
			code: lines('const o = {', '  a: 1,', '  // the nested part', '  b: {', '    c: 1,', '  },', '};'),
			output: lines('const o = {', '  a: 1,', '', '  // the nested part', '  b: {', '    c: 1,', '  },', '};'),
			errors: 1,
			name: 'a comment directly above the element moves with it',
		},

		{
			code: lines('const o = {', '  a: 1, // first', '  b: {', '    c: 1,', '  },', '};'),
			output: lines('const o = {', '  a: 1, // first', '', '  b: {', '    c: 1,', '  },', '};'),
			errors: 1,
			name: 'a comment that trails the element above stays with it',
		},

		{
			code: lines('const o = {', '  a: 1,', '  get b() {', '    return 1;', '  },', '  async c() {', '    run();', '  },', '};'),
			output: lines('const o = {', '  a: 1,', '', '  get b() {', '    return 1;', '  },', '', '  async c() {', '    run();', '  },', '};'),
			errors: 2,
			name: 'methods of an object',
		},

		{
			code: lines('const o = {', '  a: 1,', '  b: {', '    c: 1,', '  },', '};', 'const p = [', '  1,', '  [', '    2,', '  ],', '];'),
			output: lines('const o = {', '  a: 1,', '', '  b: {', '    c: 1,', '  },', '};', 'const p = [', '  1,', '', '  [', '    2,', '  ],', '];'),
			errors: 2,
		},

		{
			code: lines('const o = {', '  a: 1,', '  b: {', '    c: 1,', '  },', '};'),
			output: lines('const o = {', '  a: 1,', '', '  b: {', '    c: 1,', '  },', '};'),
			options: [only('objectMembers')],
			errors: 1,
			name: 'an option turns the lists of one kind on',
		},

		{
			code: crlf(lines('const o = {', '  a: 1,', '  b: {', '    c: 1,', '  },', '};')),
			output: crlf(lines('const o = {', '  a: 1,', '', '  b: {', '    c: 1,', '  },', '};')),
			errors: 1,
		},

		{
			code: lines('run(', '  [', '    1,', '  ],', '  [', '    2,', '  ],', ');'),
			output: lines('run(', '  [', '    1,', '  ],', '', '  [', '    2,', '  ],', ');'),
			errors: 1,
			name: 'the arguments and the array inside them are two lists',
		},
	],
});

tester.run('keep-single-line-elements-together', keepSingleLine as never, {
	valid: [
		{ code: lines('const o = {', '  a: 1,', '  b: 2,', '};') },
		{ code: lines('const o = {', '  a: 1,', '', '  b: {', '    c: 1,', '  },', '};'), name: 'a multi-line element keeps the blank line' },
		{ code: lines('const o = {', '  a: 1,', '', '  // the size', '  b: 2,', '};'), name: 'a comment in the gap keeps the blank line' },
		{ code: lines('const o = {', '  a: 1, // first', '', '  b: 2,', '};'), name: 'a trailing comment keeps the blank line' },
		{ code: lines('run(', '  a', '  ,', '', '  b', ');'), name: 'a comma on a line of its own keeps the gap' },

		{
			code: lines('const o = {', '  a: 1,', '', '  b: 2,', '};'),
			options: [{ objectMembers: false }],
			name: 'the objectMembers option turns off the blank lines of an object',
		},

		{
			code: lines('const a = [', '  1,', '', '  2,', '];'),
			options: [{ arrayElements: false }],
			name: 'the arrayElements option turns off the blank lines of an array',
		},

		{
			code: lines('enum E {', '  A = 1,', '', '  B = 2,', '}'),
			options: [{ enumMembers: false }],
			name: 'the enumMembers option turns off the blank lines of an enum',
		},

		{
			code: lines('create(', '  "Application",', '', '  true,', ');'),
			options: [{ callArguments: false }],
			name: 'the callArguments option turns off the blank lines of a call',
		},

		{
			code: lines('function make(', '  name: string,', '', '  count: number,', ') {}'),
			options: [{ parameters: false }],
			name: 'the parameters option turns off the blank lines of a parameter list',
		},
	],

	invalid: [
		{
			code: lines('const o = {', '  a: 1,', '', '  b: 2,', '};'),
			output: lines('const o = {', '  a: 1,', '  b: 2,', '};'),
			errors: 1,
		},

		{
			code: lines('const o = {', '  a: 1,', '', '', '  b: 2,', '  c: 3,', '', '  d: 4,', '};'),
			output: lines('const o = {', '  a: 1,', '  b: 2,', '  c: 3,', '  d: 4,', '};'),
			errors: 2,
		},

		{
			code: lines('const a = [', '  "first",', '', '  "second",', '];'),
			output: lines('const a = [', '  "first",', '  "second",', '];'),
			errors: 1,
		},

		{
			code: lines('create(', '  "Application",', '', '  true,', ');'),
			output: lines('create(', '  "Application",', '  true,', ');'),
			errors: 1,
		},

		{
			code: lines('const x = new Thing(', '  one,', '', '  two,', ');'),
			output: lines('const x = new Thing(', '  one,', '  two,', ');'),
			errors: 1,
		},

		{
			code: lines('function make(', '  name: string,', '', '  count: number,', ') {}'),
			output: lines('function make(', '  name: string,', '  count: number,', ') {}'),
			errors: 1,
		},

		{
			code: lines('enum E {', '  A = 1,', '', '  B = 2,', '}'),
			output: lines('enum E {', '  A = 1,', '  B = 2,', '}'),
			errors: 1,
		},

		{
			code: crlf(lines('const o = {', '  a: 1,', '', '  b: 2,', '};')),
			output: crlf(lines('const o = {', '  a: 1,', '  b: 2,', '};')),
			errors: 1,
		},

		{
			code: lines('const o = {', '  a: 1,', '  ', '  b: 2,', '};'),
			output: lines('const o = {', '  a: 1,', '  b: 2,', '};'),
			errors: 1,
			name: 'a line of whitespace is a blank line',
		},

		{
			code: lines('const o = {', '  a: 1,', '', '  b: 2,', '};'),
			output: lines('const o = {', '  a: 1,', '  b: 2,', '};'),
			options: [only('objectMembers')],
			errors: 1,
			name: 'only objectMembers left on judges an object',
		},

		{
			code: lines('const a = [', '  1,', '', '  2,', '];'),
			output: lines('const a = [', '  1,', '  2,', '];'),
			options: [only('arrayElements')],
			errors: 1,
			name: 'only arrayElements left on judges an array',
		},

		{
			code: lines('enum E {', '  A = 1,', '', '  B = 2,', '}'),
			output: lines('enum E {', '  A = 1,', '  B = 2,', '}'),
			options: [only('enumMembers')],
			errors: 1,
			name: 'only enumMembers left on judges an enum',
		},

		{
			code: lines('create(', '  "Application",', '', '  true,', ');'),
			output: lines('create(', '  "Application",', '  true,', ');'),
			options: [only('callArguments')],
			errors: 1,
			name: 'only callArguments left on judges a call',
		},

		{
			code: lines('function make(', '  name: string,', '', '  count: number,', ') {}'),
			output: lines('function make(', '  name: string,', '  count: number,', ') {}'),
			options: [only('parameters')],
			errors: 1,
			name: 'only parameters left on judges a parameter list',
		},
	],
});
