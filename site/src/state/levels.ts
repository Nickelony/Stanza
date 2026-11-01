import type { Rule, RuleLevel } from './model.ts';

/** The values of the `StanzaLevel` property. */
export const levels = ['none', 'minimal', 'moderate', 'strong'] as const;

export type Level = (typeof levels)[number];

/** The values of the `StanzaFormatter` property. */
export const formatters = ['stanza-format', 'csharpier'] as const;

export type Formatter = (typeof formatters)[number];

/** The values of the `StanzaSeverity` property. */
export const packageSeverities = ['suggestion', 'warning', 'error'] as const;

export type PackageSeverity = (typeof packageSeverities)[number];

/** What each property is while the project sets none, which is the preset the analyzers ship. */
export const defaultLevel: Level = 'moderate';
export const defaultFormatter: Formatter = 'stanza-format';
export const defaultPackageSeverity: PackageSeverity = 'suggestion';

/** How far into the levels a rule's own level reaches: a level holds the rules of the levels before it. */
const reach: Record<RuleLevel, number> = {
	minimal: 1,
	moderate: 2,
	strong: 3,
	'opt-in': Number.POSITIVE_INFINITY,
	'no-level': Number.POSITIVE_INFINITY,
};

/** How many levels a level holds. */
const depth: Record<Level, number> = { none: 0, minimal: 1, moderate: 2, strong: 3 };

/**
 * Whether the preset of the level and the formatter enables the rule.
 * The `csharpier` formatter changes only the strong level: it disables each rule marked `csharpier-off` and enables each marked `csharpier-on`.
 */
export function enabledAt<S>(rule: Pick<Rule<S>, 'level' | 'csharp'>, level: Level, formatter: Formatter): boolean {
	const mark = rule.csharp?.csharpier ?? null;

	if (level === 'strong' && formatter === 'csharpier' && mark !== null) {
		return mark === 'on';
	}

	return reach[rule.level] <= depth[level];
}

/** The sentence that says which level enables the rule, and what the picked formatter does to it at the strong level. */
export function enabledNote<S>(rule: Pick<Rule<S>, 'level' | 'csharp'>, formatter: Formatter): string {
	const mark = formatter === 'csharpier' ? (rule.csharp?.csharpier ?? null) : null;

	if (mark === 'on') {
		return ' No level enables it; the csharpier formatter enables it at the strong level.';
	}

	const lead = rule.level === 'opt-in' || rule.level === 'no-level' ? 'No level enables it' : `Enabled from the ${rule.level} level`;

	return mark === 'off' ? ` ${lead}; the csharpier formatter turns it off at the strong level.` : ` ${lead}.`;
}

/** Whether a project that sets these properties gets a preset file; the moderate level at suggestion is what ships, so it adds none. */
export function addsPresetFile(level: Level, severity: PackageSeverity): boolean {
	return !(level === defaultLevel && severity === defaultPackageSeverity);
}
