import assert from 'node:assert/strict';
import { test } from 'node:test';

import prettier from 'prettier';

import { check, resolvePrettierOptions } from './helpers.mjs';

test('separates nested selectors from declarations and the declarations following a block', async () => {
	const code =
		'import styled from "@emotion/styled";\nconst X = styled.div`\nmargin-top: auto;\npadding-bottom: 20px;\nli {\n padding-bottom: 10px;\n}\ncolor: red;\n`;';

	const before = await check(code);

	assert.equal(before.errored, true);
	assert.deepEqual(new Set(before.results[0].warnings.map(({ rule }) => rule)), new Set(['rule-empty-line-before', 'declaration-empty-line-before']));

	const fixed = await check(code, true);

	assert.equal(fixed.errored, false);
	assert.match(fixed.code, /padding-bottom: 20px;\n\s*\nli/);
	assert.match(fixed.code, /}\n\s*\ncolor: red/);
	assert.equal((await check(fixed.code)).errored, false);
});

test('separates media blocks without padding their first declaration', async () => {
	const code = 'import { css } from "@emotion/react";\nconst x = css`\ndisplay: flex;\n@media (min-width: 600px) {\n gap: 8px;\n}\npadding: 4px;\n`;';
	const fixed = await check(code, true);

	assert.equal(fixed.errored, false);
	assert.match(fixed.code, /display: flex;\n\s*\n@media/);
	assert.match(fixed.code, /600px\) {\n gap/);
	assert.match(fixed.code, /}\n\s*\npadding: 4px/);
});

test('separates a selector from a preceding standalone interpolation without flattening its stanza', async () => {
	const code =
		'import { css } from "@emotion/react";\nconst x = css`\n&.error {\n ${getBorderStyle(props.noBorder, colors.ERROR)}\n .input-label, .unit-display {\n  color: ${colors.ERROR};\n }\n}\n`;';

	const expected = code.replace('ERROR)}\n .input-label', 'ERROR)}\n\n .input-label');

	assert.equal((await check(code)).errored, true);
	assert.equal((await check(code, true)).code, expected);
	assert.equal((await check(expected)).errored, false);
	assert.equal((await check(expected, true)).code, expected);
});

test('does not count blank lines inside an interpolation as CSS stanza spacing', async () => {
	const code =
		'import styled from "@emotion/styled";\nconst X = styled.div`\n${(props) => {\n const border = createBorder(props);\n\n return border;\n}}\n.label {\n color: red;\n}\n`;';

	const expected = code.replace('}}\n.label', '}}\n\n.label');

	assert.equal((await check(code)).errored, true);
	assert.equal((await check(code, true)).code, expected);
	assert.equal((await check(expected)).errored, false);
});

test('keeps inline property and selector interpolations intact and the first CSS child unpadded', async () => {
	const code = 'import { css } from "@emotion/react";\nconst x = css`\n${selector}:hover {\n margin: ${gap}px;\n color: ${color};\n}\n`;';

	assert.equal((await check(code, true)).code, code);
	assert.equal((await check(code)).errored, false);
});

test('separates media blocks and a trailing helper without padding the scope', async () => {
	const code =
		'import { css } from "@emotion/react";\nconst x = css`\n${baseStyles}\n@media (min-width: 600px) {\n ${responsiveStyles}\n .label {\n  color: red;\n }\n ${tailStyles}\n}\n`;';

	const expected = code
		.replace('baseStyles}\n@media', 'baseStyles}\n\n@media')
		.replace('responsiveStyles}\n .label', 'responsiveStyles}\n\n .label')
		.replace('}\n ${tailStyles}', '}\n\n ${tailStyles}');

	assert.equal((await check(code, true)).code, expected);
	assert.equal((await check(expected)).errored, false);
	assert.equal((await check(expected, true)).code, expected);
});

test('spaces a helper that ends the template below a block', async () => {
	const code = 'import { css } from "@emotion/react";\nconst x = css`\n.label {\n color: red;\n}\n${tail}\n`;';
	const expected = code.replace('}\n${tail}', '}\n\n${tail}');

	assert.equal((await check(code, true)).code, expected);
	assert.equal((await check(expected)).errored, false);
	assert.equal((await check(expected, true)).code, expected);
});

test('leaves a trailing helper below a declaration as written and a lone helper tight', async () => {
	const tight = 'import { css } from "@emotion/react";\nconst x = css`\n.label {\n color: red;\n ${tail}\n}\n.only {\n ${tail}\n}\n`;';
	const apart = tight.replace('red;\n ${tail}', 'red;\n\n ${tail}');

	for (const code of [tight, apart]) {
		const expected = code.replace('}\n.only', '}\n\n.only');

		assert.equal((await check(code, true)).code, expected);
		assert.equal((await check(expected)).errored, false);
		assert.equal((await check(expected, true)).code, expected);
	}
});

test('fixes nested css inside a helper without rewriting an identical ordinary JavaScript template', async () => {
	const body = '\nli {\n padding: 4px;\n}\na {\n margin: 0;\n}\n';

	const code =
		'import { css } from "@emotion/react";\nconst x = css`\n${() => {\n const label = `' + body + '`;\n\n return css`' + body + '`;\n}}\n`;';

	const expected = code.replace('return css`' + body, 'return css`' + body.replace('}\na', '}\n\na'));

	assert.equal((await check(code, true)).code, expected);
	assert.equal((await check(expected)).errored, false);
});

test("fixes nested CSS inside a block's trailing interpolation at its original source range", async () => {
	const code =
		'import { css } from "@emotion/react";\nconst x = css`\n.host {\n &:hover {\n  color: red;\n }\n ${() => css`\nli {\n padding: 4px;\n}\na {\n margin: 0;\n}\n`}\n}\n`;';

	const expected = code.replace('}\na', '}\n\na').replace('}\n ${() =>', '}\n\n ${() =>');

	assert.equal((await check(code, true)).code, expected);
	assert.equal((await check(expected)).errored, false);
	assert.equal((await check(expected, true)).code, expected);
});

test('preserves attached comments and existing helper/declaration groups', async () => {
	const code =
		'import { css } from "@emotion/react";\nconst x = css`\n${baseStyles}\n\n${borderStyles}\n\ncolor: red;\n\n${hoverStyles}\n\n/* Interactive state. */\n&:hover {\n color: blue;\n}\n`;';

	assert.equal((await check(code, true)).code, code);
	assert.equal((await check(code)).errored, false);
});

test('preserves CRLF, Unicode and blank lines inside JavaScript callbacks', async () => {
	const code =
		'import { css } from "@emotion/react";\nconst label = "🪑";\nconst x = css`\n&:hover {\n ${() => {\n  const border = createBorder(label);\n\n  return border;\n }}\n .label {\n  color: red;\n }\n}\n`;';

	const expected = code.replace('}}\n .label', '}}\n\n .label').replaceAll('\n', '\r\n');

	assert.equal((await check(code.replaceAll('\n', '\r\n'), true)).code, expected);
	assert.equal((await check(expected)).errored, false);
});

test('Prettier and Stylelint agree on interpolation stanza boundaries across repeated formatting', async () => {
	const code =
		'import { css } from "@emotion/react";\nconst x = css`\n&.error {\n ${getBorderStyle(props.noBorder, colors.ERROR)}\n .input-label, .unit-display {\n  color: ${colors.ERROR};\n }\n}\n`;';

	const options = { ...(await resolvePrettierOptions()), parser: 'typescript' };
	const formatted = await prettier.format(code, options);
	const fixed = (await check(formatted, true)).code;

	assert.notEqual(fixed, formatted);
	assert.equal(await prettier.format(fixed, options), fixed);
	assert.equal((await check(fixed, true)).code, fixed);
	assert.equal((await check(fixed)).errored, false);
});

test('preserves property order, existing declaration groups and complex Emotion interpolations', async () => {
	const code =
		'import styled from "@emotion/styled";\nimport { css } from "@emotion/react";\nconst X = styled("div")<{ $isOpen: boolean }>`\n${applyBaseStyles}\ndisplay: flex;\nalign-items: center;\n\ncolor: ${({ $isOpen }) => ($isOpen ? "red" : "blue")};\n${({ $isOpen }) => $isOpen && css`\nli {\n padding: 4px;\n}\na {\n margin: 0;\n}\n`}\nli {\n padding: 8px;\n}\n`;';

	const fixed = await check(code, true);

	assert.equal(fixed.errored, false);
	assert.match(fixed.code, /\$\{applyBaseStyles\}/);
	assert.match(fixed.code, /display: flex;\nalign-items: center;\n\ncolor:/);
	assert.ok(fixed.code.includes('color: ${({ $isOpen }) => ($isOpen ? "red" : "blue")};'));
	assert.match(fixed.code, /\$\{\(\{ \$isOpen \}\) => \$isOpen && css`/);
	assert.equal((await check(fixed.code)).errored, false);
});

test('keeps comments attached to their rule and ignores ordinary template strings', async () => {
	const code =
		'import { css } from "@emotion/react";\nconst label = `Keep\nthis text`;\nconst x = css`\ncolor: red;\n\n/* Interactive state. */\n&:hover {\n color: blue;\n}\n`;';

	const fixed = await check(code, true);

	assert.equal(fixed.errored, false);
	assert.match(fixed.code, /const label = `Keep\nthis text`;/);
	assert.match(fixed.code, /\/\* Interactive state\. \*\/\n&:hover/);
});

test('checks plain CSS and produces a stable combined Prettier/Stylelint result', async () => {
	const source = '.card {\ncolor: red;\nli {\npadding: 8px;\n}\n}';
	const options = await resolvePrettierOptions();
	const formatted = await prettier.format(source, { ...options, parser: 'css' });
	const fixed = await check(formatted, true, 'src/example.css');

	assert.equal(fixed.errored, false);
	assert.match(fixed.code, /color: red;\n\s*\n\tli/);
	assert.equal(await prettier.format(fixed.code, { ...options, parser: 'css' }), fixed.code);
	assert.equal((await check(fixed.code, false, 'src/example.css')).errored, false);
});

test('keeps consecutive blockless at-rules compact', async () => {
	const code = '@import "base.css";\n@import "theme.css";\n';
	const fixed = await check(code, true, 'src/example.css');

	assert.equal(fixed.errored, false);
	assert.equal(fixed.code, code);
});

test('checks Emotion keyframes and restores their JavaScript tags after fixing', async () => {
	const code = 'import { keyframes } from "@emotion/react";\nconst spin = keyframes`\nfrom {\n opacity: 0;\n}\nto {\n opacity: 1;\n}\n`;';
	const fixed = await check(code, true);

	assert.equal(fixed.errored, false);
	assert.match(fixed.code, /const spin = keyframes`/);
	assert.match(fixed.code, /}\n\s*\nto {/);
	assert.equal(fixed.code, code.replace('}\nto', '}\n\nto'));
	assert.equal((await check(fixed.code, true)).code, fixed.code);
});

test('does not treat shadowed or unrelated keyframes factories as Emotion', async () => {
	const code =
		'import { keyframes } from "@emotion/react";\nfunction label(keyframes) { return keyframes`Keep\nthis text`; }\nconst other = text`keyframes`;';

	const fixed = await check(code, true);

	assert.equal(fixed.errored, false);
	assert.equal(fixed.code, code);
});

test('preserves nested keyframes and interpolation expressions', async () => {
	const code =
		'import { css, keyframes } from "@emotion/react";\nconst x = css`\nanimation: ${keyframes`\nfrom {\n opacity: ${start};\n}\nto {\n opacity: 1;\n}\n`} 1s;\n`;';

	const fixed = await check(code, true);

	assert.equal(fixed.errored, false);
	assert.equal(fixed.code, code.replace('}\nto', '}\n\nto'));
});

test('inserts inline keyframes source literally when JavaScript contains replacement tokens', async () => {
	for (const token of ['$&', '$$', '$`', "$'", '$1', '$<name>']) {
		const code =
			'import { css, keyframes } from "@emotion/react";\nconst x = css`\nanimation: ${keyframes`\nfrom {\n opacity: ${value.replace(/x/g, ' +
			JSON.stringify(token) +
			')};\n}\nto {\n opacity: 1;\n}\n`} 1s;\n`;';

		const expected = code.replace('}\nto', '}\n\nto');
		const fixed = await check(code, true);

		assert.equal(fixed.code, expected, token);
		assert.equal((await check(expected)).errored, false, token);
		assert.equal((await check(expected, true)).code, expected, token);
	}
});

test('preserves whitespace and comments between an inline keyframes tag and its template', async () => {
	for (const trivia of [' ', '\n', ' /* animation steps $& $$ */ ', '\n/* animation steps */\n', ' // animation steps\n']) {
		const code =
			'import { css, keyframes } from "@emotion/react";\nconst x = css`\nanimation: ${keyframes' +
			trivia +
			'`\nfrom {\n opacity: 0;\n}\nto {\n opacity: 1;\n}\n`} 1s;\n`;';

		const expected = code.replace('}\nto', '}\n\nto');
		const fixed = await check(code, true);

		assert.equal(fixed.code, expected);
		assert.equal((await check(expected)).errored, false);
		assert.equal((await check(expected, true)).code, expected);
	}
});
