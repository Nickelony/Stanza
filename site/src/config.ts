import type { Catalog, Option, Rule, RuleSetting, Severity } from './model.ts';

/** What the user chose: only the rules and options that are not left at their default are stored. */
export interface Config {
	rules: Record<string, Severity>;
	options: Record<string, string>;

	/** Write every setting, the ones at their default included. */
	everySetting: boolean;

	/** Write the section that `stanza-markup` reads. */
	markupSection: boolean;
}

export const severities: readonly Severity[] = ['error', 'warning', 'suggestion', 'silent', 'none'];

export function emptyConfig(): Config {
	return { rules: {}, options: {}, everySetting: false, markupSection: true };
}

export function settingOf(config: Config, rule: { id: string }): RuleSetting {
	return config.rules[rule.id] ?? 'default';
}

/**
 * Whether a setting changes what the analyzer does.
 * A rule that is on by default reports at its tier, so its own tier as a setting changes nothing.
 */
export function isRuleChanged<S>(rule: Rule<S>, setting: RuleSetting): boolean {
	return setting !== 'default' && !(rule.onByDefault && setting === rule.tier);
}

export type EffectiveState = Severity | 'off';

/** The severity the analyzer reports at, or `off` when it reports nothing and nothing enables it. */
export function effectiveState<S>(rule: Rule<S>, setting: RuleSetting): EffectiveState {
	if (setting === 'default') {
		return rule.onByDefault ? rule.tier : 'off';
	}

	return setting;
}

export type OptionCheck = { ok: true; value: string | null } | { ok: false; reason: string };

/** Checks what was typed for an option against the values its page allows; empty input leaves the option at its default. */
export function checkOption<S>(option: Option<S>, raw: string): OptionCheck {
	const value = raw.trim();

	if (value === '') {
		return { ok: true, value: null };
	}

	// A tab, a line separator or another control character would end the line or the value in some readers of an .editorconfig.
	if (/[\p{Cc}\p{Zl}\p{Zp}#;]/u.test(value)) {
		return { ok: false, reason: 'A value cannot hold a control character, a line break, "#" or ";".' };
	}

	switch (option.kind) {
		case 'enum':
			return option.choices.includes(value) ? { ok: true, value } : { ok: false, reason: `Use ${option.choices.join(', ')}.` };

		case 'number': {
			if (option.words.includes(value)) {
				return { ok: true, value };
			}

			const words = option.words.length > 0 ? ` or ${option.words.join(', ')}` : '';

			if (!/^\d+$/.test(value)) {
				return { ok: false, reason: `Use a whole number from ${option.min} to ${option.max}${words}.` };
			}

			const number = Number(value);

			return number >= (option.min ?? 0) && number <= (option.max ?? Number.MAX_SAFE_INTEGER)
				? { ok: true, value: String(number) }
				: { ok: false, reason: `Use a whole number from ${option.min} to ${option.max}${words}.` };
		}

		case 'list': {
			const items = value
				.split(',')
				.map((item) => item.trim())
				.filter((item) => item !== '');

			return items.length > 0 ? { ok: true, value: items.join(', ') } : { ok: true, value: null };
		}
	}
}

/** The tool whose section of an `.editorconfig` a line goes under. */
export type Context = 'analyzer' | 'markup';

const contexts: readonly Context[] = ['analyzer', 'markup'];

/** Whether the tool for the context reads the key. */
export function readsOption<S>(option: Option<S>, context: Context): boolean {
	return context === 'analyzer' ? option.audience !== 'markup' : option.audience !== 'analyzer';
}

/** The value the tool for the context uses when no line sets the key, when the docs give one. */
export function defaultIn<S>(option: Option<S>, context: Context): string | null {
	return context === 'analyzer' ? option.defaultValue : option.markupDefaultValue;
}

/** Whether a line for the key would change what the tool for the context does. */
export function isOptionChangedIn<S>(option: Option<S>, value: string | undefined, context: Context): boolean {
	return value !== undefined && readsOption(option, context) && value !== defaultIn(option, context);
}

/** Whether a line for the key would change what either tool does. */
export function isOptionChanged<S>(option: Option<S>, value: string | undefined): boolean {
	return contexts.some((context) => isOptionChangedIn(option, value, context));
}

export function changedCount<S>(catalog: Catalog<S>, config: Config): number {
	const rules = catalog.rules.filter((rule) => isRuleChanged(rule, settingOf(config, rule))).length;
	const options = catalog.options.filter((option) => isOptionChanged(option, config.options[option.key])).length;

	return rules + options;
}

// The address hash
const letters: Record<Severity | 'default', string> = {
	error: 'e',
	warning: 'w',
	suggestion: 's',
	silent: 'i',
	none: 'n',
	default: 'd',
};

const bySeverity = new Map<string, Severity>(severities.map((severity) => [letters[severity], severity]));

export type Selection = { kind: 'rule' | 'option'; id: string };

export interface HashState {
	config: Config;
	selection: Selection | null;
}

/** Writes the configuration as a hash such as `ZA0003=w&stanza_max_line_length=120`, which a link can carry. */
export function encodeHash<S>(catalog: Catalog<S>, state: HashState): string {
	const params = new URLSearchParams();

	for (const rule of catalog.rules) {
		const severity = state.config.rules[rule.id];

		if (severity) {
			params.set(rule.id, letters[severity]);
		}
	}

	for (const option of catalog.options) {
		const value = state.config.options[option.key];

		if (value !== undefined) {
			params.set(option.key, value);
		}
	}

	// The hash writes `everySetting` as the short key `all`, which keeps a link short.
	if (state.config.everySetting) {
		params.set('all', '1');
	}

	if (!state.config.markupSection) {
		params.set('markup', '0');
	}

	if (state.selection) {
		params.set('sel', state.selection.id);
	}

	return params.toString();
}

/** Reads a hash back; a key the catalog does not know and a value its page does not allow are dropped. */
export function decodeHash<S>(catalog: Catalog<S>, hash: string): HashState {
	const params = new URLSearchParams(hash.replace(/^#/, ''));
	const config = emptyConfig();

	for (const rule of catalog.rules) {
		const severity = bySeverity.get(params.get(rule.id) ?? '');

		if (severity) {
			config.rules[rule.id] = severity;
		}
	}

	for (const option of catalog.options) {
		const raw = params.get(option.key);
		const checked = raw === null ? null : checkOption(option, raw);

		if (checked?.ok && checked.value !== null) {
			config.options[option.key] = checked.value;
		}
	}

	config.everySetting = params.get('all') === '1';
	config.markupSection = params.get('markup') !== '0';

	const selected = params.get('sel') ?? '';

	const selection: Selection | null = catalog.rules.some((rule) => rule.id === selected)
		? { kind: 'rule', id: selected }
		: catalog.options.some((option) => option.key === selected)
			? { kind: 'option', id: selected }
			: null;

	return { config, selection };
}
