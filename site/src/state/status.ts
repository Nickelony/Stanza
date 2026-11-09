import { hasLevels, type Language } from '../languages/language.ts';

import { effectiveState, isOptionChanged, isReporting, replacedBy, settingOf, type Config } from './config.ts';
import { enabledNote } from './levels.ts';
import { optionId, type Option, type Rule, type SiteCatalog, type Snippet } from './model.ts';

export interface RuleStatus {
	text: string;
	line: string | null;
	off: boolean;
}

/** What a rule's status box says: the sentence, the line the configuration writes for it, and whether it reports nothing. */
export function ruleStatus(language: Language, catalog: SiteCatalog, rule: Rule<Snippet>, config: Config): RuleStatus {
	const setting = settingOf(config, rule);
	const effective = effectiveState(rule, config);
	const markupNote = rule.csharp?.markupOnByDefault && setting === 'default' ? ' stanza-markup applies it unless a file disables it.' : '';
	const formatterNote = rule.csharp?.undoneByCSharpier ? ' CSharpier undoes the layout this rule writes.' : '';
	const line = language.ruleLines(catalog, rule, config).join('\n') || null;
	const lead = isReporting(effective) ? `${language.stateLabels[effective]}. ` : '';
	const opposite = replacedBy(rule, config);
	const sentence = (opposite !== null && effective === 'off' ? language.replacedText?.(opposite) : undefined) ?? language.statusText[effective];
	const levelNote = hasLevels(language) ? enabledNote(rule, config.formatter) : '';

	return {
		text: `${lead}${sentence}${levelNote}${markupNote}${formatterNote}`,
		line,
		off: !isReporting(effective),
	};
}

/** What the status box says of the value the form holds for the option. */
export function statusSentence(option: Option<Snippet>, config: Config, line: string | null): string {
	const stands = line === null ? 'no line is written' : 'Every setting is on, so it is written';

	if (config.options[optionId(option)] === undefined) {
		return `Left at its default; ${stands}.`;
	}

	return isOptionChanged(option, config) ? 'Set to a value other than the default.' : `Set to the default value; ${stands}.`;
}
