import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

import { parse } from '@typescript-eslint/parser';
import { Linter, RuleTester, type Rule } from 'eslint';
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

/** Fixes a source as ESLint does, checking that one pass of fixes settles it, nothing is left to report and a second fix changes nothing. */
export function stable(
	source: string,
	rules: Record<string, Linter.RuleEntry>,
	settings: Record<string, unknown> = {},
	filename = 'file.ts',
): string {
	const linter = new Linter();
	const config = createConfig(rules, settings);
	const options = { filename, stats: true };
	const result = linter.verifyAndFix(source, config, options);

	assert.deepEqual(result.messages, []);
	assert.ok(linter.getFixPassCount() <= 1, `needed ${linter.getFixPassCount()} fix passes`);
	assert.equal(linter.verifyAndFix(result.output, config, { filename }).output, result.output);

	return result.output;
}

/** Fixes a source with the named rules enabled while applying the fixes of one of them only, as a per-rule fix in an editor does. */
export function fixRule(source: string, rules: Record<string, Linter.RuleEntry>, name: string, settings: Record<string, unknown> = {}): string {
	// ESLint takes a filter of the messages to fix, which its typings leave out.
	const fixOne = ((message: Linter.LintMessage) => message.ruleId === `stanza/${name}`) as unknown as boolean;
	return new Linter().verifyAndFix(source, createConfig(rules, settings), { filename: 'file.ts', fix: fixOne }).output;
}

/**
 * Fixes a source one report at a time, always the first or always the last one that carries a fix, as an editor that applies a single fix does.
 * `check` reads the text after each fix.
 */
export function fixOneByOne(source: string, config: Linter.Config[], pick: 'first' | 'last', check?: (text: string) => void): string {
	const linter = new Linter();
	let text = source;

	for (let step = 0; step < 1000; step += 1) {
		const fixes = linter.verify(text, config, { filename: 'file.ts' }).flatMap((message) => message.fix ?? []);
		const chosen = pick === 'first' ? fixes[0] : fixes.at(-1);

		if (chosen === undefined) {
			return text;
		}

		text = applyFix(text, chosen);
		check?.(text);
	}

	throw new Error('fixing one report at a time never settles');
}

/** Applies one fix to a source. */
export function applyFix(source: string, fix: Rule.Fix): string {
	return source.slice(0, fix.range[0]) + fix.text + source.slice(fix.range[1]);
}

/** Checks that each report of one group carries a fix that writes the expected text, so applying any one of them settles the group. */
export function assertGroupFix(source: string, reports: readonly Linter.LintMessage[], expected: string): void {
	assert.ok(reports.length > 0, 'the group has no report');

	for (const report of reports) {
		assert.ok(report.fix, `the report at line ${report.line} carries no fix`);
		assert.equal(applyFix(source, report.fix), expected);
	}
}

/** Fixes a source as `stable` does, and checks that fixing one report at a time from the top and from the bottom writes the same text. */
export function settled(source: string, rules: Record<string, Linter.RuleEntry>, settings: Record<string, unknown> = {}): string {
	const output = stable(source, rules, settings);
	const config = createConfig(rules, settings);

	assert.equal(fixOneByOne(source, config, 'first'), output, 'fixing from the top writes another text');
	assert.equal(fixOneByOne(source, config, 'last'), output, 'fixing from the bottom writes another text');

	return output;
}

/** Lints a source and reads the rule IDs of the reports its `eslint-disable` comments silence. */
export function readSilencedRuleIds(
	code: string,
	rules: Record<string, Linter.RuleEntry>,
	settings: Record<string, unknown> = {},
): (string | null)[] {
	const linter = new Linter();

	assert.deepEqual(linter.verify(code, createConfig(rules, settings), { filename: 'file.ts' }), []);

	return readSuppressedMessages(linter).map((message) => message.ruleId);
}

/** Lints a source and checks that its `eslint-disable` comments silence a report of each of the given rules, and that none of those reports carries a fix. */
export function assertSilencedWithoutFix(
	code: string,
	rules: Record<string, Linter.RuleEntry>,
	ruleIds: readonly string[],
	settings: Record<string, unknown> = {},
): void {
	const linter = new Linter();

	linter.verify(code, createConfig(rules, settings), { filename: 'file.ts' });

	const suppressed = readSuppressedMessages(linter);

	assert.deepEqual(
		suppressed.map((message) => message.ruleId),
		ruleIds,
	);

	assert.deepEqual(
		suppressed.map((message) => message.fix),
		ruleIds.map(() => undefined),
	);
}

/** Reads the reports of the last lint that `eslint-disable` comments silenced, which the typings of `Linter` leave out. */
export function readSuppressedMessages(linter: Linter): Linter.LintMessage[] {
	return (linter as Linter & { getSuppressedMessages: () => Linter.LintMessage[] }).getSuppressedMessages();
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

/** The settings the corpus suites vary beside the defaults, each read by the receiver rules or by the declaration shapes of `separate-declaration-runs`. */
export const settingVariants: [name: string, settings: Record<string, unknown>][] = [
	['all receiver roots', { stanza: { receiverRoots: ['*'], receiverComparison: 'root' } }],
	['all receiver paths', { stanza: { receiverRoots: ['*'], receiverComparison: 'path' } }],
	['named receiver paths and split declarations', { stanza: { receiverRoots: ['db'], receiverComparison: 'path', declarationShapes: 'split' } }],
];

/**
 * The runs of the suites that fix the corpus: each preset run with the default settings,
 * and each setting variant with the opt-in rules, the run that enables every rule the variants steer.
 */
export const corpusRuns: { name: string; level: Level; withOptIn: boolean; settings: Record<string, unknown> }[] = [
	...presetRuns.map(([name, level, withOptIn]) => ({ name, level, withOptIn, settings: {} })),

	...settingVariants.map(([variant, settings]) => ({
		name: `strong level with the opt-in rules and ${variant}`,
		level: 'strong' as const,
		withOptIn: true,
		settings,
	})),
];

/** Lists the rules that report a source, one entry for each message. */
export function reporters(
	code: string,
	rules: Record<string, Linter.RuleEntry>,
	filename = 'file.ts',
	settings: Record<string, unknown> = {},
): (string | null)[] {
	return new Linter().verify(code, createConfig(rules, settings), { filename }).map((message) => message.ruleId);
}

export type Entries = Record<string, Linter.RuleEntry>;

/** The ways to enable a pair of removal rules for the units of one half: that half alone, and both halves together. */
export function halvesOf(half: string, other: string, label: string): [name: string, rules: Entries][] {
	return [
		[`the ${label} half`, { [half]: 'error' }],
		['both halves', { [half]: 'error', [other]: 'error' }],
	];
}
