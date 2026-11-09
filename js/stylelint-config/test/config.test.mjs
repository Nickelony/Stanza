import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import stylelint from 'stylelint';

import configuration from '../index.mjs';

import { check } from './helpers.mjs';

describe('the config', () => {
	it('sets the three empty-line rules and a syntax for the script files and one for the SCSS files', () => {
		assert.deepEqual(Object.keys(configuration.rules).sort(), [
			'at-rule-empty-line-before',
			'declaration-empty-line-before',
			'rule-empty-line-before',
		]);

		assert.equal(configuration.overrides.length, 2);
		assert.deepEqual(configuration.overrides[0].files, ['**/*.{js,jsx,mjs,cjs,ts,tsx,mts,cts}']);
		assert.equal(typeof configuration.overrides[0].customSyntax.parse, 'function');
		assert.deepEqual(configuration.overrides[1].files, ['**/*.scss']);
		assert.equal(typeof configuration.overrides[1].customSyntax.parse, 'function');
	});

	it('loads through extends by the name of the package', async () => {
		const code = 'import { css } from "@emotion/react";\nconst x = css`\ncolor: red;\n&:hover {\n color: blue;\n}\n`;';
		const result = await stylelint.lint({ code, codeFilename: 'src/example.ts', config: { extends: ['stylelint-config-stanza'] }, fix: true });

		assert.equal(result.errored, false);
		assert.equal(result.code, code.replace('red;\n&', 'red;\n\n&'));
	});

	it('loses the fixes next to an interpolation when the project sets the upstream syntax', async () => {
		const code = 'import { css } from "@emotion/react";\nconst x = css`\n&.error {\n ${border}\n .label {\n  color: red;\n }\n}\n`;';
		const overrides = [{ files: ['**/*.ts'], customSyntax: 'postcss-styled-syntax' }];

		const own = await stylelint.lint({
			code,
			codeFilename: 'src/example.ts',
			config: { extends: ['stylelint-config-stanza'], overrides },
			fix: true,
		});

		const kept = await stylelint.lint({ code, codeFilename: 'src/example.ts', config: { extends: ['stylelint-config-stanza'] }, fix: true });

		assert.equal(own.code, code);
		assert.equal(kept.code, code.replace('}\n .label', '}\n\n .label'));
	});

	it('lets a rule of the project replace a rule of the config', async () => {
		const code = 'import { css } from "@emotion/react";\nconst x = css`\ncolor: red;\n&:hover {\n color: blue;\n}\n`;';
		const config = { extends: ['stylelint-config-stanza'], rules: { 'rule-empty-line-before': null } };
		const result = await stylelint.lint({ code, codeFilename: 'src/example.ts', config, fix: true });

		assert.equal(result.code, code);
	});
});

describe('the script files', () => {
	const code = 'import { css } from "@emotion/react";\nconst x = css`\ncolor: red;\na {\n color: blue;\n}\n`;';
	const expected = code.replace('red;\na', 'red;\n\na');

	for (const extension of ['js', 'jsx', 'mjs', 'cjs', 'ts', 'tsx', 'mts', 'cts']) {
		it(`reads the templates of a .${extension} file`, async () => {
			assert.equal((await check(code, true, `src/example.${extension}`)).code, expected);
		});
	}

	it('reads the templates of styled-components, which the upstream parser recognizes', async () => {
		const styled = 'import styled from "styled-components";\nconst X = styled.div`\ncolor: red;\n&:hover {\n color: ${color};\n}\nmargin: 0;\n`;';
		const fixed = await check(styled, true);

		assert.equal(fixed.errored, false);
		assert.equal(fixed.code, styled.replace('red;\n&', 'red;\n\n&').replace('}\nmargin', '}\n\nmargin'));
	});

	it('reads a styled component that wraps another component', async () => {
		const wrapped = 'import styled from "@emotion/styled";\nconst X = styled(Button)`\ncolor: red;\na {\n color: blue;\n}\n`;';
		assert.equal((await check(wrapped, true)).code, wrapped.replace('red;\na', 'red;\n\na'));
	});

	it('checks Emotion keyframes of the css package', async () => {
		const keyframes = 'import { keyframes } from "@emotion/css";\nconst spin = keyframes`\nfrom {\n opacity: 0;\n}\nto {\n opacity: 1;\n}\n`;';
		assert.equal((await check(keyframes, true)).code, keyframes.replace('}\nto', '}\n\nto'));
	});

	it('leaves keyframes under another name unchecked', async () => {
		const aliased =
			'import { keyframes as animate } from "@emotion/react";\nconst spin = animate`\nfrom {\n opacity: 0;\n}\nto {\n opacity: 1;\n}\n`;';

		const result = await check(aliased, true);

		assert.equal(result.errored, false);
		assert.equal(result.code, aliased);
	});

	it('leaves the templates of a renamed styled or css import unchecked', async () => {
		const body = '\ncolor: red;\na {\n color: blue;\n}\n`;';
		const renamedStyled = `import s from "@emotion/styled";\nconst X = s.div\`${body}`;
		const renamedCss = `import { css as c } from "@emotion/react";\nconst X = c\`${body}`;

		assert.equal((await check(renamedStyled, true)).code, renamedStyled);
		assert.equal((await check(renamedCss, true)).code, renamedCss);
	});

	it('leaves keyframes of another library unchecked', async () => {
		const other = 'import { keyframes } from "styled-components";\nconst spin = keyframes`\nfrom {\n opacity: 0;\n}\nto {\n opacity: 1;\n}\n`;';
		assert.equal((await check(other, true)).code, other);
	});

	it('does not judge a rule whose selector holds an interpolation, as Stylelint skips it', async () => {
		const code =
			'import styled from "@emotion/styled";\nconst X = styled.div`\na {\n color: red;\n}\n${Button}:hover & {\n color: blue;\n}\nb {\n color: red;\n}\n`;';

		const fixed = await check(code, true);

		assert.equal(fixed.code, code.replace('}\nb', '}\n\nb'));
	});

	it('reports an unclosed block of a template as a syntax error', async () => {
		const result = await check('import { css } from "@emotion/react";\nconst x = css`\ncolor: red;\n&:hover {\n`;');

		assert.equal(result.errored, true);

		assert.deepEqual(
			result.results[0].warnings.map(({ rule }) => rule),
			['CssSyntaxError'],
		);
	});

	it('leaves a script that TypeScript cannot read as written', async () => {
		const broken = 'import { keyframes } from "@emotion/react";\nconst x = keyframes`\nfrom {\n}\n`;\nconst = ;';
		assert.equal((await check(broken, true)).code, broken);
	});
});

describe('the stylesheet files', () => {
	it('removes the blank line above the first child of a block', async () => {
		const fixed = await check('.card {\n\n\tcolor: red;\n}\n', true, 'src/example.css');
		assert.equal(fixed.code, '.card {\n\tcolor: red;\n}\n');
	});

	it('keeps single-line rules compact and separates a multi-line rule from its neighbor', async () => {
		const code = '.a { color: red; }\n.b { color: blue; }\n.c {\n\tcolor: green;\n}\n';
		const fixed = await check(code, true, 'src/example.css');

		assert.equal(fixed.code, code.replace('}\n.c', '}\n\n.c'));
	});

	it('separates an at-rule block from the declaration above it', async () => {
		const fixed = await check('.a {\n\tcolor: red;\n\t@media (min-width: 600px) {\n\t\tcolor: blue;\n\t}\n}\n', true, 'src/example.css');
		assert.equal(fixed.code, '.a {\n\tcolor: red;\n\n\t@media (min-width: 600px) {\n\t\tcolor: blue;\n\t}\n}\n');
	});

	it('keeps the groups of declarations and the blank lines between blockless at-rules as written', async () => {
		const code = '@use "a";\n\n@use "b";\n@use "c";\n\n.a {\n\tdisplay: flex;\n\talign-items: center;\n\n\tcolor: red;\n}\n';
		assert.equal((await check(code, true, 'src/example.scss')).code, code);
	});

	it('keeps a comment attached to the rule below it', async () => {
		const code = '.a {\n\tcolor: red;\n\n\t/* Hover. */\n\t&:hover {\n\t\tcolor: blue;\n\t}\n}\n';
		assert.equal((await check(code, true, 'src/example.scss')).code, code);
	});

	it('separates the nested rule and mixin call of a SCSS file', async () => {
		const fixed = await check('.a {\n\tcolor: red;\n\t&:hover {\n\t\tcolor: blue;\n\t}\n\t@include focus;\n}\n', true, 'src/example.scss');
		assert.equal(fixed.code, '.a {\n\tcolor: red;\n\n\t&:hover {\n\t\tcolor: blue;\n\t}\n\n\t@include focus;\n}\n');
	});

	it('keeps an @else beside the block of its @if', async () => {
		const code = '.a {\n\t@if $dark {\n\t\tcolor: white;\n\t} @else {\n\t\tcolor: black;\n\t}\n}\n';
		const stacked = '.a {\n\t@if $dark {\n\t\tcolor: white;\n\t}\n\t@else {\n\t\tcolor: black;\n\t}\n}\n';

		assert.equal((await check(code, true, 'src/example.scss')).code, code);
		assert.equal((await check(stacked, true, 'src/example.scss')).code, stacked);
	});

	it('reads a // comment of a SCSS file as a comment', async () => {
		const code = '.a {\n\t// First.\n\tcolor: red;\n}\n';
		assert.equal((await check(code, true, 'src/example.scss')).code, code);
	});

	it('reads a $variable beside a // comment of a SCSS file', async () => {
		const code = '// Spacing.\n$gap: 4px;\n\n.a {\n\tmargin: $gap;\n}\n';
		assert.equal((await check(code, true, 'src/example.scss')).code, code);
	});

	it('keeps a CRLF line ending', async () => {
		const fixed = await check('.a {\r\n\tcolor: red;\r\n\t&:hover {\r\n\t\tcolor: blue;\r\n\t}\r\n}\r\n', true, 'src/example.scss');
		assert.equal(fixed.code, '.a {\r\n\tcolor: red;\r\n\r\n\t&:hover {\r\n\t\tcolor: blue;\r\n\t}\r\n}\r\n');
	});
});
