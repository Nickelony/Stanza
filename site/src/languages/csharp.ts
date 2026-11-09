import { wikiUrl } from '../address/links.ts';
import type { Severity } from '../state/model.ts';
import { generateEditorConfig, writtenOptionLines, writtenRuleLines } from '../writers/editorconfig.ts';
import { generatePropertyGroup, noPropertiesNeeded } from '../writers/msbuild.ts';

import type { Language, OutputBlock } from './language.ts';

const severityLabels: Record<Severity, string> = {
	error: 'Error',
	warning: 'Warning',
	suggestion: 'Suggestion',
	silent: 'Silent',
	none: 'None (Disabled)',
};

/** The block of the `.editorconfig`, with the import, the conflicts and the file choices, which the Markup page writes as well. */
export const editorConfigBlock: OutputBlock = {
	title: '.editorconfig',
	note: null,
	label: 'Generated .editorconfig',
	syntax: 'editorconfig',
	compact: false,
	empty: null,
	file: { name: '.editorconfig', hint: 'A browser that drops the leading dot saves the file as editorconfig; rename it to .editorconfig.' },
	tools: true,
	write: generateEditorConfig,
};

export const csharp: Language = {
	id: 'csharp',
	title: 'C# Stanza configurator',

	description:
		'Pick a level, a formatter and a severity for the Stanza analyzers, tune each rule and option, see the code before and after, and copy the MSBuild properties and the matching .editorconfig.',

	pageHost: 'on the wiki',
	docs: { lead: 'The rule pages are on the', name: 'wiki', href: wikiUrl },
	ruleFilterPlaceholder: 'Filter rules by ID or title',
	formatterPicker: true,
	severities: ['suggestion', 'warning', 'error'],
	defaultSeverity: 'suggestion',

	severityHints: {
		suggestion: 'Shows in the IDE and never fails a build.',
		warning: 'Reports in the build; fails it under TreatWarningsAsErrors.',
		error: 'Reports in the build and fails it.',
	},

	levelHints: {
		minimal: 'Spacing hygiene that rarely fires, such as the blank-line cap and brace padding.',
		moderate: 'The Stanza look: blank lines around multi-line code, consistent braces, is-null checks.',
		strong: 'The full style: restructuring, documentation layout, argument names and limits.',
		none: 'No rule is on; enable rules one by one below.',
	},

	everySettingHint:
		'Write each rule of the level and each key, so the file stands without the package properties and stanza-markup can read it. Its none lines and comments hold only for a project that sets the same level, or none.',

	elsewhere: { lead: 'The markup rules ZA2001 to ZA2010 and the keys of stanza-markup are on the', id: 'markup' },
	ruleSettings: ['error', 'warning', 'suggestion', 'silent', 'none'],
	severityLabels,
	stateLabels: { ...severityLabels, none: 'Disabled', off: 'Off' },

	statusText: {
		error: 'Reports as an error, which fails a build.',
		warning: 'Reports as a warning in the IDE and in a build.',
		suggestion: 'Reports as a suggestion in the IDE; a build never reports it.',
		silent: 'Enabled without a report; the rule still owns its gap or construct in a pair.',
		none: 'Disabled: the rule reports nothing, and a bulk key never reaches it.',
		off: 'Off at this level: the rule reports nothing until a line enables it.',
	},

	outputs: [
		{
			title: 'MSBuild properties',
			note: 'for a .csproj or Directory.Build.props',
			label: 'MSBuild properties',
			syntax: 'plain',
			compact: true,
			empty: noPropertiesNeeded,
			file: null,
			tools: false,
			write: (_catalog, config) => generatePropertyGroup(config),
		},

		editorConfigBlock,
	],

	ruleLines: (_catalog, rule, config) => writtenRuleLines(rule, config),
	optionLines: (_catalog, option, config) => writtenOptionLines(option, config),
};
