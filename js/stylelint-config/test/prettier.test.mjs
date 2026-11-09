import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import prettier from 'prettier';

import { check, resolvePrettierOptions } from './helpers.mjs';

/** Formats a source text as TypeScript with the options of the package. */
async function format(source) {
	return prettier.format(source, { ...(await resolvePrettierOptions()), parser: 'typescript' });
}

describe('Prettier', () => {
	it('indents typed Emotion interpolations and CSS declarations, and formats the result as it is', async () => {
		const source = [
			'import styled from "@emotion/styled";',
			'const Container = styled("div")<{ $isOpen: boolean }>`',
			"${(props) => props.$isOpen && 'display: flex;'}",
			'    align-items: center;',
			'`;',
		].join('\n');

		const formatted = await format(source);

		assert.match(formatted, /`\n\t\$\{/);
		assert.match(formatted, /\n\talign-items: center;/);
		assert.equal(await format(formatted), formatted);
	});

	it('keeps the blank lines the fix writes between the rules of a template', async () => {
		const source = 'import { css } from "@emotion/react";\nconst x = css`\ncolor: red;\n&:hover {\ncolor: blue;\n}\nmargin: 0;\n`;';
		const formatted = await format(source);
		const fixed = (await check(formatted, true)).code;

		assert.notEqual(fixed, formatted);
		assert.match(fixed, /color: red;\n\n\t&:hover \{/);
		assert.match(fixed, /\}\n\n\tmargin: 0;/);
		assert.equal(await format(fixed), fixed);
		assert.equal((await check(fixed)).errored, false);
	});

	it('leaves a keyframes template as written, so the blank line the fix writes stays', async () => {
		const source = 'import { keyframes } from "@emotion/react";\nconst spin = keyframes`\nfrom {\nopacity: 0;\n}\nto {\nopacity: 1;\n}\n`;';
		const formatted = await format(source);
		const fixed = (await check(formatted, true)).code;

		assert.match(formatted, /keyframes`\nfrom \{\nopacity: 0;/);
		assert.notEqual(fixed, formatted);
		assert.match(fixed, /\}\n\nto \{/);
		assert.equal(await format(fixed), fixed);
		assert.equal((await check(fixed, true)).code, fixed);
	});

	it('does not format the template of a renamed tag, which the syntax leaves alone too', async () => {
		const source = 'import { css as styles } from "@emotion/react";\nconst x = styles`\n      color: red;\n`;';
		assert.match(await format(source), /styles`\n {6}color: red;\n`;/);
	});
});
