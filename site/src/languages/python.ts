import { blobUrl } from '../address/links.ts';
import { generatePyproject, pyprojectOptionLines, pyprojectRuleLines } from '../writers/pyproject.ts';

import type { Language } from './language.ts';

const enabled = 'stanza-py reports what the rule finds, and fix writes it.';

export const python: Language = {
	id: 'python',
	title: 'Python Stanza configurator',

	description:
		'Pick a level for the Python Stanza rules, tune each rule and key, see the code before and after, and copy the [tool.stanza] table of your pyproject.toml.',

	pageHost: 'on GitHub',
	docs: { lead: 'The rule pages are in the', name: 'repository', href: `${blobUrl}/py/docs/rules` },
	ruleFilterPlaceholder: 'Filter rules by ID or name',
	formatterPicker: false,
	severities: [],
	defaultSeverity: 'error',
	severityHints: {},

	levelHints: {
		minimal: 'The file header and the imports, and the padding before a clause keyword or between single-line case clauses.',
		moderate: 'Minimal, plus the multi-line, run, member and two-unit rules. The default.',

		strong:
			'Moderate, plus the await, assignment, guard, jump, comment, type, subscription and base call rules, the rules that gather loose runs, and the rules that name call arguments.',

		none: 'Enables no rule; enable rules one by one below. No level enables the opt-in rules.',
	},

	elsewhere: null,

	// Python Stanza reports no severity, so a rule is enabled or disabled, and the labels of the severities it lacks never show.
	ruleSettings: ['error', 'none'],
	severityLabels: { error: 'Enabled', warning: 'Enabled', suggestion: 'Enabled', silent: 'Enabled', none: 'Disabled' },
	stateLabels: { error: 'Enabled', warning: 'Enabled', suggestion: 'Enabled', silent: 'Enabled', none: 'Disabled', off: 'Off' },

	statusText: {
		error: enabled,
		warning: enabled,
		suggestion: enabled,
		silent: enabled,
		none: 'Disabled: the rule reports nothing.',
		off: 'Off at this level: the rule reports nothing until enable lists it.',
	},

	replacedText: (opposite) => `Off: ${opposite} is enabled and owns the gap, so the rule reports nothing unless enable lists it too.`,

	outputs: [
		{
			title: 'pyproject.toml',
			note: 'the [tool.stanza] table',
			label: 'Generated [tool.stanza] table',
			syntax: 'toml',
			compact: false,
			empty: null,
			file: { name: 'pyproject-stanza.toml', hint: 'The file holds only the table, which you copy into your pyproject.toml.' },
			tools: false,
			write: generatePyproject,
		},
	],

	ruleLines: pyprojectRuleLines,
	optionLines: (_catalog, option, config) => pyprojectOptionLines(option, config),
};
