import js from '@eslint/js';

import stanza from '../dist/index.js';

// The package lints its own files with the strong preset of the plugin in ../, so that package must be built first.
export default [
	{ ignores: ['node_modules/**'] },
	js.configs.recommended,
	stanza.configs.strong,
	{ rules: { curly: ['error', 'all'], 'stanza/keep-statement-runs-together': 'error', 'stanza/keep-member-runs-together': 'error' } },
];
