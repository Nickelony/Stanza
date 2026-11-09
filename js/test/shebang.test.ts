import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { Linter, RuleTester } from 'eslint';
import tseslint from 'typescript-eslint';

import plugin from '../src/index.ts';
import commentedSiblings from '../src/rules/separate-commented-siblings.ts';
import directives from '../src/rules/separate-directives.ts';
import importSection from '../src/rules/separate-import-section.ts';

import { crlf, lines, readCorpus, readPresetRules } from './helpers.ts';

/** The default parser of ESLint, which keeps the shebang as a comment where the TypeScript parser drops it. */
const tester = new RuleTester({ languageOptions: { ecmaVersion: 'latest', sourceType: 'module' } });

tester.run('separate-directives with the default parser', directives as never, {
	valid: [
		{ code: lines('#!/usr/bin/env node', '', 'run();') },
		{ code: lines('#!/usr/bin/env node', '', "'use strict';", '', 'run();') },
		{ code: '#!/usr/bin/env node' },
		{ code: lines('#!/usr/bin/env node', '// only a comment') },
	],

	invalid: [
		{
			code: lines('#!/usr/bin/env node', 'run();'),
			output: lines('#!/usr/bin/env node', '', 'run();'),
			errors: 1,
		},

		{
			code: lines('#!/usr/bin/env node', "'use strict';", 'run();'),
			output: lines('#!/usr/bin/env node', '', "'use strict';", '', 'run();'),
			errors: 2,
		},

		{
			code: lines('#!/usr/bin/env node', '// the entry', 'run();'),
			output: lines('#!/usr/bin/env node', '', '// the entry', 'run();'),
			errors: 1,
		},

		{
			code: crlf(lines('#!/usr/bin/env node', 'run();')),
			output: crlf(lines('#!/usr/bin/env node', '', 'run();')),
			errors: 1,
		},
	],
});

tester.run('separate-import-section with the default parser', importSection as never, {
	valid: [{ code: lines('#!/usr/bin/env node', '', "import a from 'a';", '', 'run(a);') }],

	invalid: [
		{
			code: lines('#!/usr/bin/env node', '', "import a from 'a';", 'run(a);'),
			output: lines('#!/usr/bin/env node', '', "import a from 'a';", '', 'run(a);'),
			errors: 1,
		},
	],
});

tester.run('separate-commented-siblings with the default parser', commentedSiblings as never, {
	valid: [{ code: lines('#!/usr/bin/env node', '', '// the entry', 'run();') }],

	invalid: [
		{
			code: lines('#!/usr/bin/env node', '', 'prepare();', '// the entry', 'run();'),
			output: lines('#!/usr/bin/env node', '', 'prepare();', '', '// the entry', 'run();'),
			errors: 1,
		},
	],
});

describe('a file with a shebang', () => {
	/** Fixes a source with a preset, with the default parser or with the TypeScript parser. */
	function fixWith(code: string, level: 'minimal' | 'moderate' | 'strong', parser?: Linter.Parser): string {
		const rules = Object.fromEntries(Object.entries(readPresetRules(level)).map(([name, entry]) => [`stanza/${name}`, entry]));

		const config: Linter.Config[] = [
			{
				files: ['**/*.js'],
				languageOptions: { ecmaVersion: 'latest', sourceType: 'commonjs', ...(parser ? { parser } : {}) },
				plugins: { stanza: plugin as never },
				rules,
			},
		];

		return new Linter().verifyAndFix(code, config, { filename: 'cli.js' }).output;
	}

	for (const level of ['minimal', 'moderate', 'strong'] as const) {
		it(`is fixed alike by the default parser and the TypeScript parser at ${level}`, () => {
			const script = readCorpus().find((file) => file.name === 'cli.js')?.text ?? '';

			// The blank lines are dropped, so there is something to fix.
			const packed = lines(...script.split(String.fromCharCode(10)).filter((line) => line !== ''));

			assert.ok(packed.startsWith('#!'), 'the corpus has a script with a shebang');
			assert.notEqual(fixWith(packed, level), packed);
			assert.equal(fixWith(packed, level), fixWith(packed, level, tseslint.parser));
		});
	}
});
