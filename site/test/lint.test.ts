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

	it('enables the strong preset with its options and the selected opt-in run rules', () => {
		const optIn = Object.keys(stanza.rules).filter((name) => !(`stanza/${name}` in (stanza.configs.strong.rules ?? {})));
		const rules = Object.entries(config.rules).filter(([name]) => name.startsWith('stanza/'));
		const selected = ['keep-member-runs-together', 'keep-statement-runs-together'];

		for (const name of selected) {
			expect(optIn).toContain(name);
		}

		expect(Object.fromEntries(rules)).toEqual({
			...stanza.configs.strong.rules,
			...Object.fromEntries(selected.map((name) => [`stanza/${name}`, 'error'])),
		});
	});
});
