import commentedSiblings from '../../src/rules/separate-commented-siblings.ts';
import directives from '../../src/rules/separate-directives.ts';
import guardClauses from '../../src/rules/separate-guard-clauses.ts';
import importSection from '../../src/rules/separate-import-section.ts';
import jumpStatements from '../../src/rules/separate-jump-statements.ts';
import memberGroups from '../../src/rules/separate-member-groups.ts';
import testBlocks from '../../src/rules/separate-test-blocks.ts';
import { createRuleTester, lines } from '../helpers.ts';

const tester = createRuleTester();

tester.run('separate-guard-clauses', guardClauses as never, {
	valid: [
		{ code: lines('function f(order) {', '  if (order.closed) return;', '', '  validate(order);', '  order.close();', '}') },
		{ code: lines('function f(a, b) {', '  if (!a) return;', '  if (!b) throw new Error("b");', '', '  work(a, b);', '}') },
		{ code: lines('function f(a) {', '  work(a);', '  if (a.done) return;', '  more(a);', '}') },
		{ code: lines('function f(a) {', '  if (!a) return;', '  if (a.done) return;', '}') },
		{ code: lines('function f(a) {', '  if (a) {', '    log(a);', '  }', '  more(a);', '}') },
		{ code: lines('function f(a) {', '  if (a) return 1;', '  else return 2;', '}') },
	],

	invalid: [
		{
			code: lines('function f(order) {', '  assert(order);', '  if (order.closed) return;', '  validate(order);', '  order.close();', '}'),
			output: lines('function f(order) {', '  assert(order);', '  if (order.closed) return;', '', '  validate(order);', '  order.close();', '}'),
			errors: 1,
		},

		{
			code: lines('function f(a) {', '  if (!a) {', '    return;', '  }', '  work(a);', '  more(a);', '}'),
			output: lines('function f(a) {', '  if (!a) {', '    return;', '  }', '', '  work(a);', '  more(a);', '}'),
			errors: 1,
		},

		{
			code: lines('function f(a) {', '  invariant(a, "a");', '  work(a);', '  more(a);', '}'),
			output: lines('function f(a) {', '  invariant(a, "a");', '', '  work(a);', '  more(a);', '}'),
			errors: 1,
		},

		{
			code: lines('function f(a) {', '  guard(a);', '  work(a);', '  more(a);', '}'),
			output: lines('function f(a) {', '  guard(a);', '', '  work(a);', '  more(a);', '}'),
			settings: { stanza: { guardRoots: ['guard'] } },
			errors: 1,
		},

		{
			code: lines('for (const x of xs) {', '  if (!x) continue;', '  // handle it', '  handle(x);', '  done(x);', '}'),
			output: lines('for (const x of xs) {', '  if (!x) continue;', '', '  // handle it', '  handle(x);', '  done(x);', '}'),
			errors: 1,
		},
	],
});

tester.run('separate-jump-statements', jumpStatements as never, {
	valid: [
		{ code: lines('function f(id) {', '  const order = load(id);', '  return order;', '}') },
		{ code: lines('function f(id) {', '  const order = load(id);', '  validate(order);', '', '  return order;', '}') },
		{ code: lines('function f(id) {', '  if (id) return 1;', '  return 2;', '}') },
		{ code: lines('function f() {', '  return 1;', '}') },
		{ code: lines('switch (x) {', '  case 1:', '    log(x);', '    work(x);', '    break;', '}') },
		{ code: lines('switch (x) {', '  case 1: {', '    log(x);', '    work(x);', '    break;', '  }', '}') },
		{ code: lines('outer: for (const a of xs) {', '  log(a);', '  work(a);', '  continue outer;', '}') },
		{ code: lines("import a from 'a';", "import b from 'b';", 'throw new Error(a + b);'), name: 'an import ends the stanza above the jump' },

		{
			code: lines('function f(a) {', '  const x = g(a);', '  return x;', '}'),
			name: 'a stanza of one line above the jump keeps it tight',
		},
	],

	invalid: [
		{
			code: lines('function f(id) {', '  const order = load(id);', '  validate(order);', '  order.close();', '  return order;', '}'),
			output: lines('function f(id) {', '  const order = load(id);', '  validate(order);', '  order.close();', '', '  return order;', '}'),
			errors: 1,
		},

		{
			code: lines('function f(a) {', '  const x = a', '    .map(g)', '    .filter(h);', '  return x;', '}'),
			output: lines('function f(a) {', '  const x = a', '    .map(g)', '    .filter(h);', '', '  return x;', '}'),
			errors: 1,
		},

		{
			code: lines('function f(a, b) {', '  if (!a) return 1;', '  if (!b) return 2;', '  return 3;', '}'),
			output: lines('function f(a, b) {', '  if (!a) return 1;', '  if (!b) return 2;', '', '  return 3;', '}'),
			errors: 1,
		},

		{
			code: lines('function f(a) {', '  log(a);', '  work(a);', '  // done', '  throw new Error("x");', '}'),
			output: lines('function f(a) {', '  log(a);', '  work(a);', '', '  // done', '  throw new Error("x");', '}'),
			errors: 1,
		},

		{
			code: lines('for (const a of xs) {', '  log(a);', '  work(a);', '  continue;', '}'),
			output: lines('for (const a of xs) {', '  log(a);', '  work(a);', '', '  continue;', '}'),
			errors: 1,
		},

		{
			code: lines('switch (x) {', '  case 1:', '    log(x);', '    work(x);', '    return x;', '}'),
			output: lines('switch (x) {', '  case 1:', '    log(x);', '    work(x);', '', '    return x;', '}'),
			errors: 1,
		},

		{
			code: lines('function f(a) {', '  log(a);', '  work(a);', '  return 1;', '  return 2;', '}'),
			output: lines('function f(a) {', '  log(a);', '  work(a);', '', '  return 1;', '  return 2;', '}'),
			errors: 1,
			name: 'a jump that asks for a blank line starts the stanza below it',
		},

		{
			code: lines('function f(a) {', '  log(a);', '  if (a) {', '    work(a);', '  }', '  return 1;', '}'),
			output: lines('function f(a) {', '  log(a);', '  if (a) {', '    work(a);', '  }', '', '  return 1;', '}'),
			errors: 1,
			name: 'a multi-line statement above the jump is a stanza of several lines',
		},

		{
			code: lines('for (const a of xs) {', '  if (a) {', '    log(a);', '    work(a);', '    break;', '  }', '}'),
			output: lines('for (const a of xs) {', '  if (a) {', '    log(a);', '    work(a);', '', '    break;', '  }', '}'),
			errors: 1,
		},
	],
});

tester.run('separate-commented-siblings', commentedSiblings as never, {
	valid: [
		{ code: lines('// Opens the file.', 'open();', 'read();') },
		{ code: lines('open();', '', '// Reads the file.', 'read();') },
		{ code: lines('open();', '// @ts-expect-error the type is wrong', 'read(1);') },
		{ code: lines('open();', '// eslint-disable-next-line no-console', 'console.log(1);') },
		{ code: lines('open(); // trailing', 'read();') },
		{ code: lines('open();', '/* istanbul ignore next */', 'read();') },
		{ code: lines('open();', '// heading', 'if (x) {', '  read();', '}') },
		{ code: lines('class A {', '  a = 1;', '', '  // the size', '  size = 2;', '}') },
	],

	invalid: [
		{
			code: lines('open();', '// Reads the file.', 'read();'),
			output: lines('open();', '', '// Reads the file.', 'read();'),
			errors: 1,
		},

		{
			code: lines('open(); // trailing', '// Reads the file.', 'read();'),
			output: lines('open(); // trailing', '', '// Reads the file.', 'read();'),
			errors: 1,
		},

		{
			code: lines('open();', '/**', ' * Reads.', ' */', 'read();'),
			output: lines('open();', '', '/**', ' * Reads.', ' */', 'read();'),
			errors: 1,
		},

		{
			code: lines('class A {', '  a = 1;', '  // the size', '  size = 2;', '}'),
			output: lines('class A {', '  a = 1;', '', '  // the size', '  size = 2;', '}'),
			errors: 1,
		},

		{
			code: lines('interface A {', '  a: string;', '  /** The size. */', '  size: number;', '}'),
			output: lines('interface A {', '  a: string;', '', '  /** The size. */', '  size: number;', '}'),
			errors: 1,
		},

		{
			code: lines('open();', '// @ts-expect-error', '// Reads the file.', 'read(1);'),
			output: lines('open();', '', '// @ts-expect-error', '// Reads the file.', 'read(1);'),
			errors: 1,
		},
	],
});

tester.run('separate-import-section', importSection as never, {
	valid: [
		{ code: lines("import a from 'a';", "import b from 'b';", '', 'use(a, b);') },
		{ code: lines("import a from 'a';") },
		{ code: lines('use(1);', "import a from 'a';") },
		{ code: lines("'use client';", '', "import a from 'a';", '', 'use(a);') },
	],

	invalid: [
		{
			code: lines("import a from 'a';", "import type { B } from 'b';", "import 'side-effect';", 'use(a);'),
			output: lines("import a from 'a';", "import type { B } from 'b';", "import 'side-effect';", '', 'use(a);'),
			errors: 1,
		},

		{
			code: lines("import a from 'a';", "export * from 'b';"),
			output: lines("import a from 'a';", '', "export * from 'b';"),
			errors: 1,
		},

		{
			code: lines("import fs = require('fs');", 'use(fs);'),
			output: lines("import fs = require('fs');", '', 'use(fs);'),
			errors: 1,
		},

		{
			code: lines('// @ts-check', "import a from 'a';", '// the work', 'use(a);'),
			output: lines('// @ts-check', "import a from 'a';", '', '// the work', 'use(a);'),
			errors: 1,
		},

		{
			code: lines('declare module "x" {', "  import a from 'a';", '  const b: typeof a;', '}'),
			output: lines('declare module "x" {', "  import a from 'a';", '', '  const b: typeof a;', '}'),
			errors: 1,
		},
	],
});

tester.run('separate-directives', directives as never, {
	valid: [
		{ code: lines("'use strict';", '', 'run();') },
		{ code: lines("'use client';") },
		{ code: lines('run();', "'use strict';", 'more();'), name: 'a string statement below code is no directive' },
		{ code: lines('#!/usr/bin/env node', '', 'run();') },
		{ code: lines('function f() {', "  'use strict';", '', '  run();', '}') },
	],

	invalid: [
		{
			code: lines("'use strict';", 'run();'),
			output: lines("'use strict';", '', 'run();'),
			errors: 1,
		},

		{
			code: lines("'use client';", "'use strict';", "import a from 'a';"),
			output: lines("'use client';", "'use strict';", '', "import a from 'a';"),
			errors: 1,
		},

		{
			code: lines('#!/usr/bin/env node', 'run();'),
			output: lines('#!/usr/bin/env node', '', 'run();'),
			errors: 1,
		},

		{
			code: lines('#!/usr/bin/env node', "'use strict';", 'run();'),
			output: lines('#!/usr/bin/env node', '', "'use strict';", '', 'run();'),
			errors: 2,
		},

		{
			code: lines('function f() {', "  'use strict';", '  run();', '}'),
			output: lines('function f() {', "  'use strict';", '', '  run();', '}'),
			errors: 1,
		},

		{
			code: lines('#!/usr/bin/env node', '// the entry', 'run();'),
			output: lines('#!/usr/bin/env node', '', '// the entry', 'run();'),
			errors: 1,
		},
	],
});

tester.run('separate-member-groups', memberGroups as never, {
	valid: [
		{ code: lines('class A {', '  a = 1;', '  b = 2;', '', '  get c() {', '    return 1;', '  }', '}') },
		{ code: lines('class A {', '  a = 1;', '  run() {}', '}') },
		{ code: lines('class A {', '  run() {}', '  stop() {}', '}') },
		{ code: lines('class A {', '  get c(): number {', '    return 1;', '  }', '  set c(next: number) {}', '}') },
		{ code: lines('interface A {', '  a: string;', '  b: number;', '', '  run(): void;', '  stop(): void;', '}') },
	],

	invalid: [
		{
			code: lines(
				'class Widget {',
				'  private count = 0;',
				'  private name = "";',
				'  get total() {',
				'    return 1;',
				'  }',
				'  get label() {',
				'    return "";',
				'  }',
				'  reset() {}',
				'  clear() {}',
				'}',
			),

			output: lines(
				'class Widget {',
				'  private count = 0;',
				'  private name = "";',
				'',
				'  get total() {',
				'    return 1;',
				'  }',
				'  get label() {',
				'    return "";',
				'  }',
				'',
				'  reset() {}',
				'  clear() {}',
				'}',
			),

			errors: 2,
		},

		{
			code: lines('class A {', '  static a = 1;', '  static b = 2;', '  constructor() {}', '}'),
			output: lines('class A {', '  static a = 1;', '  static b = 2;', '', '  constructor() {}', '}'),
			errors: 1,
		},

		{
			code: lines('interface A {', '  a: string;', '  b: number;', '  run(): void;', '}'),
			output: lines('interface A {', '  a: string;', '  b: number;', '', '  run(): void;', '}'),
			errors: 1,
		},

		{
			code: lines('type A = {', '  a: string;', '  b: number;', '  (x: number): void;', '};'),
			output: lines('type A = {', '  a: string;', '  b: number;', '', '  (x: number): void;', '};'),
			errors: 1,
		},
	],
});

tester.run('separate-test-blocks', testBlocks as never, {
	valid: [
		{ code: lines('it("a", () => {});', '', 'it("b", () => {});') },
		{ code: lines('const a = 1;', 'foo(a);') },
		{ code: lines('foo();', 'it("a", () => {});') },
	],

	invalid: [
		{
			code: lines(
				'describe("a", () => {',
				'  beforeEach(() => {});',
				'  it("b", () => {});',
				'  it.skip("c", () => {});',
				'  test.each([1, 2])("d", () => {});',
				'});',
			),

			output: lines(
				'describe("a", () => {',
				'  beforeEach(() => {});',
				'',
				'  it("b", () => {});',
				'',
				'  it.skip("c", () => {});',
				'',
				'  test.each([1, 2])("d", () => {});',
				'});',
			),

			errors: 3,
		},

		{
			code: lines('it.todo("a");', 'it.todo("b");', 'it.todo("c");'),
			output: lines('it.todo("a");', '', 'it.todo("b");', '', 'it.todo("c");'),
			errors: 2,
		},

		{
			code: lines('it("a", () => {});', '// the second', 'it("b", () => {});'),
			output: lines('it("a", () => {});', '', '// the second', 'it("b", () => {});'),
			errors: 1,
		},

		{
			code: lines('describe("a", () => {});', 'describe("b", () => {});', 'it("c", () => {});'),
			output: lines('describe("a", () => {});', '', 'describe("b", () => {});', 'it("c", () => {});'),
			settings: { stanza: { testRoots: ['describe'] } },
			errors: 1,
		},
	],
});
