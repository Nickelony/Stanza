import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import stanza from 'eslint-plugin-stanza';
import { describe, expect, it } from 'vitest';

interface LintConfig {
	jsPlugins: { name: string; specifier: string }[];
	rules: Record<string, unknown>;
}

const config = JSON.parse(readFileSync(join(import.meta.dirname, '..', '.oxlintrc.json'), 'utf8')) as LintConfig;

describe('the oxlint configuration', () => {
	it('loads JS Stanza under the name its rules use', () => {
		expect(config.jsPlugins).toEqual([{ name: 'stanza', specifier: 'eslint-plugin-stanza' }]);
	});

	it('enables the rules of the strong preset of JS Stanza with its options, and the opt-in rules of the plugin', () => {
		const optIn = Object.keys(stanza.rules).filter((name) => !(`stanza/${name}` in (stanza.configs.strong.rules ?? {})));
		const rules = Object.entries(config.rules).filter(([name]) => name.startsWith('stanza/'));

		// The site keeps the cases of a switch and the children of a JSX element apart, so it leaves their tight halves off.
		const tightHalves = ['keep-multi-line-jsx-children-together', 'keep-multi-line-switch-cases-together'];

		expect(optIn).toEqual(['keep-member-runs-together', ...tightHalves, 'keep-statement-runs-together']);

		expect(Object.fromEntries(rules)).toEqual({
			...stanza.configs.strong.rules,
			...Object.fromEntries(optIn.filter((name) => !tightHalves.includes(name)).map((name) => [`stanza/${name}`, 'error'])),
		});
	});
});
