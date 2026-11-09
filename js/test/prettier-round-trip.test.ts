import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import type { Linter } from 'eslint';

import { fix, fixWithPreset, formatWithPrettier, lines, lintWithPreset, presetRuns, readCorpus, readShape } from './helpers.ts';

/** Drops every second blank line, so the round trip also starts from sources a team would not format. */
function dropEveryOtherBlankLine(text: string): string {
	let seen = 0;

	return text
		.split('\n')
		.filter((line) => line.trim() !== '' || (seen += 1) % 2 === 0)
		.join('\n');
}

for (const [name, level, withOptIn] of presetRuns) {
	describe(`the Prettier round trip at the ${name}`, () => {
		for (const file of readCorpus()) {
			it(`leaves the fixed ${file.name} as Prettier writes it, and the same code`, async () => {
				const formatted = await formatWithPrettier(file.text, file.path);

				for (const input of [formatted, await formatWithPrettier(dropEveryOtherBlankLine(formatted), file.path)]) {
					const fixed = fixWithPreset(input, level, file.name, withOptIn);
					const reformatted = await formatWithPrettier(fixed, file.path);

					assert.equal(reformatted, fixed, 'Prettier rewrites the fixed text');
					assert.deepEqual(lintWithPreset(reformatted, level, file.name, withOptIn), [], 'the rules report on the text Prettier writes');
					assert.equal(readShape(fixed), readShape(input), 'the fix changed code and not only blank lines');
				}
			});
		}
	});
}

describe('the lists', () => {
	const samples: Record<string, string> = {
		'object members': lines('const o = {', '\ta: 1,', '', '\tb: {', '\t\tc: 1,', '\t},', '', '\td: 2,', '};'),
		'array elements': lines('const a = [', '\t{', '\t\tb: 1,', '\t},', '', '\t{', '\t\tc: 1,', '\t},', '];'),
		arguments: lines('run(', '\ta,', '', '\t() => {', '\t\tgo();', '\t},', '', '\tb,', ');'),
		parameters: lines('function f(', '\ta: string,', '', '\tb: {', '\t\tc: number;', '\t},', '', '\td: number,', ') {}'),
	};

	for (const [name, code] of Object.entries(samples)) {
		it(`keep the blank line that separates the multi-line ${name}, so the separating rule and Prettier agree`, async () => {
			assert.equal(await formatWithPrettier(code, 'file.ts'), code);
			assert.deepEqual(lintWithPreset(code, 'strong', 'file.ts'), []);
		});
	}

	it('keep the blank line between enum members, which the tight rule removes', async () => {
		const code = lines('enum E {', '	A = 1,', '', '	B = 2,', '}');

		assert.equal(await formatWithPrettier(code, 'file.ts'), code);
		assert.equal(fix(code, { 'keep-single-line-elements-together': 'error' }, {}, 'file.ts'), lines('enum E {', '	A = 1,', '	B = 2,', '}'));
	});

	it('join what the tight rule leaves once the list fits on one line, and Prettier writes no blank line back', async () => {
		const code = lines('run(', '\ta,', '', '\tb,', ');');
		const fixed = fix(code, { 'keep-single-line-elements-together': 'error' }, {}, 'file.ts');
		const formatted = await formatWithPrettier(fixed, 'file.ts');

		assert.equal(fixed, lines('run(', '\ta,', '\tb,', ');'));
		assert.equal(formatted, lines('run(a, b);'));
		assert.deepEqual(lintWithPreset(formatted, 'strong', 'file.ts'), []);
	});

	it('lose the blank line between declarators in Prettier', async () => {
		const code = lines('const a = 1,', '', '\tb = {', '\t\tc: 2,', '\t},', '', '\td = 3;');
		assert.equal(await formatWithPrettier(code, 'file.ts'), lines('const a = 1,', '\tb = {', '\t\tc: 2,', '\t},', '\td = 3;'));
	});
});

describe('the JSX children', () => {
	const source = lines(
		'export const view = (',
		'\t<section>',
		'\t\t<Header />',
		'\t\t<Main>',
		'\t\t\t<Item />',
		'\t\t</Main>',
		'\t\t{/* The footer. */}',
		'\t\t<Footer />',
		'\t</section>',
		');',
	);

	it('keep the blank lines the rule writes at the strong level, and the same code', async () => {
		const formatted = await formatWithPrettier(source, 'file.tsx');
		const fixed = fixWithPreset(formatted, 'strong', 'file.tsx');

		assert.notEqual(fixed, formatted);
		assert.equal(await formatWithPrettier(fixed, 'file.tsx'), fixed);
		assert.deepEqual(lintWithPreset(fixed, 'strong', 'file.tsx'), []);
		assert.equal(readShape(fixed), readShape(formatted));
		assert.equal(fixWithPreset(fixed, 'strong', 'file.tsx'), fixed);
	});

	it('stay joined in an element with text, where Prettier removes every blank line', async () => {
		const mixed = lines(
			'export const view = (',
			'\t<section>',
			'\t\t<Header />',
			'\t\t<Main>',
			'\t\t\t<Item />',
			'\t\t</Main>',
			'\t\tText <b>here</b> and <i>there</i>.',
			'\t</section>',
			');',
		);

		assert.equal(await formatWithPrettier(mixed, 'file.tsx'), mixed);
		assert.equal(fixWithPreset(mixed, 'strong', 'file.tsx'), mixed);

		const spaced = lines(
			'export const view = (',
			'\t<section>',
			'\t\t<Header />',
			'',
			'\t\t<Main>',
			'\t\t\t<Item />',
			'\t\t</Main>',
			'\t\tText <b>here</b> and <i>there</i>.',
			'\t</section>',
			');',
		);

		assert.equal(await formatWithPrettier(spaced, 'file.tsx'), mixed);
	});
});

describe('the import groups', () => {
	const tight = lines(
		"import './polyfills.ts';",
		"import { readFile } from 'node:fs/promises';",
		"import { parse } from './parse.ts';",
		'',
		'run(parse, readFile);',
	);

	const apart = lines(
		"import './polyfills.ts';",
		'',
		"import { readFile } from 'node:fs/promises';",
		'',
		"import { parse } from './parse.ts';",
		'',
		'run(parse, readFile);',
	);

	/** The order of the repository plus a group for the type imports, written between two blank lines. */
	const typesOrder = ['<BUILTIN_MODULES>', '', '<THIRD_PARTY_MODULES>', '', '<TYPES>', '', '^[.]'];

	it('are separated from the side-effect imports with the blank lines that the sort plugin writes', async () => {
		const formatted = await formatWithPrettier(tight, 'file.ts');

		assert.equal(formatted, apart);
		assert.deepEqual(lintWithPreset(formatted, 'strong', 'file.ts'), []);

		assert.equal(
			fixWithPreset(tight, 'strong', 'file.ts'),

			lines(
				"import './polyfills.ts';",
				'',
				"import { readFile } from 'node:fs/promises';",
				"import { parse } from './parse.ts';",
				'',
				'run(parse, readFile);',
			),
		);

		assert.equal(await formatWithPrettier(fixWithPreset(tight, 'strong', 'file.ts'), 'file.ts'), apart);
	});

	it('settle with the type group of the sort plugin when the option separates the type imports', async () => {
		const mixed = lines("import type { Order } from './order.ts';", "import { parse } from './parse.ts';", '', 'run(parse);');
		const rules: Record<string, Linter.RuleEntry> = { 'separate-import-groups': ['error', { typeImports: true }] };
		const formatted = await formatWithPrettier(mixed, 'file.ts', { importOrder: typesOrder });

		assert.equal(formatted, lines("import type { Order } from './order.ts';", '', "import { parse } from './parse.ts';", '', 'run(parse);'));
		assert.equal(fix(formatted, rules), formatted);
		assert.equal(fix(mixed, rules), formatted);
		assert.equal(await formatWithPrettier(fix(mixed, rules), 'file.ts', { importOrder: typesOrder }), formatted);
	});

	it('conflict with the sort plugin when no group separates the type imports, which is why the option is off', async () => {
		const mixed = lines("import type { Order } from './order.ts';", "import { parse } from './parse.ts';", '', 'run(parse);');
		const rules: Record<string, Linter.RuleEntry> = { 'separate-import-groups': ['error', { typeImports: true }] };
		const fixed = fix(mixed, rules);

		assert.equal(fixed, lines("import type { Order } from './order.ts';", '', "import { parse } from './parse.ts';", '', 'run(parse);'));
		assert.equal(await formatWithPrettier(fixed, 'file.ts'), mixed);
		assert.deepEqual(lintWithPreset(mixed, 'strong', 'file.ts'), []);
	});
});

describe('the JSX child rules', () => {
	const file = 'file.tsx';

	const source = lines(
		'export const view = (',
		'\t<section>',
		'\t\t<A />',
		'',
		'\t\t<B />',
		'\t\t{/* the body */}',
		'\t\t<C />',
		'\t\t<D>',
		'\t\t\t<E />',
		'',
		'\t\t\t<F />',
		'\t\t</D>',
		'\t\t<G />',
		'\t</section>',
		');',
	);

	it('write blank lines that Prettier keeps and remove the ones it leaves, in every level that holds the rules', async () => {
		const formatted = await formatWithPrettier(source, file);

		for (const withOptIn of [false, true]) {
			const fixed = fixWithPreset(formatted, 'strong', file, withOptIn);

			assert.notEqual(fixed, formatted);
			assert.equal(await formatWithPrettier(fixed, file), fixed);
			assert.deepEqual(lintWithPreset(fixed, 'strong', file, withOptIn), []);
			assert.equal(readShape(fixed), readShape(formatted));
			assert.equal(fixWithPreset(fixed, 'strong', file, withOptIn), fixed);
		}

		assert.equal(
			fixWithPreset(formatted, 'strong', file),

			lines(
				'export const view = (',
				'\t<section>',
				'\t\t<A />',
				'\t\t<B />',
				'',
				'\t\t{/* the body */}',
				'\t\t<C />',
				'',
				'\t\t<D>',
				'\t\t\t<E />',
				'\t\t\t<F />',
				'\t\t</D>',
				'',
				'\t\t<G />',
				'\t</section>',
				');',
			),
		);
	});

	it('leave an element with a space as written, because Prettier joins the space to the element above it', async () => {
		const spaced = lines('export const view = (', '\t<div>', '\t\t<A />', "\t\t{' '}", '\t\t<B>', '\t\t\t<C />', '\t\t</B>', '\t</div>', ');');
		const formatted = await formatWithPrettier(spaced, file);

		assert.equal(formatted, lines('export const view = (', '\t<div>', "\t\t<A />{' '}", '\t\t<B>', '\t\t\t<C />', '\t\t</B>', '\t</div>', ');'));
		assert.equal(fixWithPreset(spaced, 'strong', file, true), spaced);
		assert.equal(fixWithPreset(formatted, 'strong', file, true), formatted);
	});
});
