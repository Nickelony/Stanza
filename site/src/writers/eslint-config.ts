import { isOptionChanged, listItems, replacedBy, ruleStateIn, type Config, type EffectiveState } from '../state/config.ts';
import { enabledAt } from '../state/levels.ts';
import { optionId, type Catalog, type Option, type Rule } from '../state/model.ts';

/** The severity every rule of a preset has, which `js/src/presets.ts` gives and the reader of the build checks. */
const presetSeverity = 'error';
const eslintSeverity: Partial<Record<EffectiveState, string>> = { error: 'error', warning: 'warn' };
const quote = (text: string): string => `'${text.replaceAll('\\', '\\\\').replaceAll("'", "\\'")}'`;

/** Writes a value of an option as JavaScript: `true`, `false`, or a list of strings. */
function literal<S>(option: Option<S>, value: string): string {
	return option.kind === 'list' ? `[${listItems(value).map(quote).join(', ')}]` : value;
}

/** The options an entry of the rule carries: those a rule's own page lists, which are not in `settings.stanza`. */
const ruleOptions = <S>(catalog: Catalog<S>, rule: Rule<S>): Option<S>[] =>
	catalog.options.filter((option) => !option.global && option.rules.includes(rule.id));

/**
 * The entry the configuration writes for a rule, or `null` when the preset of the level already gives it.
 * An entry with options names each one that is not its default, and an entry of a rule that is off names none.
 */
function ruleEntry<S>(catalog: Catalog<S>, rule: Rule<S>, config: Config): string | null {
	// The preset still holds the separating half of a pair, and the plugin gives the gap to the tight half, so a half the form shows replaced is written as the level gives it.
	const replaced = config.rules[rule.id] === undefined && replacedBy(rule, config) !== null;
	const state = replaced ? config.severity : ruleStateIn(rule, config, 'analyzer');
	const severity = eslintSeverity[state] ?? 'off';
	const preset = enabledAt(rule, config.level, config.formatter) ? presetSeverity : 'off';
	const changed = severity === 'off' ? [] : ruleOptions(catalog, rule).filter((option) => isOptionChanged(option, config));

	if (severity === preset && changed.length === 0) {
		return null;
	}

	if (changed.length === 0) {
		return `${quote(`stanza/${rule.id}`)}: ${quote(severity)},`;
	}

	const written = changed.map((option) => `${option.key}: ${literal(option, config.options[optionId(option)]!)}`);

	return `${quote(`stanza/${rule.id}`)}: [${quote(severity)}, { ${written.join(', ')} }],`;
}

/** The entry of `settings.stanza` for an option the rules share, or `null` while it is at its default. */
function settingEntry<S>(option: Option<S>, config: Config): string | null {
	const value = config.options[optionId(option)];
	return value === undefined || value === option.defaultValue ? null : `${option.key}: ${literal(option, value)},`;
}

/** The entries the configuration writes for a rule: its own, and none for a rule the preset gives as written. */
export function eslintRuleLines<S>(catalog: Catalog<S>, rule: Rule<S>, config: Config): string[] {
	const entry = ruleEntry(catalog, rule, config);
	return entry === null ? [] : [entry];
}

/** The entry the configuration writes for an option: a setting's own, or the entry of the rule that takes it. */
export function eslintOptionLines<S>(catalog: Catalog<S>, option: Option<S>, config: Config): string[] {
	if (option.global) {
		const entry = settingEntry(option, config);
		return entry === null ? [] : [entry];
	}

	return option.rules.flatMap((id) => {
		const rule = catalog.rules.find((candidate) => candidate.id === id);

		if (rule === undefined) {
			return [];
		}

		const entry = ruleEntry(catalog, rule, config);

		return entry !== null && isOptionChanged(option, config) ? [entry] : [];
	});
}

/** Writes the `eslint.config.mjs` for a configuration: the preset of the level after `eslint-config-prettier`, then what differs from it. */
export function generateEslintConfig<S>(catalog: Catalog<S>, config: Config): string {
	const rules = catalog.rules.flatMap((rule) => ruleEntry(catalog, rule, config) ?? []);
	const settings = catalog.options.flatMap((option) => (option.global ? (settingEntry(option, config) ?? []) : []));
	const body: string[] = [];

	if (rules.length > 0) {
		body.push('\t\trules: {', ...rules.map((line) => `\t\t\t${line}`), '\t\t},');
	}

	if (settings.length > 0) {
		body.push('\t\tsettings: {', '\t\t\tstanza: {', ...settings.map((line) => `\t\t\t\t${line}`), '\t\t\t},', '\t\t},');
	}

	return [
		"import prettier from 'eslint-config-prettier';",
		"import stanza from 'eslint-plugin-stanza';",
		'',
		'export default [',
		'\tprettier,',
		`\tstanza.configs.${config.level},`,
		...(body.length === 0 ? [] : ['\t{', ...body, '\t},']),
		'];',
		'',
	].join('\n');
}
