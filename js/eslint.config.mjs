import tseslint from 'typescript-eslint';

import stanza from './dist/index.js';

// The plugin lints its own source with the strongest preset and the opt-in rules, so the build must run first.
export default [
	{ ignores: ['dist/**', 'node_modules/**', 'test/corpus/**'] },
	{ files: ['**/*.ts'], languageOptions: { parser: tseslint.parser } },
	stanza.configs.strong,
	{ rules: { curly: ['error', 'all'], 'stanza/keep-statement-runs-together': 'error', 'stanza/keep-member-runs-together': 'error' } },

	// A test sets its arrange, act and assert steps apart with blank lines, which the statement rule would join.
	{ files: ['test/**/*.ts'], rules: { 'stanza/keep-statement-runs-together': 'off' } },
];
