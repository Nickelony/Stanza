import {
	addsPresetFile,
	defaultFormatter,
	defaultLevel,
	defaultPackageSeverity,
	enabledAt,
	type Formatter,
	type Level,
	type PackageSeverity,
} from './levels.ts';
import { audienceOf, optionId, wordsOf, type Catalog, type Option, type Rule, type RuleSetting, type Severity } from './model.ts';

/** What the user chose: the level, the formatter and the severity, and only the rules and options that differ from them are stored. */
export interface Config {
	/** The `StanzaLevel` the project sets: the rules the form starts from. */
	level: Level;

	/** The `StanzaFormatter` the project sets; `csharpier` also hides the rules CSharpier undoes and warns about one that is enabled. */
	formatter: Formatter;

	/** The `StanzaSeverity` the project sets: the severity of the rules the level enables. */
	severity: PackageSeverity;

	/** The rules the visitor set, by ID; one that says what the level already says counts as no change. */
	rules: Record<string, Severity>;

	/** The options the visitor set, by `optionId`. */
	options: Record<string, string>;

	/** Write every setting, the ones at their default included. */
	everySetting: boolean;

	/** Write the section that `stanza-markup` reads. */
	markupSection: boolean;
}

export const severities: readonly Severity[] = ['error', 'warning', 'suggestion', 'silent', 'none'];

/** The configuration that follows the pickers' defaults; a language whose severity default differs from the C# one passes its own, and a language with no levels the none level. */
export function emptyConfig(severity: PackageSeverity = defaultPackageSeverity, level: Level = defaultLevel): Config {
	return {
		level,
		formatter: defaultFormatter,
		severity,
		rules: {},
		options: {},
		everySetting: false,
		markupSection: true,
	};
}

/** What the visitor set for the rule, or `default` for a rule left to the level. */
export function settingOf(config: Config, rule: { id: string }): RuleSetting {
	return config.rules[rule.id] ?? 'default';
}

/**
 * Whether the rule page hides the rule from a user who runs CSharpier.
 * A rule the configuration sets stays in view, so the visitor can see what the page writes and change it.
 */
export function hiddenByCSharpier<S>(rule: Rule<S>, config: Config): boolean {
	return config.formatter === 'csharpier' && rule.csharp?.undoneByCSharpier === true && settingOf(config, rule) === 'default';
}

export type EffectiveState = Severity | 'off';

/** Whether a rule in this state reports: neither the level leaves it off nor a line of `none` disables it. */
export function isReporting(state: EffectiveState): boolean {
	return state !== 'off' && state !== 'none';
}

/** The tool whose section of an `.editorconfig` a line goes under. */
export type Context = 'analyzer' | 'markup';

const contexts: readonly Context[] = ['analyzer', 'markup'];

/** Whether the tool for the context reads the rule: the analyzer reads each one, `stanza-markup` the ones it declares. */
function readsRule<S>(rule: Rule<S>, context: Context): boolean {
	return context === 'analyzer' || rule.csharp?.readByMarkup === true;
}

/** The severity `stanza-markup` applies to a rule with no line, or `off`. */
function toolDefault<S>(rule: Rule<S>): EffectiveState {
	return rule.csharp?.markupOnByDefault ? rule.tier : 'off';
}

/**
 * Whether two states are the same for the tool of the context.
 * `none` and `off` both report nothing, so a comparison does not tell them apart.
 * `stanza-markup` applies any severity but `none` alike, so for it only on and off differ.
 */
function sameIn(a: EffectiveState, b: EffectiveState, context: Context): boolean {
	const first = a === 'none' ? 'off' : a;
	const second = b === 'none' ? 'off' : b;

	return context === 'markup' ? (first === 'off') === (second === 'off') : first === second;
}

/**
 * The opposite that takes a rule out of the level, or `null`.
 * Where the tool replaces a half of a pair, a rule the level enables is off once the form enables its opposite.
 */
export function replacedBy<S>(rule: Rule<S>, config: Config): string | null {
	const named = rule.opposite === undefined ? undefined : config.rules[rule.opposite];
	const replaced = named !== undefined && isReporting(named) && enabledAt(rule, config.level, config.formatter);

	return replaced ? rule.opposite! : null;
}

/** What the package applies to the rule when no line sets it: the level at the picked severity, apart from a half the form's other half replaces. */
function packageState<S>(rule: Rule<S>, config: Config): EffectiveState {
	return enabledAt(rule, config.level, config.formatter) && replacedBy(rule, config) === null ? config.severity : 'off';
}

/**
 * What a tool applies to the rule when the form sets nothing for it: what the pickers give.
 * The analyzer takes the level at the picked severity, apart from a half the form's other half replaces.
 * `stanza-markup` never reads the level, so a level can only add rules to it; a rule it applies by default stays on.
 * A markup rule has no level, so the page treats it alike for both tools and gives the tool's default: the page writes the line the analyzer needs.
 */
export function levelState<S>(rule: Rule<S>, config: Config, context: Context = 'analyzer'): EffectiveState {
	if (enabledAt(rule, config.level, config.formatter)) {
		return packageState(rule, config);
	}

	return context === 'markup' || rule.level === 'no-level' ? toolDefault(rule) : 'off';
}

/** What a tool applies to the rule once the form's own setting, if any, is in. */
export function ruleStateIn<S>(rule: Rule<S>, config: Config, context: Context): EffectiveState {
	return config.rules[rule.id] ?? levelState(rule, config, context);
}

/** The severity the analyzer reports at, `off` when the level leaves the rule off and nothing enables it, or `none` when a line disables it. */
export function effectiveState<S>(rule: Rule<S>, config: Config): EffectiveState {
	return ruleStateIn(rule, config, 'analyzer');
}

/**
 * Whether the form's setting for the rule changes what a tool does from what the pickers give.
 * A setting that says what the level already says is no change, so an `.editorconfig` the page wrote imports as no change.
 */
export function isRuleChanged<S>(rule: Rule<S>, config: Config): boolean {
	return contexts.some((context) => isRuleChangedIn(rule, config, context));
}

/** Whether the form's setting for the rule changes what the tool for the context does from what the pickers give. */
export function isRuleChangedIn<S>(rule: Rule<S>, config: Config, context: Context): boolean {
	return (
		config.rules[rule.id] !== undefined &&
		readsRule(rule, context) &&
		!sameIn(ruleStateIn(rule, config, context), levelState(rule, config, context), context)
	);
}

/**
 * The severity the rule's line has in the section of a tool, or `null` when the section needs no line.
 * The analyzer gets the level from the package, so its section holds only what differs from the level, and every markup rule that is on, which no level holds.
 * `stanza-markup` reads only the file, so its section holds whatever differs from the tool's own default, the rules of the level included.
 */
export function ruleDeltaIn<S>(rule: Rule<S>, config: Config, context: Context): Severity | null {
	if (!readsRule(rule, context)) {
		return null;
	}

	const state = ruleStateIn(rule, config, context);
	const reference = context === 'analyzer' ? packageState(rule, config) : toolDefault(rule);

	if (sameIn(state, reference, context)) {
		return null;
	}

	return state === 'off' ? 'none' : state;
}

/** Whether a file enables the rule in the sense of the terms page: a line of the file, or the preset file, sets it to something but `none`. */
export function isEnabledByFile<S>(rule: Rule<S>, config: Config): boolean {
	if (!isReporting(effectiveState(rule, config))) {
		return false;
	}

	return ruleDeltaIn(rule, config, 'analyzer') !== null || config.everySetting || addsPresetFile(config.level, config.severity);
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
			const accepted = wordsOf(option);

			if (accepted.includes(value)) {
				return { ok: true, value };
			}

			const words = accepted.length > 0 ? ` or ${accepted.join(', ')}` : '';

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

			const unknown = items.find((item) => option.choices.length > 0 && !option.choices.includes(item));

			if (unknown !== undefined) {
				return { ok: false, reason: `Use ${option.choices.join(', ')}, separated by commas.` };
			}

			return items.length > 0 ? { ok: true, value: items.join(', ') } : { ok: true, value: null };
		}
	}
}

/** Whether the tool for the context reads the key. */
export function readsOption<S>(option: Option<S>, context: Context): boolean {
	return context === 'analyzer' ? audienceOf(option) !== 'markup' : audienceOf(option) !== 'analyzer';
}

/** The value the tool for the context uses when no line sets the key, when the docs give one. */
export function defaultIn<S>(option: Option<S>, context: Context): string | null {
	return context === 'analyzer' ? option.defaultValue : (option.csharp?.markupDefaultValue ?? null);
}

/** Whether a line for the key would change what the tool for the context does. */
export function differsFromDefaultIn<S>(option: Option<S>, value: string | undefined, context: Context): boolean {
	return value !== undefined && readsOption(option, context) && value !== defaultIn(option, context);
}

/** Whether a line for the key would change what either tool does. */
export function differsFromDefault<S>(option: Option<S>, value: string | undefined): boolean {
	return contexts.some((context) => differsFromDefaultIn(option, value, context));
}

/**
 * Picks a level: the form is filled from it, with its rules on at the picked severity and the others off.
 * The rules the visitor set for another level are dropped, because they were changes against that one.
 */
export function withLevel(config: Config, level: Level): Config {
	return { ...config, level, rules: {} };
}

/** Sets the rule to the severity, or back to the level with `default`. */
export function withRule(config: Config, id: string, setting: RuleSetting): Config {
	const rules = { ...config.rules };

	if (setting === 'default') {
		delete rules[id];
	} else {
		rules[id] = setting;
	}

	return { ...config, rules };
}

/** Sets the option with this identity to the value, or back to its default with `null`. */
export function withOption(config: Config, id: string, value: string | null): Config {
	const options = { ...config.options };

	if (value === null) {
		delete options[id];
	} else {
		options[id] = value;
	}

	return { ...config, options };
}

/** Drops every rule and option the visitor set, and keeps the pickers and the two checkboxes. */
export function withoutChanges(config: Config): Config {
	return { ...config, rules: {}, options: {} };
}

/** How many rules and options differ from what the pickers and the defaults give. */
export function changedCount<S>(catalog: Catalog<S>, config: Config): number {
	const rules = catalog.rules.filter((rule) => isRuleChanged(rule, config)).length;
	const options = catalog.options.filter((option) => isOptionChanged(option, config)).length;

	return rules + options;
}

/** The items of a list option, which `checkOption` stores joined by a comma and a space. */
export const listItems = (value: string): string[] => value.split(', ');

/** The value the tool uses for an option of a preset language: the visitor's, else the default. */
export function optionValueAt<S>(option: Option<S>, config: Config): string | null {
	return config.options[optionId(option)] ?? option.defaultValue;
}

/** Whether the form holds a value for the option that differs from its default. */
export function isOptionChanged<S>(option: Option<S>, config: Config): boolean {
	return differsFromDefault(option, config.options[optionId(option)]);
}
