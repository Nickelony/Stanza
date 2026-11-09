import { useAppState } from '../../hooks/use-app-state.ts';
import { usePage } from '../../hooks/use-page.ts';
import { pages } from '../../languages/pages.ts';
import { isRuleChanged, withLevel } from '../../state/config.ts';
import { formatterHints, formatterLabels, levelLabels, severityNoneHint } from '../../state/labels.ts';
import { formatters, levels, type Level } from '../../state/levels.ts';
import { setConfig } from '../../state/store.ts';
import { counted } from '../../util/counted.ts';

import { Box, Hint } from './parts.ts';
import { PickerGroup } from './picker-group.tsx';

/** The pickers of the language: the level, and the severity and the formatter where it has them, then where the rules and keys it lacks went. */
export function Pickers() {
	const { catalog, language, store } = usePage();
	const { config } = useAppState();

	// The none level enables no rule, so there is no severity for it to apply.
	const noSeverity = config.level === 'none';

	const pickLevel = (level: Level): boolean => {
		const current = store.get().config;

		// The form is filled from the level, so the rules set against the old one go; the visitor confirms that, as for a reset.
		const set = catalog.rules.filter((rule) => isRuleChanged(rule, current)).length;
		const sure = set === 0 || window.confirm(`Picking a level fills the rules again and drops the ${counted(set, 'rule')} you set.`);

		if (sure) {
			store.set({ config: withLevel(current, level) });
		}

		return sure;
	};

	const { levelHints, elsewhere } = language;
	const other = elsewhere === null ? undefined : pages.find((page) => page.id === elsewhere.id);

	return (
		<Box>
			{levelHints !== null && (
				<PickerGroup
					name="level"
					title="Level"
					values={levels}
					labels={levelLabels}
					value={config.level}
					hint={levelHints[config.level]}
					tight
					onPick={pickLevel}
				/>
			)}

			{language.severities.length > 0 && (
				<PickerGroup
					name="severity"
					title="Severity"
					values={language.severities}
					labels={language.severityLabels}
					value={config.severity}
					hint={noSeverity ? severityNoneHint : (language.severityHints[config.severity] ?? '')}
					disabled={noSeverity}
					onPick={(severity) => setConfig(store, { severity })}
				/>
			)}

			{language.formatterPicker && (
				<PickerGroup
					name="formatter"
					title="On conflict with CSharpier"
					values={formatters}
					labels={formatterLabels}
					value={config.formatter}
					hint={formatterHints[config.formatter]}
					onPick={(formatter) => setConfig(store, { formatter })}
				/>
			)}

			{elsewhere !== null && other !== undefined && (
				<Hint>
					{elsewhere.lead} <a href={`${import.meta.env.BASE_URL}${other.path}`}>{other.name} page</a>.
				</Hint>
			)}
		</Box>
	);
}
