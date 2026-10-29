import type { EffectiveState } from '../config.ts';
import type { Option, Rule, Severity } from '../model.ts';

export const severityLabels: Record<Severity, string> = {
	error: 'Error',
	warning: 'Warning',
	suggestion: 'Suggestion',
	silent: 'Silent',
	none: 'None (Disabled)',
};

export const stateLabels: Record<EffectiveState, string> = {
	error: 'Error',
	warning: 'Warning',
	suggestion: 'Suggestion',
	silent: 'Silent',
	none: 'Disabled',
	off: 'Off',
};

const capitalize = (word: string): string => word.charAt(0).toUpperCase() + word.slice(1);

/** The label of a rule's "use the default" choice, which says what the default is. */
export function defaultLabel<S>(rule: Rule<S>): string {
	if (rule.onByDefault) {
		return `Default (${capitalize(rule.tier)})`;
	}

	// A rule's label names its state as the severities are named, while an option's dropdown shows the literal it writes.
	return rule.markupOnByDefault ? 'Default (Off; on in stanza-markup)' : 'Default (Off)';
}

/**
 * The label of an option's "use the default" choice. A derived default reads as plain Default, and a default that stanza-markup
 * replaces says so briefly; the tooltip and the preview carry the whole default.
 */
export function optionDefaultLabel<S>(option: Option<S>): string {
	if (option.defaultValue === null) {
		return 'Default';
	}

	if (option.audience === 'both' && option.markupDefaultValue === null) {
		return `Default (${option.defaultValue}; ${/profile/.test(option.defaultText) ? 'per profile' : 'varies'})`;
	}

	return `Default (${option.defaultValue})`;
}
