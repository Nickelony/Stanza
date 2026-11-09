import baseCall from '../../src/rules/separate-base-call.ts';
import modifierGroups from '../../src/rules/separate-modifier-groups.ts';
import { createRuleTester, lines } from '../helpers.ts';

const tester = createRuleTester();

/** Wraps the statements in the body of a method of a derived class. */
function method(...body: string[]): string {
	return lines('class A extends B {', '  run() {', ...body.map((line) => (line === '' ? line : `    ${line}`)), '  }', '}');
}

tester.run('separate-base-call', baseCall as never, {
	valid: [
		{ code: lines('class A extends B {', '  constructor(a) {', '    super(a);', '', '    this.a = a;', '    this.b = a;', '  }', '}') },

		{
			code: lines('class A extends B {', '  constructor(a) {', '    super(a);', '    this.a = a;', '  }', '}'),
			name: 'a body of two statements stays tight',
		},

		{
			code: lines(
				'class A extends B {',
				'  constructor(a) {',
				'    const x = f(a);',
				'    super(x);',
				'    this.x = x;',
				'    this.y = x;',
				'  }',
				'}',
			),

			name: 'a call that does not open the body',
		},

		{ code: method('if (this.ok) {', '  super.run();', '  this.a();', '  this.b();', '}'), name: 'a nested block' },
		{ code: method('return () => {', '  super.run();', '  this.a();', '  this.b();', '};'), name: 'an arrow function' },
		{ code: method('this.a();', 'this.b();', 'return super.run();'), name: 'a returned call is a jump' },
		{ code: method('super.value = 1;', 'this.a();', 'this.b();'), name: 'an assignment is no call' },
		{ code: method('use(super.value);', 'this.a();', 'this.b();'), name: 'a call that passes super.value' },
		{ code: method('this.a();', 'super.run();', 'this.b();'), name: 'a call in the middle' },
		{ code: method('this.a();', 'this.b();', '', 'super.run();') },
		{ code: lines('function f() {', '  super_();', '  a();', '  b();', '}') },
	],

	invalid: [
		{
			code: lines('class A extends B {', '  constructor(a) {', '    super(a);', '    this.a = a;', '    this.init();', '  }', '}'),
			output: lines('class A extends B {', '  constructor(a) {', '    super(a);', '', '    this.a = a;', '    this.init();', '  }', '}'),
			errors: 1,
		},

		{
			code: method('this.timer.dispose();', 'this.stream.dispose();', 'super.dispose();'),
			output: method('this.timer.dispose();', 'this.stream.dispose();', '', 'super.dispose();'),
			errors: 1,
		},

		{
			code: method('super.connect();', 'this.render();', 'super.finish();'),
			output: method('super.connect();', '', 'this.render();', '', 'super.finish();'),
			errors: 2,
			name: 'a body that opens and closes with a base call gets both blank lines',
		},

		{
			code: method('super.a();', 'super.b();', 'this.c();'),
			output: method('super.a();', '', 'super.b();', 'this.c();'),
			errors: 1,
			name: 'a second base call below the opening one is no opening call',
		},

		{
			code: method('await super.load();', 'this.a();', 'this.b();'),
			output: method('await super.load();', '', 'this.a();', 'this.b();'),
			errors: 1,
		},

		{
			code: method('super.items.clear();', 'this.a();', 'this.b();'),
			output: method('super.items.clear();', '', 'this.a();', 'this.b();'),
			errors: 1,
		},

		{
			code: method('if (!this.ok) return;', '', 'super.run();', 'this.a();', 'this.b();'),
			output: method('if (!this.ok) return;', '', 'super.run();', '', 'this.a();', 'this.b();'),
			errors: 1,
			name: 'the opening call is the first statement below the guards',
		},

		{
			code: method('invariant(this.ok);', '', 'super.run();', 'this.a();', 'this.b();'),
			output: method('invariant(this.ok);', '', 'super.run();', '', 'this.a();', 'this.b();'),
			errors: 1,
		},

		{
			code: method('if (!this.ok) return;', 'this.a();', 'super.run();'),
			output: method('if (!this.ok) return;', 'this.a();', '', 'super.run();'),
			errors: 1,
		},

		{
			code: lines('class A extends B {', '  set value(v) {', '    this.a(v);', '    this.b(v);', '    super.touch();', '  }', '}'),
			output: lines('class A extends B {', '  set value(v) {', '    this.a(v);', '    this.b(v);', '', '    super.touch();', '  }', '}'),
			errors: 1,
			name: 'a setter',
		},

		{
			code: lines('const o = {', '  run() {', '    super.run();', '    a();', '    b();', '  },', '};'),
			output: lines('const o = {', '  run() {', '    super.run();', '', '    a();', '    b();', '  },', '};'),
			errors: 1,
			name: 'an object method',
		},

		{
			code: method('super.run();', '// keep the fields', 'this.a = 1;', 'this.b = 2;'),
			output: method('super.run();', '', '// keep the fields', 'this.a = 1;', 'this.b = 2;'),
			errors: 1,
			name: 'a comment directly above the next statement moves with it',
		},
	],
});

tester.run('separate-modifier-groups', modifierGroups as never, {
	valid: [
		{ code: lines('class A {', '  a = 1;', '  static b = 2;', '  readonly c = 3;', '}'), name: 'lone members of three groups stay tight' },
		{ code: lines('class A {', '  public a = 1;', '  b = 2;', '  public c = 3;', '}'), name: 'no access is public' },
		{ code: lines('class A {', '  private a = 1;', '  #b = 2;', '', '  c = 3;', '}'), name: 'a hash name is private' },
		{ code: lines('class A {', '  async a() {}', '  b() {}', '  override c() {}', '  d() {}', '}'), name: 'other modifiers do not end a run' },
		{ code: lines('class A {', '  declare a: number;', '  b: number;', '  abstract c: number;', '  d: number;', '}') },

		{
			code: lines('class A {', '  get a() {', '    return 1;', '  }', '  set a(v) {}', '', '  b = 1;', '}'),
			name: 'a getter and its setter stay together',
		},

		{ code: lines('class A {', '  static a = 1;', '  static b = 2;', '', '  c = 1;', '  d = 2;', '}') },
		{ code: lines('interface A {', '  readonly a: string;', '  readonly b: string;', '', '  c: string;', '}') },
	],

	invalid: [
		{
			code: lines('class A {', '  public a = 1;', '  public b = 2;', '  private c = 3;', '  private d = 4;', '}'),
			output: lines('class A {', '  public a = 1;', '  public b = 2;', '', '  private c = 3;', '  private d = 4;', '}'),
			errors: 1,
		},

		{
			code: lines('class A {', '  a = 1;', '  b = 2;', '  protected c = 3;', '  protected d = 4;', '  private e = 5;', '  private f = 6;', '}'),

			output: lines(
				'class A {',
				'  a = 1;',
				'  b = 2;',
				'',
				'  protected c = 3;',
				'  protected d = 4;',
				'',
				'  private e = 5;',
				'  private f = 6;',
				'}',
			),

			errors: 2,
		},

		{
			code: lines('class A {', '  static a = 1;', '  static b = 2;', '  c = 3;', '  d = 4;', '}'),
			output: lines('class A {', '  static a = 1;', '  static b = 2;', '', '  c = 3;', '  d = 4;', '}'),
			errors: 1,
		},

		{
			code: lines(
				'class A {',
				'  readonly a = 1;',
				'  readonly b = 2;',
				'  c = 3;',
				'  d = 4;',
				'  static readonly e = 5;',
				'  static readonly f = 6;',
				'}',
			),

			output: lines(
				'class A {',
				'  readonly a = 1;',
				'  readonly b = 2;',
				'',
				'  c = 3;',
				'  d = 4;',
				'',
				'  static readonly e = 5;',
				'  static readonly f = 6;',
				'}',
			),

			errors: 2,
		},

		{
			code: lines('class A {', '  private a = 1;', '  #b = 2;', '  c = 3;', '  d = 4;', '}'),
			output: lines('class A {', '  private a = 1;', '  #b = 2;', '', '  c = 3;', '  d = 4;', '}'),
			errors: 1,
		},

		{
			code: lines('class A {', '  static a() {}', '  static b() {}', '  c() {}', '  d() {}', '}'),
			output: lines('class A {', '  static a() {}', '  static b() {}', '', '  c() {}', '  d() {}', '}'),
			errors: 1,
		},

		{
			code: lines('class A {', '  a = 1;', '  b = 2;', '  c() {}', '  d() {}', '}'),
			output: lines('class A {', '  a = 1;', '  b = 2;', '', '  c() {}', '  d() {}', '}'),
			errors: 1,
			name: 'a kind boundary is a modifier boundary too',
		},

		{
			code: lines('interface A {', '  readonly a: string;', '  readonly b: string;', '  c: string;', '  d: string;', '}'),
			output: lines('interface A {', '  readonly a: string;', '  readonly b: string;', '', '  c: string;', '  d: string;', '}'),
			errors: 1,
		},

		{
			code: lines('type A = {', '  readonly a: string;', '  readonly b: string;', '  c: string;', '};'),
			output: lines('type A = {', '  readonly a: string;', '  readonly b: string;', '', '  c: string;', '};'),
			errors: 1,
		},

		{
			code: lines('class A {', '  a = 1;', '  b = 2;', '  // the shared state', '  static c = 3;', '}'),
			output: lines('class A {', '  a = 1;', '  b = 2;', '', '  // the shared state', '  static c = 3;', '}'),
			errors: 1,
			name: 'a comment directly above the next member moves with it',
		},
	],
});
