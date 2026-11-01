import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

import { parse } from '@typescript-eslint/parser';
import { Linter, RuleTester } from 'eslint';
import prettier from 'prettier';
import tseslint from 'typescript-eslint';

import plugin, { configs } from '../src/index.ts';
import { optInRules, type Level } from '../src/presets.ts';

RuleTester.describe = describe;
RuleTester.it = it;

/** Creates a tester that parses TypeScript with JSX, the superset of the files the rules read. */
export function createRuleTester(): RuleTester {
	return new RuleTester({
		languageOptions: {
			ecmaVersion: 'latest',
			parser: tseslint.parser,
			parserOptions: { ecmaFeatures: { jsx: true } },
			sourceType: 'module',
		},
	});
}

/** Builds a flat config that enables the named rules, each with its options, over a TypeScript file. */
export function createConfig(rules: Record<string, Linter.RuleEntry>, settings: Record<string, unknown> = {}): Linter.Config[] {
	const entries = Object.fromEntries(Object.entries(rules).map(([name, entry]) => [`stanza/${name}`, entry]));

	return [
		{
			files: ['**/*.ts', '**/*.tsx', '**/*.js'],
			languageOptions: { parser: tseslint.parser, parserOptions: { ecmaFeatures: { jsx: true } }, sourceType: 'module' },
			plugins: { stanza: plugin as never },
			rules: entries,
			settings,
			linterOptions: { reportUnusedDisableDirectives: 'off' },
		},
	];
}

/** Fixes a source with the named rules until nothing is left to fix, as ESLint does. */
export function fix(code: string, rules: Record<string, Linter.RuleEntry>, settings: Record<string, unknown> = {}, filename = 'file.ts'): string {
	return new Linter().verifyAndFix(code, createConfig(rules, settings), { filename }).output;
}

/** Converts a source to CRLF line endings; the carriage return is built from its code point. */
export function crlf(text: string): string {
	return text.replaceAll('\n', String.fromCharCode(13, 10));
}

/** Joins lines into a source that ends with a line break. */
export function lines(...text: string[]): string {
	return `${text.join('\n')}\n`;
}

export type { Level };

/** The preset runs of the suites that fix the corpus: each level, and the strong level with the opt-in rules. */
export const presetRuns: [name: string, level: Level, withOptIn: boolean][] = [
	['minimal level', 'minimal', false],
	['moderate level', 'moderate', false],
	['strong level', 'strong', false],
	['strong level with the opt-in rules', 'strong', true],
];

/** Reads the rules a preset enables, without the plugin prefix; `withOptIn` adds each opt-in rule, as an entry of a project's own would. */
export function readPresetRules(level: Level, withOptIn = false): Record<string, Linter.RuleEntry> {
	const rules = configs[level].rules ?? {};

	return {
		...Object.fromEntries(Object.entries(rules).map(([name, entry]) => [name.replace(/^stanza\//, ''), entry as Linter.RuleEntry])),
		...(withOptIn ? Object.fromEntries(optInRules.map((name) => [name, 'error' as const])) : {}),
	};
}

/** Reads the first level whose preset enables the rule, or opt-in for a rule that no preset holds. */
export function readRuleLevel(name: string): string | undefined {
	if ((optInRules as readonly string[]).includes(name)) {
		return 'opt-in';
	}

	return (['minimal', 'moderate', 'strong'] as const).find((level) => `stanza/${name}` in (configs[level].rules ?? {}));
}

/** Lints a source with a preset and returns the messages ESLint reports. */
export function lintWithPreset(code: string, level: Level, filename = 'file.ts', withOptIn = false) {
	return new Linter().verify(code, createConfig(readPresetRules(level, withOptIn)), { filename });
}

/** Fixes a source with a preset until nothing is left to fix. */
export function fixWithPreset(code: string, level: Level, filename = 'file.ts', withOptIn = false): string {
	return fix(code, readPresetRules(level, withOptIn), {}, filename);
}

const corpusDirectory = fileURLToPath(new URL('./corpus/', import.meta.url));

/** Reads the files of the corpus with the name and the text of each. */
export function readCorpus(): { name: string; path: string; text: string }[] {
	return readdirSync(corpusDirectory)
		.sort()
		.map((name) => {
			const path = join(corpusDirectory, name);
			return { name, path, text: readFileSync(path, 'utf8') };
		});
}

/** Formats a source the way the repository formats it, using the Prettier settings next to the corpus; `overrides` replaces some of them. */
export async function formatWithPrettier(code: string, filepath: string, overrides: Record<string, unknown> = {}): Promise<string> {
	const options = await prettier.resolveConfig(filepath);
	return prettier.format(code, { ...options, ...overrides, filepath });
}

/** Reads the syntax tree without positions, so two sources compare equal when only whitespace and comments differ. */
export function readShape(code: string): string {
	const ast = parse(code, { jsx: true, range: false, loc: false, comment: false, tokens: false });

	return JSON.stringify(ast, function (this: { type?: string }, key: string, value: unknown) {
		if (this.type === 'JSXText' && key === 'value' && typeof value === 'string' && value.trim() === '' && value.includes(String.fromCharCode(10))) {
			return '';
		}

		return typeof value === 'bigint' ? String(value) : key === 'parent' || key === 'raw' || key === 'range' || key === 'loc' ? undefined : value;
	});
}
