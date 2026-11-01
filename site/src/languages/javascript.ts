import { blobUrl } from '../address/links.ts';
import { eslintOptionLines, eslintRuleLines, generateEslintConfig } from '../writers/eslint-config.ts';

import type { Language } from './language.ts';

const warn = 'Reports as a warning, which fails the run only under --max-warnings.';

export const javascript: Language = {
	id: 'js',
	title: 'JS Stanza configurator',

	description:
		'Pick a level and a severity for the JS Stanza ESLint rules, tune each rule and option, see the code before and after, and copy the eslint.config.mjs that loads them.',

	pageHost: 'on GitHub',
	docs: { lead: 'The rule pages are in the', name: 'repository', href: `${blobUrl}/js/docs/rules` },
	ruleFilterPlaceholder: 'Filter rules by name',
	formatterPicker: false,
	severities: ['warning', 'error'],
	defaultSeverity: 'error',

	severityHints: {
		warning: 'ESLint reports the rules and exits with 0 unless --max-warnings is set.',
		error: 'ESLint reports the rules and exits with 1.',
	},

	levelHints: {
		minimal: 'The file header: the shebang, the directives and the imports, and the blank lines between single-line list elements and switch cases.',
		moderate: 'Minimal, plus the multi-line, switch case, run, member and test rules. The preset to start from.',

		strong:
			'Moderate, plus the import group, await, assignment, guard, base call, jump, comment, modifier, list and JSX child rules, and the rules that gather loose runs.',

		none: 'Registers the plugin and enables no rule; enable rules one by one below. No level enables the opt-in rules.',
	},

	elsewhere: null,
	ruleSettings: ['error', 'warning', 'none'],
	severityLabels: { error: 'Error', warning: 'Warn', suggestion: 'Warn', silent: 'Warn', none: 'Off (Disabled)' },
	stateLabels: { error: 'Error', warning: 'Warn', suggestion: 'Warn', silent: 'Warn', none: 'Disabled', off: 'Off' },

	statusText: {
		error: 'Reports as an error, which makes ESLint exit with 1.',
		warning: warn,
		suggestion: warn,
		silent: warn,
		none: 'Disabled: the rule is off and reports nothing.',
		off: 'Off at this level: the rule reports nothing until an entry enables it.',
	},

	replacedText: (opposite) => `Off: ${opposite} is enabled and owns the gap, so the rule reports nothing there.`,

	outputs: [
		{
			title: 'eslint.config.mjs',
			note: 'load the plugin after eslint-config-prettier',
			label: 'Generated eslint.config.mjs',
			syntax: 'js',
			compact: false,
			empty: null,
			file: { name: 'eslint.config.mjs', hint: null },
			tools: false,
			write: generateEslintConfig,
		},
	],

	ruleLines: eslintRuleLines,
	optionLines: eslintOptionLines,
};
