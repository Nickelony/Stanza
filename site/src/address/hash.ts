import { hasEditorConfigTools, hasLevels, type Language } from '../languages/language.ts';
import { checkOption, emptyConfig, severities, type Config } from '../state/config.ts';
import { defaultFormatter, defaultLevel, formatters, levels } from '../state/levels.ts';
import { optionId, type Catalog, type Severity } from '../state/model.ts';
import { selectionOf, type Selection } from '../state/selection.ts';
import { opening, type AppState } from '../state/store.ts';

const letters: Record<Severity, string> = {
	error: 'e',
	warning: 'w',
	suggestion: 's',
	silent: 'i',
	none: 'n',
};

const bySeverity = new Map<string, Severity>(severities.map((severity) => [letters[severity], severity]));

export interface HashState {
	config: Config;
	selection: Selection | null;
}

/** Writes the configuration as a hash such as `ZA0014=w&stanza_max_line_length=120`, which a link can carry. */
export function encodeHash<S>(catalog: Catalog<S>, state: HashState, language: Language): string {
	const params = new URLSearchParams();

	for (const rule of catalog.rules) {
		const severity = state.config.rules[rule.id];

		if (severity) {
			params.set(rule.id, letters[severity]);
		}
	}

	for (const option of catalog.options) {
		const value = state.config.options[optionId(option)];

		if (value !== undefined) {
			params.set(optionId(option), value);
		}
	}

	// The hash writes `everySetting` as the short key `all`, which keeps a link short.
	if (hasEditorConfigTools(language) && state.config.everySetting) {
		params.set('all', '1');
	}

	if (hasEditorConfigTools(language) && !state.config.markupSection) {
		params.set('markup', '0');
	}

	if (hasLevels(language) && state.config.level !== defaultLevel) {
		params.set('level', state.config.level);
	}

	if (language.formatterPicker && state.config.formatter !== defaultFormatter) {
		params.set('formatter', state.config.formatter);
	}

	if (state.config.severity !== language.defaultSeverity) {
		params.set('severity', state.config.severity);
	}

	if (state.selection) {
		params.set('sel', state.selection.id);
	}

	return params.toString();
}

/** Reads a hash back; a key the catalog does not know and a value its page does not allow are dropped. */
export function decodeHash<S>(catalog: Catalog<S>, hash: string, language: Language): HashState {
	const params = new URLSearchParams(hash.replace(/^#/, ''));
	const config = emptyConfig(language.defaultSeverity, hasLevels(language) ? defaultLevel : 'none');

	for (const rule of catalog.rules) {
		const severity = bySeverity.get(params.get(rule.id) ?? '');

		if (severity && language.ruleSettings.includes(severity)) {
			config.rules[rule.id] = severity;
		}
	}

	for (const option of catalog.options) {
		const raw = params.get(optionId(option));
		const checked = raw === null ? null : checkOption(option, raw);

		if (checked?.ok && checked.value !== null) {
			config.options[optionId(option)] = checked.value;
		}
	}

	config.everySetting = hasEditorConfigTools(language) && params.get('all') === '1';
	config.markupSection = !hasEditorConfigTools(language) || params.get('markup') !== '0';
	config.level = hasLevels(language) ? (levels.find((level) => level === params.get('level')) ?? defaultLevel) : 'none';
	config.severity = language.severities.find((severity) => severity === params.get('severity')) ?? language.defaultSeverity;

	config.formatter = language.formatterPicker
		? (formatters.find((formatter) => formatter === params.get('formatter')) ?? defaultFormatter)
		: defaultFormatter;

	const selected = params.get('sel') ?? '';

	return { config, selection: selectionOf(catalog, selected) };
}

/** The change a hash the visitor typed makes to the state; a selection it names opens on its first variant, in view under the filters. */
export function patchFromHash<S>(catalog: Catalog<S>, hash: string, language: Language): Partial<AppState> {
	const next = decodeHash(catalog, hash, language);
	return next.selection ? { config: next.config, ...opening(catalog, next.config, next.selection) } : { config: next.config };
}
