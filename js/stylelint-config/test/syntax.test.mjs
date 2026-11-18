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
			['stanza-@{interpolation}', '${border}'],
			['color', 'red'],
			['stanza-@{interpolation}', '${tail}'],
		]);

		const rule = template.first;

		assert.equal(rule.first.prop, 'stanza-@{interpolation}');
		assert.equal(rule.first.source.start.line, 4);
		assert.equal(rule.last.prop, 'stanza-@{interpolation}');
	});

	it('leaves an interpolation inside a value or a selector as part of it', () => {
		const source = `${react}const x = css\`\n\${selector}:hover {\n margin: \${gap}px;\n}\n\`;\n`;
		const [template] = syntax.parse(source, { from: 'src/example.tsx' }).nodes;

		assert.deepEqual(readDeclarations(template), [['margin', '${gap}px']]);
	});

	it('picks a marker character the source does not hold', () => {
		const marker = `/*${String.fromCharCode(0xe000)}*/ css`;
		const source = `${react}const label = "${marker}";\nconst spin = keyframes\`\nfrom {\n opacity: 0;\n}\nto {\n opacity: 1;\n}\n\`;\n`;

		assert.equal(roundTrip(source), source);
	});

	it('writes a tag back as it was spelled, an escaped spelling too', () => {
		const backslash = String.fromCharCode(0x5c);
		const escaped = `key${backslash}u0066rames`;
		const braced = `keyfr${backslash}u{61}mes`;
		const body = '`\nfrom {\n opacity: 0;\n}\nto {\n opacity: ${end};\n}\n`';
		const source = `${react}const a = ${escaped}${body};\nconst b = keyframes${body};\nconst c = ${braced}${body};\nconst d = ${escaped}${body};\n`;

		assert.equal(roundTrip(source), source);

		for (const template of syntax.parse(source, { from: 'src/example.tsx' }).nodes) {
			assert.equal(template.nodes.length, 2);
		}
	});

	it('writes a standalone interpolation from its source, whatever became of its value', () => {
		const source = `${react}const x = css\`\n\${ base( 1 ) }\ncolor: red;\n\`;\n`;
		const document = syntax.parse(source, { from: 'src/example.tsx' });

		document.walkDecls((declaration) => {
			if (declaration.prop === 'stanza-@{interpolation}') {
				declaration.value = '${changed}';
			}
		});

		let output = '';

		syntax.stringify(document, (part) => {
			output += part;
		});

		assert.equal(output, source);
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
