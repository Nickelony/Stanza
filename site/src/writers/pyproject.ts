import { listItems, ruleDeltaIn, type Config } from '../state/config.ts';
import { defaultLevel } from '../state/levels.ts';
import { optionId, type Catalog, type Option, type Rule } from '../state/model.ts';

const quote = (text: string): string => `"${text.replaceAll('\\', '\\\\').replaceAll('"', '\\"')}"`;

/** Writes a value of an option as TOML: a list or a word as strings, and `true`, `false` or a number as they are. */
function literal<S>(option: Option<S>, value: string): string {
	if (option.kind === 'list') {
		return `[${listItems(value).map(quote).join(', ')}]`;
	}

	return option.kind === 'enum' && value !== 'true' && value !== 'false' ? quote(value) : value;
}

const list = (ids: readonly string[]): string => `[${ids.map(quote).join(', ')}]`;

/** The codes of the rules the configuration adds to the level, and of those it removes. */
function ruleChanges<S>(catalog: Catalog<S>, config: Config): { enable: string[]; disable: string[] } {
	const enable: string[] = [];
	const disable: string[] = [];

	for (const rule of catalog.rules) {
		const delta = ruleDeltaIn(rule, config, 'analyzer');

		if (delta === 'none') {
			disable.push(rule.id);
		} else if (delta !== null) {
			enable.push(rule.id);
		}
	}

	return { enable, disable };
}

/** The line of a key the visitor changed, or `null` while it is at its default. */
function optionLine<S>(option: Option<S>, config: Config): string | null {
	const value = config.options[optionId(option)];
	return value === undefined || value === option.defaultValue ? null : `${option.key} = ${literal(option, value)}`;
}

/** The lines the configuration writes for a rule: the list it joins, with every code in it. */
export function pyprojectRuleLines<S>(catalog: Catalog<S>, rule: Rule<S>, config: Config): string[] {
	const { enable, disable } = ruleChanges(catalog, config);
	const key = disable.includes(rule.id) ? 'disable' : enable.includes(rule.id) ? 'enable' : null;

	return key === null ? [] : [`${key} = ${list(key === 'disable' ? disable : enable)}`];
}

/** The line the configuration writes for a key, or none while it is at its default. */
export function pyprojectOptionLines<S>(option: Option<S>, config: Config): string[] {
	const line = optionLine(option, config);
	return line === null ? [] : [line];
}

/** Writes the `[tool.stanza]` table of a `pyproject.toml` for a configuration: the level, the rules it adds or removes, and the keys it changes. */
export function generatePyproject<S>(catalog: Catalog<S>, config: Config): string {
	const { enable, disable } = ruleChanges(catalog, config);

	const lines = [
		config.level === defaultLevel ? null : `level = ${quote(config.level)}`,
		enable.length === 0 ? null : `enable = ${list(enable)}`,
		disable.length === 0 ? null : `disable = ${list(disable)}`,
		...catalog.options.map((option) => optionLine(option, config)),
	].filter((line): line is string => line !== null);

	return `${['[tool.stanza]', ...lines].join('\n')}\n`;
}
