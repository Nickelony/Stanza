import { csharp, editorConfigBlock } from './csharp.ts';
import type { Language } from './language.ts';

const alike = 'stanza-markup applies the rule at any severity.';

/**
 * The Markup page: the markup rules and the keys of `stanza-markup`, written to the same `.editorconfig` as the C# page.
 * It has no level, because no level holds a markup rule, so a rule is on or off and the file says which.
 */
export const markup: Language = {
	...csharp,
	id: 'markup',
	title: 'Markup Stanza configurator',

	description:
		'Pick the markup rules and keys of stanza-markup and the C# analyzers, see the XML before and after, and copy the matching .editorconfig.',

	formatterPicker: false,
	severities: [],
	defaultSeverity: 'suggestion',
	severityHints: {},
	levelHints: null,

	everySettingHint:
		'Write every rule and every key. A rule that is on gets its severity, and a rule that is off a comment, or a none line where stanza-markup would otherwise apply it.',

	elsewhere: { lead: 'The key stanza_markup_literals, which picks the C# literals the analyzer reads, is on the', id: 'csharp' },

	statusText: {
		error: `Reports as an error in the analyzer, which fails a build; ${alike}`,
		warning: `Reports as a warning in the analyzer, in the IDE and in a build; ${alike}`,
		suggestion: `Reports as a suggestion in the analyzer, in the IDE and never in a build; ${alike}`,
		silent: `Enabled without a report; the rule still owns its gap or construct in a pair, and ${alike}`,
		none: 'Disabled: the rule reports nothing, in the analyzer or in stanza-markup.',
		off: 'Off: the rule reports nothing until a line enables it.',
	},

	outputs: [editorConfigBlock],
};
