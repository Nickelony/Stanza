import keepMultiLine from '../../src/rules/keep-multi-line-switch-cases-together.ts';
import keepSingleLine from '../../src/rules/keep-single-line-switch-cases-together.ts';
import separateMultiLine from '../../src/rules/separate-multi-line-switch-cases.ts';
import { createRuleTester, crlf, lines } from '../helpers.ts';

const tester = createRuleTester();
const apart = lines('switch (x) {', '  case 1:', '    foo();', '    break;', '', '  case 2:', '    bar();', '}');
const tight = lines('switch (x) {', '  case 1:', '    foo();', '    break;', '  case 2:', '    bar();', '}');
const singleApart = lines('switch (x) {', '  case 1: return a;', '', '  case 2: return b;', '}');
const singleTight = lines('switch (x) {', '  case 1: return a;', '  case 2: return b;', '}');

tester.run('separate-multi-line-switch-cases', separateMultiLine as never, {
	valid: [
		{ code: apart },
		{ code: singleTight, name: 'two single-line cases stay tight' },

		{
			code: lines('switch (x) {', '  case 1:', '  case 2:', '    foo();', '    break;', '', '  default:', '    bar();', '}'),
			name: 'an empty case falls through',
		},

		{ code: lines('switch (x) {', '  case 1:', '  case 2:', '', '  case 3:', '    foo();', '}'), name: 'a blank line after an empty case stays' },
		{ code: lines('switch (x) {', '  case 1:', '    foo();', '    break;', '}'), name: 'a lone case has no neighbor' },
		{ code: lines('foo();', 'if (a) {', '  bar();', '}'), name: 'statements belong to separate-multi-line-constructs' },
	],

	invalid: [
		{ code: tight, output: apart, errors: 1 },

		{
			code: lines('switch (x) {', '  case 1: return a;', '  case 2:', '    foo();', '    return b;', '}'),
			output: lines('switch (x) {', '  case 1: return a;', '', '  case 2:', '    foo();', '    return b;', '}'),
			errors: 1,
			name: 'a multi-line case below a single-line one',
		},

		{
			code: lines('switch (x) {', '  case 1:', '    foo();', '    break;', '  // the last one', '  default:', '    bar();', '}'),
			output: lines('switch (x) {', '  case 1:', '    foo();', '    break;', '', '  // the last one', '  default:', '    bar();', '}'),
			errors: 1,
			name: 'a comment directly above a case travels with it',
		},

		{
			code: lines('switch (x) {', '  case 1:', '    foo();', '    break; // done', '  case 2:', '    bar();', '}'),
			output: lines('switch (x) {', '  case 1:', '    foo();', '    break; // done', '', '  case 2:', '    bar();', '}'),
			errors: 1,
			name: 'a comment that trails the case above stays with it',
		},

		{
			code: lines(
				'switch (x) {',
				'  case 1:',
				'  case 2:',
				'    foo();',
				'    break;',
				'  case 3:',
				'    bar();',
				'    break;',
				'  default:',
				'    baz();',
				'}',
			),

			output: lines(
				'switch (x) {',
				'  case 1:',
				'  case 2:',
				'    foo();',
				'    break;',
				'',
				'  case 3:',
				'    bar();',
				'    break;',
				'',
				'  default:',
				'    baz();',
				'}',
			),

			errors: 2,
			name: 'the labels of a fall-through list stay together',
		},

		{ code: crlf(tight), output: crlf(apart), errors: 1 },

		{
			code: lines('function f() {', '  switch (x) {', '    case 1:', '      foo();', '      break;', '    case 2:', '      bar();', '  }', '}'),
			output: lines('function f() {', '  switch (x) {', '    case 1:', '      foo();', '      break;', '', '    case 2:', '      bar();', '  }', '}'),
			errors: 1,
			name: 'a switch in a function',
		},
	],
});

tester.run('keep-multi-line-switch-cases-together', keepMultiLine as never, {
	valid: [
		{ code: tight },
		{ code: singleApart, name: 'two single-line cases belong to the single-line rule' },

		{
			code: lines('switch (x) {', '  case 1:', '    foo();', '    break;', '', '  // note', '  case 2:', '    bar();', '}'),
			name: 'a comment keeps the blank line',
		},

		{
			code: lines('switch (x) {', '  case 1:', '    foo();', '    break; // done', '', '  case 2:', '    bar();', '}'),
			name: 'a trailing comment keeps the blank line',
		},

		{ code: lines('switch (x) {', '  case 1:', '', '  case 2:', '    foo();', '}'), name: 'an empty case stays with the next one' },
		{ code: lines('foo();', '', 'if (a) {', '  bar();', '}'), name: 'statements are no cases' },
	],

	invalid: [
		{ code: apart, output: tight, errors: 1 },

		{
			code: lines('switch (x) {', '  case 1: return a;', '', '  case 2:', '    foo();', '    return b;', '}'),
			output: lines('switch (x) {', '  case 1: return a;', '  case 2:', '    foo();', '    return b;', '}'),
			errors: 1,
			name: 'a multi-line case below a single-line one',
		},

		{
			code: lines('switch (x) {', '  case 1:', '    foo();', '    break;', '', '', '  case 2:', '    bar();', '}'),
			output: tight,
			errors: 1,
			name: 'several blank lines in a row all go',
		},

		{
			code: lines('switch (x) {', '  case 1:', '    foo();', '    break;', '  ', '  case 2:', '    bar();', '}'),
			output: tight,
			errors: 1,
			name: 'a line of whitespace is a blank line',
		},

		{ code: crlf(apart), output: crlf(tight), errors: 1 },
	],
});

tester.run('keep-single-line-switch-cases-together', keepSingleLine as never, {
	valid: [
		{ code: singleTight },
		{ code: apart, name: 'a multi-line case keeps the blank line' },
		{ code: lines('switch (x) {', '  case 1: return a;', '', '  // note', '  case 2: return b;', '}'), name: 'a comment keeps the blank line' },
		{ code: lines('switch (x) {', '  case 1: return a; // first', '', '  case 2: return b;', '}'), name: 'a trailing comment keeps the blank line' },
		{ code: lines('switch (x) {', '  case 1:', '', '  case 2: return b;', '}'), name: 'an empty case stays with the next one' },
	],

	invalid: [
		{ code: singleApart, output: singleTight, errors: 1 },

		{
			code: lines('switch (x) {', '  case 1: return a;', '', '  case 2: return b;', '', '  default: return c;', '}'),
			output: lines('switch (x) {', '  case 1: return a;', '  case 2: return b;', '  default: return c;', '}'),
			errors: 2,
		},

		{ code: crlf(singleApart), output: crlf(singleTight), errors: 1 },
	],
});
