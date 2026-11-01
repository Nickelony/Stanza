import keepMultiLine from '../../src/rules/keep-multi-line-jsx-children-together.ts';
import keepSingleLine from '../../src/rules/keep-single-line-jsx-children-together.ts';
import separateCommented from '../../src/rules/separate-commented-jsx-children.ts';
import separateMultiLine from '../../src/rules/separate-multi-line-jsx-children.ts';
import { createRuleTester, crlf, lines } from '../helpers.ts';

const tester = createRuleTester();

/** Wraps the lines of the children in an element of a component. */
const view = (...children: string[]): string =>
	lines('const view = (', '  <div>', ...children.map((child) => (child === '' ? child : `    ${child}`)), '  </div>', ');');

const multi = ['<B>', '  x', '</B>'];

tester.run('separate-multi-line-jsx-children (spaces)', separateMultiLine as never, {
	valid: [
		{ code: view('<A />', "{' '}", '<B>', '  x', '</B>'), name: 'a space between inline elements is text' },
		{ code: view('<A />', "{' '}", '<B>', '  x', '</B>', '<C />'), name: 'a space makes every gap of the element joined' },
	],

	invalid: [
		{
			code: view('<A />', "{'  '}", '<B>', '  x', '</B>'),
			output: view('<A />', "{'  '}", '', '<B>', '  x', '</B>'),
			errors: 1,
			name: 'only the string of one space is a space of the text',
		},
	],
});

tester.run('keep-single-line-jsx-children-together', keepSingleLine as never, {
	valid: [
		{ code: view('<A />', '<B />') },
		{ code: view('<A />', '', ...multi), name: 'a multi-line child keeps the blank line' },
		{ code: view(...multi, '', '<A />'), name: 'a multi-line child above keeps the blank line' },
		{ code: view('<A />', '', '{/* note */}', '<B />'), name: 'a comment above the child keeps the blank line' },
		{ code: view('<A />', '', 'text', '<B />'), name: 'an element with text is left as written' },
		{ code: view('<A />', '', "{' '}", '<B />'), name: 'an element with a space is left as written' },
		{ code: view('<A />', '<B />', '', ...multi), name: 'the pair beside a multi-line child' },
		{ code: lines('const view = <div><A /><B /></div>;'), name: 'children that share a line' },
		{ code: view('<A />'), name: 'a lone child has no neighbor' },
		{ code: lines('const o = {', '  a: 1,', '', '  b: 2,', '};'), name: 'a list is no element' },
	],

	invalid: [
		{ code: view('<A />', '', '<B />'), output: view('<A />', '<B />'), errors: 1 },
		{ code: view('{a}', '', '{b}'), output: view('{a}', '{b}'), errors: 1, name: 'expressions are children' },

		{
			code: view('<A />', '', '<B />', '', '<C />'),
			output: view('<A />', '<B />', '<C />'),
			errors: 2,
		},

		{
			code: lines('const view = (', '  <>', '    <A />', '', '    <B />', '  </>', ');'),
			output: lines('const view = (', '  <>', '    <A />', '    <B />', '  </>', ');'),
			errors: 1,
			name: 'a fragment is an element',
		},

		{
			code: view('<A />', '', '', '<B />'),
			output: view('<A />', '<B />'),
			errors: 1,
			name: 'several blank lines in a row all go',
		},

		{
			code: view('<A />', '{/* tool */}', '', '<B />'),
			output: view('<A />', '{/* tool */}', '<B />'),
			errors: 1,
			name: 'a comment that stands apart from the child below is a child, and the blank line below it goes',
		},

		{
			code: view('<A />', '', '{/* one */}', '', '{/* two */}'),
			output: view('<A />', '{/* one */}', '{/* two */}'),
			errors: 2,
			name: 'two comments that stand apart are two children',
		},

		{ code: crlf(view('<A />', '', '<B />')), output: crlf(view('<A />', '<B />')), errors: 1 },

		{
			code: lines('const view = (', '  <div>', '    <A>', '      <B />', '', '      <C />', '    </A>', '  </div>', ');'),
			output: lines('const view = (', '  <div>', '    <A>', '      <B />', '      <C />', '    </A>', '  </div>', ');'),
			errors: 1,
			name: 'a nested element is judged by its own children',
		},
	],
});

tester.run('keep-multi-line-jsx-children-together', keepMultiLine as never, {
	valid: [
		{ code: view('<A />', ...multi) },
		{ code: view('<A />', '', '<B />'), name: 'two single-line children belong to the single-line rule' },
		{ code: view('<A />', '', '{/* note */}', ...multi), name: 'a comment above the child keeps the blank line' },
		{ code: view('<A />', '', ...multi, 'text'), name: 'an element with text is left as written' },
	],

	invalid: [
		{ code: view('<A />', '', ...multi), output: view('<A />', ...multi), errors: 1 },
		{ code: view(...multi, '', '<A />'), output: view(...multi, '<A />'), errors: 1, name: 'a multi-line child above' },

		{
			code: view('<A />', '', ...multi, '', '<C />'),
			output: view('<A />', ...multi, '<C />'),
			errors: 2,
		},

		{ code: crlf(view('<A />', '', ...multi)), output: crlf(view('<A />', ...multi)), errors: 1 },
	],
});

tester.run('separate-commented-jsx-children', separateCommented as never, {
	valid: [
		{ code: view('<A />', '', '{/* note */}', '<B />') },
		{ code: view('<A />', '<B />'), name: 'no comment' },
		{ code: view('{/* note */}', '<A />', '<B />'), name: 'a comment above the first child has no sibling above' },
		{ code: view('<A />', '{/* eslint-disable-next-line */}', '<B />'), name: 'a tool directive does not lead a group' },
		{ code: view('<A />', '{/* @ts-expect-error */}', '<B />'), name: 'a type directive does not lead a group' },
		{ code: view('<A />', '{/* note */}', '', '<B />'), name: 'a comment separated from the child is a child of its own' },
		{ code: view('<A />', '{/* note */}', ...multi), name: 'a multi-line child belongs to separate-multi-line-jsx-children' },
		{ code: view(...multi, '{/* note */}', '<A />'), name: 'a multi-line sibling belongs to separate-multi-line-jsx-children' },
		{ code: view('<A />', 'text', '{/* note */}', '<B />'), name: 'an element with text is left as written' },
		{ code: view('<A />', "{' '}", '{/* note */}', '<B />'), name: 'an element with a space is left as written' },
		{ code: lines('const view = <div><A />{/* note */}<B /></div>;'), name: 'children that share a line' },
	],

	invalid: [
		{
			code: view('<A />', '{/* note */}', '<B />'),
			output: view('<A />', '', '{/* note */}', '<B />'),
			errors: 1,
		},

		{
			code: view('<A />', '{/* one */}', '{/* two */}', '<B />'),
			output: view('<A />', '', '{/* one */}', '{/* two */}', '<B />'),
			errors: 1,
			name: 'a run of comments moves together',
		},

		{
			code: view('<A />', '{/* eslint-disable-next-line */}', '{/* note */}', '<B />'),
			output: view('<A />', '', '{/* eslint-disable-next-line */}', '{/* note */}', '<B />'),
			errors: 1,
			name: 'a note beside a tool directive leads the group',
		},

		{
			code: view('{a}', '{/* note */}', '{b}'),
			output: view('{a}', '', '{/* note */}', '{b}'),
			errors: 1,
		},

		{
			code: view('<A />', '{// note', '}', '<B />'),
			output: view('<A />', '', '{// note', '}', '<B />'),
			errors: 1,
			name: 'a line comment in the braces counts',
		},

		{
			code: lines('const view = (', '  <>', '    <A />', '    {/* note */}', '    <B />', '  </>', ');'),
			output: lines('const view = (', '  <>', '    <A />', '', '    {/* note */}', '    <B />', '  </>', ');'),
			errors: 1,
			name: 'a fragment is an element',
		},

		{
			code: crlf(view('<A />', '{/* note */}', '<B />')),
			output: crlf(view('<A />', '', '{/* note */}', '<B />')),
			errors: 1,
		},
	],
});
