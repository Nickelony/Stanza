import rule from '../../src/rules/keep-two-units-together.ts';
import { createRuleTester, crlf, lines } from '../helpers.ts';

createRuleTester().run('keep-two-units-together', rule as never, {
	valid: [
		{ code: lines('function f(x) {', '  const next = decode(x);', '  return next.y;', '}') },
		{ code: lines('function f(x) {', '  const a = decode(x);', '', '  // the result', '  return a.y;', '}') },
		{ code: lines('function f(x) {', '  const a = decode(x); // trailing', '', '  return a.y;', '}') },
		{ code: lines('function f(x) {', '  const a = decode(x);', '', '  return {', '    y: a.y,', '  };', '}') },
		{ code: lines('function f(x) {', '  const a = decode(x);', '', '  const b = decode(a);', '', '  return b;', '}') },
		{ code: lines('function f(x) {', '  function g() {}', '', '  return g;', '}') },
		{ code: lines("import a from 'a';", '', 'use(a);') },
		{ code: lines("'use strict';", '', 'run();') },
		{ code: lines('#!/usr/bin/env node', '', 'run();') },
		{ code: lines('function f() {}', '', 'run();') },
		{ code: lines('const a = 1;', 'foo(a); bar();') },
		{ code: lines('class A {', '  a = 1;', '  b = 2;', '', '  c = 3;', '}') },
		{ code: lines('function f() {', '  if (a) {', '    run();', '  }', '', '  return 1;', '}') },
	],

	invalid: [
		{
			code: lines('function f(x) {', '  const next = decode(x);', '', '  return next.y;', '}'),
			output: lines('function f(x) {', '  const next = decode(x);', '  return next.y;', '}'),
			errors: 1,
		},

		{
			code: lines('function f(x) {', '  const next = decode(x);', '', '', '  return next.y;', '}'),
			output: lines('function f(x) {', '  const next = decode(x);', '  return next.y;', '}'),
			errors: 1,
		},

		{
			code: crlf(lines('function f(x) {', '  const next = decode(x);', '', '  return next.y;', '}')),
			output: crlf(lines('function f(x) {', '  const next = decode(x);', '  return next.y;', '}')),
			errors: 1,
		},

		{
			code: lines('class A {', '  a = 1;', '', '  b = 2;', '}'),
			output: lines('class A {', '  a = 1;', '  b = 2;', '}'),
			errors: 1,
		},

		{
			code: lines('interface A {', '  a: string;', '', '  b(): void;', '}'),
			output: lines('interface A {', '  a: string;', '  b(): void;', '}'),
			errors: 1,
		},

		{
			code: lines('switch (x) {', '  case 1:', '    log(x);', '', '    return x;', '}'),
			output: lines('switch (x) {', '  case 1:', '    log(x);', '    return x;', '}'),
			errors: 1,
		},

		{
			code: lines('const a = 1;', '', 'run(a);'),
			output: lines('const a = 1;', 'run(a);'),
			errors: 1,
		},
	],
});
