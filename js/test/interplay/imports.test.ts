import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { fix, fixWithPreset, lines, reporters, type Entries } from '../helpers.ts';

describe('separate-import-groups', () => {
	const code = lines("'use strict';", "import './setup.ts';", "import { a } from 'a';", "import b from './b.ts';", "import 'c';", 'run(a, b);');

	const expected = lines(
		"'use strict';",
		'',
		"import './setup.ts';",
		'',
		"import { a } from 'a';",
		"import b from './b.ts';",
		'',
		"import 'c';",
		'',
		'run(a, b);',
	);

	const names = ['separate-directives', 'separate-import-section', 'separate-import-groups', 'separate-multi-line-constructs'];

	it('asks for the gaps between the imports and leaves the gaps beside the directives and the code to the other rules', () => {
		assert.deepEqual(reporters(code, { 'separate-import-groups': 'error' }), ['stanza/separate-import-groups', 'stanza/separate-import-groups']);
		assert.deepEqual(reporters(code, { 'separate-import-section': 'error' }), ['stanza/separate-import-section']);
		assert.deepEqual(reporters(code, { 'separate-directives': 'error' }), ['stanza/separate-directives']);
	});

	it('writes one text in either order of the rules, for every subset of them, and a second fix changes nothing', () => {
		for (let mask = 1; mask < 2 ** names.length; mask += 1) {
			const entries = names.filter((_, index) => (mask >> index) % 2 === 1).map((name) => [name, 'error'] as const);
			const label = entries.map(([name]) => name).join(', ');
			const forward = Object.fromEntries(entries);
			const fixed = fix(code, forward);

			assert.equal(fix(code, Object.fromEntries(entries.toReversed())), fixed, label);
			assert.equal(fix(fixed, forward), fixed, label);
			assert.deepEqual(reporters(fixed, forward), [], label);
		}

		assert.equal(fix(code, Object.fromEntries(names.map((name) => [name, 'error'] as const))), expected);
	});

	it('leaves a multi-line import beside an import of the same kind to no rule, and one of another kind to this rule', () => {
		const multi = lines('import {', '  a,', '  b,', "} from 'a';", "import { c } from 'c';", "import 'd';", 'run();');
		const rules: Entries = { 'separate-import-groups': 'error', 'separate-multi-line-constructs': 'error', 'separate-import-section': 'error' };

		assert.equal(fix(multi, rules), lines('import {', '  a,', '  b,', "} from 'a';", "import { c } from 'c';", '', "import 'd';", '', 'run();'));
	});

	it('is enabled at the strong level and not below it, and the option stays off in the preset', () => {
		const types = lines("import type { A } from 'a';", "import { b } from 'b';", "import 'c';", 'run();');

		assert.equal(fixWithPreset(types, 'moderate'), lines("import type { A } from 'a';", "import { b } from 'b';", "import 'c';", '', 'run();'));
		assert.equal(fixWithPreset(types, 'strong'), lines("import type { A } from 'a';", "import { b } from 'b';", '', "import 'c';", '', 'run();'));
	});
});
