import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import syntax from '../syntax.mjs';

import { check } from './helpers.mjs';

const react = 'import { css, keyframes } from "@emotion/react";\n';

/** Parses a source text as the file `from` and writes the document back. */
function roundTrip(source, from = 'src/example.tsx') {
	let output = '';

	syntax.stringify(syntax.parse(source, { from }), (part) => {
		output += part;
	});

	return output;
}

/** Collects the property and value of each declaration of a template, depth first. */
function readDeclarations(document) {
	const found = [];
	document.walkDecls((declaration) => found.push([declaration.prop, declaration.value]));

	return found;
}

describe('the syntax', () => {
	it('writes back a source it did not change', () => {
		const sources = [
			'const x = 1;\n',
			`${react}const x = css\`\ncolor: red;\n&:hover {\n color: \${color};\n}\n\`;\n`,
			`${react}const x = css\`\n\${base}\n.a {\n \${nested}\n color: red;\n \${tail}\n}\n\${last}\n\`;\n`,
			`${react}const spin = keyframes\`\nfrom {\n opacity: 0;\n}\nto {\n opacity: \${end};\n}\n\`;\n`,
			`${react}const x = css\`\nanimation: \${keyframes /* steps */ \`\nfrom {\n opacity: 0;\n}\n\`} 1s;\n\`;\n`,
			`${react}const x = css\`\r\n\${base}\r\ncolor: red;\r\n\`;\r\n`,
		];

		for (const source of sources) {
			assert.equal(roundTrip(source), source);
		}
	});

	it('exposes a standalone interpolation as the declaration it stands beside', () => {
		const source = `${react}const x = css\`\n&.error {\n \${border}\n .label {\n  color: red;\n }\n \${tail}\n}\n\`;\n`;
		const [template] = syntax.parse(source, { from: 'src/example.tsx' }).nodes;

		assert.deepEqual(readDeclarations(template), [
			['stanza-interpolation', '${border}'],
			['color', 'red'],
			['stanza-interpolation', '${tail}'],
		]);

		const rule = template.first;

		assert.equal(rule.first.prop, 'stanza-interpolation');
		assert.equal(rule.first.source.start.line, 4);
		assert.equal(rule.last.prop, 'stanza-interpolation');
	});

	it('leaves an interpolation inside a value or a selector as part of it', () => {
		const source = `${react}const x = css\`\n\${selector}:hover {\n margin: \${gap}px;\n}\n\`;\n`;
		const [template] = syntax.parse(source, { from: 'src/example.tsx' }).nodes;

		assert.deepEqual(readDeclarations(template), [['margin', '${gap}px']]);
	});

	it('picks a marker character the source does not hold', () => {
		const taken = String.fromCharCode(0xe000);
		const source = `${react}const label = "${taken}";\nconst spin = keyframes\`\nfrom {\n opacity: 0;\n}\nto {\n opacity: 1;\n}\n\`;\n`;

		assert.equal(roundTrip(source), source);
	});

	it('finds the keyframes template whatever the file extension', () => {
		const source = `${react}const spin = keyframes\`\nfrom {\n opacity: 0;\n}\nto {\n opacity: 1;\n}\n\`;\n`;

		for (const extension of ['js', 'jsx', 'ts', 'tsx', 'mjs', 'mts']) {
			const [template] = syntax.parse(source, { from: `src/example.${extension}` }).nodes;
			assert.equal(template.nodes.length, 2, extension);
		}
	});

	it('finds the templates of a source parsed with no file name, and of one linted as a .tsx file', async () => {
		const source = `${react}const spin = keyframes\`\nfrom {\n opacity: 0;\n}\nto {\n opacity: 1;\n}\n\`;\n`;
		const [template] = syntax.parse(source).nodes;

		assert.equal(template.nodes.length, 2);
		assert.equal((await check(source, true)).code, source.replace('}\nto', '}\n\nto'));
	});
});
