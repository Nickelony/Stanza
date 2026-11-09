import rule from '../../src/rules/separate-import-groups.ts';
import { createRuleTester, crlf, lines } from '../helpers.ts';

const types = [{ typeImports: true }];

createRuleTester().run('separate-import-groups', rule as never, {
	valid: [
		{ code: lines("import { a } from 'a';") },
		{ code: lines("import { a } from 'a';", "import b from 'b';", "import * as c from 'c';"), name: 'value imports of several shapes are one kind' },
		{ code: lines("import 'a';", "import 'b';"), name: 'side-effect imports are one kind' },
		{ code: lines("import './a.ts';", '', "import { b } from 'b';") },
		{ code: lines("import { a } from 'a';", '', "import 'b';") },
		{ code: lines("import { a } from 'a';", '', '', "import 'b';"), name: 'a blank line stays, however many stand there' },
		{ code: lines("import type { a } from 'a';", "import { b } from 'b';"), name: 'a type import is a value import without the option' },
		{ code: lines("import { type A, b } from 'a';", "import c from 'c';"), options: types, name: 'an inline type specifier makes a value import' },
		{ code: lines("import type { A } from 'a';", "import type { B } from 'b';"), options: types },
		{ code: lines("import { a } from 'a';", "export { b } from 'b';", "import 'c';"), name: 'a re-export is no import' },
		{ code: lines("import 'a';", 'run();'), name: 'the gap below the imports belongs to separate-import-section' },
		{ code: lines("import { a } from 'a';", 'run();', "import 'b';"), name: 'a statement between two imports' },
		{ code: lines("import { a } from 'a';", "import b = require('b');"), name: 'an import equals is a value import' },
		{ code: lines('import {', '  a,', "} from 'a';", 'import {', '  b,', "} from 'b';") },
		{ code: lines('declare module "x" {', "  import { a } from 'a';", "  import { b } from 'b';", '}') },
	],

	invalid: [
		{
			code: lines("import './polyfills.ts';", "import { a } from 'a';", "import { b } from './b.ts';"),
			output: lines("import './polyfills.ts';", '', "import { a } from 'a';", "import { b } from './b.ts';"),
			errors: 1,
		},

		{
			code: lines("import { a } from 'a';", "import './setup.ts';"),
			output: lines("import { a } from 'a';", '', "import './setup.ts';"),
			errors: 1,
			name: 'a side-effect import below a value import',
		},

		{
			code: lines("import { a } from 'a';", "import 'b';", "import { c } from 'c';"),
			output: lines("import { a } from 'a';", '', "import 'b';", '', "import { c } from 'c';"),
			errors: 2,
			name: 'a group of one import is set apart on both sides',
		},

		{
			code: lines("import 'a';", "import 'b';", "import { c } from 'c';", "import 'd';"),
			output: lines("import 'a';", "import 'b';", '', "import { c } from 'c';", '', "import 'd';"),
			errors: 2,
		},

		{
			code: lines("import {} from 'a';", "import { b } from 'b';"),
			output: lines("import {} from 'a';", '', "import { b } from 'b';"),
			errors: 1,
			name: 'an import that binds nothing is a side-effect import',
		},

		{
			code: lines("import './a.ts';", "import data from './data.json' with { type: 'json' };"),
			output: lines("import './a.ts';", '', "import data from './data.json' with { type: 'json' };"),
			errors: 1,
		},

		{
			code: lines("import b = require('b');", "import 'a';"),
			output: lines("import b = require('b');", '', "import 'a';"),
			errors: 1,
			name: 'an import equals is a value import',
		},

		{
			code: lines("import 'a';", '// The parser.', "import { parse } from './parse.ts';"),
			output: lines("import 'a';", '', '// The parser.', "import { parse } from './parse.ts';"),
			errors: 1,
			name: 'a comment directly above an import travels with it',
		},

		{
			code: lines("import 'a'; // first", "import { b } from 'b';"),
			output: lines("import 'a'; // first", '', "import { b } from 'b';"),
			errors: 1,
			name: 'a comment that trails the import above stays with it',
		},

		{
			code: lines("import 'a';", 'import {', '  b,', '  c,', "} from 'b';"),
			output: lines("import 'a';", '', 'import {', '  b,', '  c,', "} from 'b';"),
			errors: 1,
			name: 'a multi-line import is asked once',
		},

		{
			code: lines('run();', "import { a } from 'a';", "import 'b';"),
			output: lines('run();', "import { a } from 'a';", '', "import 'b';"),
			errors: 1,
			name: 'imports below a statement are judged too',
		},

		{
			code: lines('declare module "x" {', "  import 'a';", "  import { b } from 'b';", '}'),
			output: lines('declare module "x" {', "  import 'a';", '', "  import { b } from 'b';", '}'),
			errors: 1,
		},

		{
			code: crlf(lines("import 'a';", "import { b } from 'b';")),
			output: crlf(lines("import 'a';", '', "import { b } from 'b';")),
			errors: 1,
		},

		{
			code: lines("import type { A } from 'a';", "import { b } from 'b';"),
			output: lines("import type { A } from 'a';", '', "import { b } from 'b';"),
			options: types,
			errors: 1,
			name: 'a type import above a value import',
		},

		{
			code: lines("import { a } from 'a';", "import type { B } from 'b';"),
			output: lines("import { a } from 'a';", '', "import type { B } from 'b';"),
			options: types,
			errors: 1,
			name: 'a type import below a value import',
		},

		{
			code: lines("import type { A } from 'a';", "import 'b';", "import type { C } from 'c';"),
			output: lines("import type { A } from 'a';", '', "import 'b';", '', "import type { C } from 'c';"),
			options: types,
			errors: 2,
			name: 'a side-effect import between two type imports',
		},

		{
			code: lines("import type { A } from 'a';", "import type b = require('b');", "import c = require('c');"),
			output: lines("import type { A } from 'a';", "import type b = require('b');", '', "import c = require('c');"),
			options: types,
			errors: 1,
			name: 'an import type equals is a type import',
		},

		{
			code: lines("import type {} from 'a';", "import { b } from 'b';"),
			output: lines("import type {} from 'a';", '', "import { b } from 'b';"),
			options: types,
			errors: 1,
			name: 'an import type that binds nothing is a type import',
		},
	],
});
