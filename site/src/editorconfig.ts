import { type Config, type Context, defaultIn, isOptionChanged, isOptionChangedIn, isRuleChanged, readsOption, settingOf } from './config.ts';
import type { Catalog, Option, Rule } from './model.ts';

const ruleLine = (id: string, severity: string): string => `dotnet_diagnostic.${id}.severity = ${severity}`;

/** The note that stands where the markup section would have been, so a changed key does not vanish without a word. */
const leftOutNote = '# stanza-markup settings are left out because Markup section is off.';

/** Whether the tool for the context applies the rule when no line enables it. */
function onByDefault<S>(rule: Rule<S>, context: Context): boolean {
	return rule.onByDefault || (context === 'markup' && rule.markupOnByDefault);
}

/** The line the configuration writes for a rule in the section of a tool, or `null` when it writes none. */
export function ruleLineIn<S>(rule: Rule<S>, config: Config, context: Context): string | null {
	if (context === 'markup' && !rule.readByMarkup) {
		return null;
	}

	const setting = settingOf(config, rule);

	if (isRuleChanged(setting)) {
		return ruleLine(rule.id, setting);
	}

	if (!config.everySetting) {
		return null;
	}

	// A rule that is off by default stays a comment, because a line with a severity would enable it.
	return onByDefault(rule, context) ? ruleLine(rule.id, rule.tier) : `# ${ruleLine(rule.id, rule.tier)}`;
}

function ruleLines<S>(catalog: Catalog<S>, config: Config, context: Context): string[] {
	const lines: string[] = [];
	let group = '';

	for (const rule of catalog.rules) {
		const line = ruleLineIn(rule, config, context);

		if (line === null) {
			continue;
		}

		// The group names are the analyzer's, so the markup section, which lists the few rules the tool reads, goes without them.
		if (config.everySetting && context === 'analyzer' && rule.group !== group) {
			if (lines.length > 0) {
				lines.push('');
			}

			lines.push(`# ${rule.group}.`);
			group = rule.group;
		}

		lines.push(line);
	}

	return lines;
}

/** The line the configuration writes for an option in the section of a tool, or `null` when it writes none. */
export function optionLineIn<S>(option: Option<S>, config: Config, context: Context): string | null {
	if (!readsOption(option, context)) {
		return null;
	}

	const value = config.options[option.key];
	const fallback = defaultIn(option, context);

	if (isOptionChangedIn(option, value, context)) {
		return `${option.key} = ${value}`;
	}

	return config.everySetting && fallback !== null ? `${option.key} = ${fallback}` : null;
}

/** Every distinct line the configuration writes for a rule, across the sections it writes. */
export function writtenRuleLines<S>(rule: Rule<S>, config: Config): string[] {
	return distinct(writtenContexts(config).map((context) => ruleLineIn(rule, config, context)));
}

/** Every distinct line the configuration writes for an option, across the sections it writes. */
export function writtenOptionLines<S>(option: Option<S>, config: Config): string[] {
	return distinct(writtenContexts(config).map((context) => optionLineIn(option, config, context)));
}

function distinct(lines: Array<string | null>): string[] {
	return [...new Set(lines.filter((line): line is string => line !== null))];
}

function writtenContexts(config: Config): Context[] {
	return config.markupSection ? ['analyzer', 'markup'] : ['analyzer'];
}

function optionLines<S>(catalog: Catalog<S>, config: Config, context: Context): string[] {
	const lines: string[] = [];

	for (const option of catalog.options) {
		const line = optionLineIn(option, config, context);

		if (line === null) {
			continue;
		}

		// A section matches many files, and a key that picks something per file would give every one of them the same pick.
		if (option.perFile && isOptionChangedIn(option, config.options[option.key], context)) {
			lines.push(`# ${option.key} applies to every file this section matches, so narrow the glob to the files that need it.`);
		}

		lines.push(line);
	}

	return lines;
}

function section(header: string, comment: string | null, rules: string[], options: string[]): string[] {
	if (rules.length === 0 && options.length === 0) {
		return [];
	}

	const lines = [header];

	if (comment) {
		lines.push(comment);
	}

	lines.push(...rules);

	if (rules.length > 0 && options.length > 0) {
		lines.push('');
	}

	lines.push(...options);

	return lines;
}

/** Whether an option is changed only for `stanza-markup`, so it reaches no line while the markup section is off. */
function leavesOutMarkup<S>(catalog: Catalog<S>, config: Config): boolean {
	return catalog.options.some((option) => {
		const value = config.options[option.key];
		return isOptionChanged(option, value) && !isOptionChangedIn(option, value, 'analyzer');
	});
}

/**
 * Writes the `.editorconfig` text for a configuration.
 * A rule or option left at its default writes nothing unless the configuration asks for every setting.
 */
export function generateEditorConfig<S>(catalog: Catalog<S>, config: Config): string {
	const analyzer = section('[*.cs]', null, ruleLines(catalog, config, 'analyzer'), optionLines(catalog, config, 'analyzer'));

	const markup = config.markupSection
		? section(
				`[*.{${catalog.markupExtensions.join(',')}}]`,
				'# The section stanza-markup reads for markup files.',
				ruleLines(catalog, config, 'markup'),
				optionLines(catalog, config, 'markup'),
			)
		: [];

	const note = !config.markupSection && leavesOutMarkup(catalog, config) ? [[leftOutNote]] : [];
	const sections = [...note, analyzer, markup].filter((lines) => lines.length > 0);

	if (sections.length === 0) {
		return '# Every rule and option is at its default, so nothing is written.\n';
	}

	return `${sections.map((lines) => lines.join('\n')).join('\n\n')}\n`;
}
