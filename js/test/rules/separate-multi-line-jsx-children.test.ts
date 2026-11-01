import rule from '../../src/rules/separate-multi-line-jsx-children.ts';
import { createRuleTester, crlf, lines } from '../helpers.ts';

createRuleTester().run('separate-multi-line-jsx-children', rule as never, {
	valid: [
		{ code: lines('const a = (', '  <div>', '    <A />', '    <B />', '  </div>', ');') },
		{ code: lines('const a = (', '  <div>', '    <A />', '', '    <B>', '      x', '    </B>', '  </div>', ');') },
		{ code: lines('const a = (', '  <p>', '    Hello <b>there</b> <i>', '      you', '    </i>', '  </p>', ');') },
		{ code: lines('const a = <div><A /><B>', '  x', '</B></div>;') },

		{
			code: lines('const a = (', '  <div>', '    <A />', '    <B>', '      x', '    </B>', '    text', '  </div>', ');'),
			name: 'an element with text keeps no blank line, because Prettier removes it',
		},

		{ code: lines('const a = (', '  <>', '    <A />', '', '    <B>', '      x', '    </B>', '  </>', ');') },
		{ code: lines('const a = (', '  <div>', '    <B>', '      x', '    </B>', '  </div>', ');'), name: 'a lone child has no neighbor' },
		{ code: lines('if (a) {', '  foo();', '}', 'bar();'), name: 'statements are left to separate-multi-line-constructs' },
		{ code: lines('switch (x) {', '  case 1:', '    foo();', '    break;', '  case 2:', '    bar();', '}'), name: 'cases are left to it too' },
	],

	invalid: [
		{
			code: lines('const view = (', '  <div>', '    <Header />', '    <Main>', '      <Item />', '    </Main>', '    <Footer />', '  </div>', ');'),

			output: lines(
				'const view = (',
				'  <div>',
				'    <Header />',
				'',
				'    <Main>',
				'      <Item />',
				'    </Main>',
				'',
				'    <Footer />',
				'  </div>',
				');',
			),

			errors: 2,
		},

		{
			code: lines('const view = (', '  <>', '    <Header />', '    <Main>', '      <Item />', '    </Main>', '  </>', ');'),
			output: lines('const view = (', '  <>', '    <Header />', '', '    <Main>', '      <Item />', '    </Main>', '  </>', ');'),
			errors: 1,
			name: 'a fragment is an element',
		},

		{
			code: lines(
				'const view = (',
				'  <div>',
				'    <Header />',
				'    {/* the body */}',
				'    <Main>',
				'      <Item />',
				'    </Main>',
				'  </div>',
				');',
			),

			output: lines(
				'const view = (',
				'  <div>',
				'    <Header />',
				'',
				'    {/* the body */}',
				'    <Main>',
				'      <Item />',
				'    </Main>',
				'  </div>',
				');',
			),

			errors: 1,
			name: 'a comment above the child travels with it',
		},

		{
			code: lines(
				'const view = (',
				'  <div>',
				'    <A>',
				'      <B>',
				'        <C />',
				'      </B>',
				'      <D />',
				'    </A>',
				'    <E />',
				'  </div>',
				');',
			),

			output: lines(
				'const view = (',
				'  <div>',
				'    <A>',
				'      <B>',
				'        <C />',
				'      </B>',
				'',
				'      <D />',
				'    </A>',
				'',
				'    <E />',
				'  </div>',
				');',
			),

			errors: 2,
			name: 'a nested element is judged by its own children',
		},

		{
			code: crlf(lines('const view = (', '  <div>', '    <A />', '    <B>', '      x', '    </B>', '  </div>', ');')),
			output: crlf(lines('const view = (', '  <div>', '    <A />', '', '    <B>', '      x', '    </B>', '  </div>', ');')),
			errors: 1,
		},
	],
});
