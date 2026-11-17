import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import stylelint from 'stylelint';

import configuration from '../index.mjs';

import { check } from './helpers.mjs';

const react = 'import { css, keyframes } from "@emotion/react";\n';

/** The autofixable core rules that read a declaration, with an option set that turns each one on. */
const declarationFixers = {
	'alpha-value-notation': 'percentage',
	'color-function-alias-notation': 'with-alpha',
	'color-function-notation': 'modern',
	'color-hex-length': 'short',
	'comment-whitespace-inside': 'always',
	'custom-property-empty-line-before': 'always',
	'declaration-block-no-duplicate-properties': [true, { ignore: ['consecutive-duplicates-with-different-values'] }],
	'declaration-block-no-redundant-longhand-properties': true,
	'declaration-property-value-keyword-no-deprecated': true,
	'display-notation': 'multi-keyword',
	'font-family-name-quotes': 'always-where-recommended',
	'font-weight-notation': 'numeric',
	'function-calc-no-unspaced-operator': true,
	'function-name-case': 'lower',
	'function-url-quotes': 'always',
	'hue-degree-notation': 'angle',
	'length-zero-no-unit': true,
	'lightness-notation': 'percentage',
	'property-layout-mappings': 'flow-relative',
	'property-no-deprecated': true,
	'property-no-vendor-prefix': true,
	'shorthand-property-no-redundant-values': true,
	'unit-layout-mappings': 'flow-relative',
	'value-keyword-case': 'lower',
	'value-keyword-layout-mappings': 'flow-relative',
	'value-no-vendor-prefix': true,
};

/** Every semantic rule that judges a property, besides the autofixable ones. */
const declarationChecks = { 'property-no-unknown': true, 'declaration-block-no-shorthand-property-overrides': true };

/** A template with a standalone interpolation in every position a declaration can stand, beside declarations the fixers rewrite. */
const source = `${react}const x = css\`
\${baseStyles}
color: #ffffff;
margin: 0px 0px;
\${moreStyles}
color: #ffffff;
&:hover {
  \${hover}
  color: RED;
  \${\`nested \${inner}\`}
}
\${tail}
\`;
`;

const interpolations = ['${baseStyles}', '${moreStyles}', '${hover}', '${`nested ${inner}`}', '${tail}'];

/** Lints a source with the config and every rule of the lists, and writes the fixes. */
async function lintWithAllRules(code, fix) {
	return stylelint.lint({
		code,
		codeFilename: 'src/example.tsx',
		config: { ...configuration, rules: { ...configuration.rules, ...declarationFixers, ...declarationChecks } },
		fix,
	});
}

describe('the syntax beside the other rules of Stylelint', () => {
	it('keeps every interpolation through the fixes of every declaration rule', async () => {
		const result = await lintWithAllRules(source, true);

		for (const interpolation of interpolations) {
			assert.ok(result.code.includes(interpolation), `${interpolation} survives in ${result.code}`);
		}

		assert.equal(result.code.split('${').length, source.split('${').length);
	});

	it('reports no interpolation as a declaration of a rule that reads a property', async () => {
		for (const fix of [false, true]) {
			const result = await lintWithAllRules(source, fix);
			const warnings = result.results.flatMap((item) => item.warnings);

			assert.deepEqual(
				warnings.filter((warning) => warning.text.includes('stanza')),
				[],
			);
		}
	});

	it('does not delete an interpolation that repeats beside a duplicate-property rule', async () => {
		const code = `${react}const x = css\`\n\${baseStyles}\ncolor: red;\n\${moreStyles}\n\`;\n`;
		const result = await check(code, true);

		assert.equal(result.code, code);

		const strict = await stylelint.lint({
			code,
			codeFilename: 'src/example.tsx',

			config: {
				...configuration,

				rules: {
					...configuration.rules,
					'declaration-block-no-duplicate-properties': [true, { ignore: ['consecutive-duplicates-with-different-values'] }],
					'property-no-unknown': true,
				},
			},

			fix: true,
		});

		assert.equal(strict.code, code);
		assert.deepEqual(strict.results[0].warnings, []);
	});

	it('writes an interpolation from its source, whatever a fixer made of its value', async () => {
		const code = `${react}const x = css\`\n\${ "#FFFFFF" }\ncolor: #ffffff;\n\`;\n`;
		const result = await lintWithAllRules(code, true);

		assert.ok(result.code.includes('${ "#FFFFFF" }'), result.code);
	});
});
