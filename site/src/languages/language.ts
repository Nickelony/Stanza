import type { Config, EffectiveState } from '../state/config.ts';
import type { Level, PackageSeverity } from '../state/levels.ts';
import type { Option, Rule, Severity, SiteCatalog, Snippet } from '../state/model.ts';

/** The languages Stanza has a page for; the id picks the descriptor and the catalog a page loads. */
export type LanguageId = 'csharp' | 'markup' | 'js' | 'python';

/** How the browser colors a block of output, which it does from the text alone because it ships no highlighter. */
export type Syntax = 'plain' | 'editorconfig' | 'js' | 'toml';

/** One block of generated output under the preview. */
export interface OutputBlock {
	title: string;
	note: string | null;

	/** The accessible name of the code region. */
	label: string;
	syntax: Syntax;

	/** Whether the block is a few lines, so it is not given the height of a file. */
	compact: boolean;

	/** Shown while the block writes no text, which also disables Copy; `null` for a block that always writes some. */
	empty: string | null;

	/** The name a Download button saves the text as, with the hint it carries; `null` for a block that is only copied. */
	file: { name: string; hint: string | null } | null;

	/** Whether the import, the conflict list and the choices about the file sit with this block. */
	tools: boolean;

	write(catalog: SiteCatalog, config: Config): string;
}

/** What a page of the configurator is told about its language; every view reads it, and none branches on the language id. */
export interface Language {
	id: LanguageId;

	/** The title of the tab and of a shared link, which name the language; the heading of the page does not. */
	title: string;
	description: string;

	/** Where the full page of a rule is, as the link under the preview words it: `on the wiki`. */
	pageHost: string;

	/** The sentence the page shows without JavaScript, and the link that ends it. */
	docs: { lead: string; name: string; href: string };
	ruleFilterPlaceholder: string;

	/** Whether the page asks what wins on a conflict with a formatter that undoes the layout. */
	formatterPicker: boolean;

	/** The severities the page offers for the rules a level enables; empty when the language has none. */
	severities: readonly PackageSeverity[];
	defaultSeverity: PackageSeverity;
	severityHints: Partial<Record<PackageSeverity, string>>;

	/** What each level holds, shown under the Level picker; `null` for a language with no levels, which has no picker and no level in its address. */
	levelHints: Record<Level, string> | null;

	/** The tooltip of Every setting on a page with the `.editorconfig` tools, which words what the file holds. */
	everySettingHint?: string;

	/** Where the rules and keys the page lacks went: the sentence that ends with the link to that page's name, and the page. `null` when none went anywhere. */
	elsewhere: { lead: string; id: LanguageId } | null;

	/** What a rule's own dropdown offers besides following the level. */
	ruleSettings: readonly Severity[];
	severityLabels: Record<Severity, string>;
	stateLabels: Record<EffectiveState, string>;
	statusText: Record<EffectiveState, string>;

	/** What the status box says of a rule that the level enables and that its opposite replaces; only a language whose package replaces a half of a pair gives it. */
	replacedText?(opposite: string): string;
	outputs: readonly OutputBlock[];

	/** The lines the configuration writes for a rule or an option, as the preview shows them. */
	ruleLines(catalog: SiteCatalog, rule: Rule<Snippet>, config: Config): string[];
	optionLines(catalog: SiteCatalog, option: Option<Snippet>, config: Config): string[];
}

/** Whether the language has levels: a Level picker, a hint for each, and the level in the address. */
export const hasLevels = (language: Pick<Language, 'levelHints'>): boolean => language.levelHints !== null;

/** Whether the page imports an `.editorconfig`, lists conflicts, and offers Every setting and the markup section, because one block has the tools. */
export const hasEditorConfigTools = (language: Language): boolean => language.outputs.some((block) => block.tools);
