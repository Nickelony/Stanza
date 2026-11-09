import { hasLevels, type Language } from '../languages/language.ts';
import { counted } from '../util/counted.ts';

import { levelState, replacedBy, type Config } from './config.ts';
import type { Formatter, Level } from './levels.ts';
import type { Option, Rule } from './model.ts';

export const levelLabels: Record<Level, string> = {
	minimal: 'Minimal',
	moderate: 'Moderate',
	strong: 'Strong',
	none: 'None',
};

export const formatterLabels: Record<Formatter, string> = {
	'stanza-format': 'Stanza wins',
	csharpier: 'CSharpier wins',
};

/** What each formatter value means, shown under the formatter picker. */
export const formatterHints: Record<Formatter, string> = {
	'stanza-format': 'Format with stanza-format, which writes back the blank lines CSharpier drops. Keep CSharpier format on save off.',
	csharpier: 'CSharpier also runs on save or in CI, so the rules it undoes stay off.',
};

/** Shown under the severity picker while the none level leaves it nothing to set. */
export const severityNoneHint = 'No rule is on, so there is no severity to set.';

/** How the page names the level and what the visitor changed in it, such as `Moderate plus 2 changes`; a page with no levels counts the changes alone. */
export function summaryText(level: Level | null, changes: number): string {
	if (level === null) {
		return changes === 0 ? 'No changes' : counted(changes, 'change');
	}

	if (changes === 0) {
		return `${levelLabels[level]}, no changes`;
	}

	return `${levelLabels[level]} plus ${counted(changes, 'change')}`;
}

/** The label of a rule's "use the level" choice, which says what the level gives. */
export function ruleDefaultLabel<S>(rule: Rule<S>, config: Config, language: Pick<Language, 'stateLabels' | 'levelHints'>): string {
	const state = levelState(rule, config);

	// Without levels the choice follows no level, so the label names the default itself.
	if (!hasLevels(language)) {
		return `Default (${language.stateLabels[state]})`;
	}

	// A rule's label names its state as the severities are named, while an option's dropdown shows the literal it writes.
	if (state === 'off') {
		const opposite = replacedBy(rule, config);

		if (opposite !== null) {
			return `Level (Off; ${opposite} is enabled)`;
		}

		return rule.csharp?.markupOnByDefault ? 'Level (Off; on in stanza-markup)' : 'Level (Off)';
	}

	return `Level (${language.stateLabels[state]})`;
}

/**
 * The label of an option's "use the default" choice. A derived default reads as plain Default, and a default that stanza-markup
 * replaces says so briefly; the tooltip and the preview carry the whole default.
 */
export function optionDefaultLabel<S>(option: Option<S>): string {
	const value = option.defaultValue;

	if (value === null) {
		return 'Default';
	}

	if (option.csharp?.audience === 'both' && option.csharp.markupDefaultValue === null) {
		return `Default (${value}; ${option.csharp.markupDefaultByProfile ? 'per profile' : 'varies'})`;
	}

	return `Default (${value})`;
}
