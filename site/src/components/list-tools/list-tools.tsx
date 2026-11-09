import { useAppState } from '../../hooks/use-app-state.ts';
import { usePage } from '../../hooks/use-page.ts';
import { hasLevels } from '../../languages/language.ts';
import { changedCount, withoutChanges } from '../../state/config.ts';
import { summaryText } from '../../state/labels.ts';
import { counted } from '../../util/counted.ts';
import { Button, CheckLabel } from '../primitives.ts';
import { tabId } from '../tabs/ids.ts';

import { Count, Filter, HiddenLabel, Row } from './parts.ts';

/** The filter, Changed only and Reset all above the lists, and the summary of the form beside them. */
export function ListTools() {
	const { catalog, language, store } = usePage();
	const state = useAppState();
	const total = changedCount(catalog, state.config);
	const onRules = state.selection?.kind !== 'option';

	// The address is replaced rather than pushed, so Back cannot undo a reset; the visitor confirms it first.
	const reset = (): void => {
		const { config } = store.get();
		const changes = changedCount(catalog, config);

		if (!window.confirm(`Put ${changes === 1 ? 'the' : 'all'} ${counted(changes, 'changed setting')} back to the default?`)) {
			return;
		}

		store.set({ config: withoutChanges(config) });

		// The button disables itself once nothing is changed, which would drop the focus, so it moves to the tab above, which a phone does not answer with a keyboard.
		document.getElementById(tabId(store.get().selection?.kind === 'option' ? 'option' : 'rule'))?.focus();
	};

	return (
		<Row>
			<HiddenLabel htmlFor="filter">Filter by ID or name</HiddenLabel>

			<Filter
				type="search"
				id="filter"
				autoComplete="off"
				spellcheck={false}
				placeholder={onRules ? language.ruleFilterPlaceholder : 'Filter options by key'}
				value={state.query}
				onInput={(event) => store.set({ query: event.currentTarget.value })}
			/>

			<CheckLabel htmlFor="changed-only">
				<input
					type="checkbox"
					id="changed-only"
					checked={state.changedOnly}
					onChange={(event) => store.set({ changedOnly: event.currentTarget.checked })}
				/>
				Changed only
			</CheckLabel>

			<Button type="button" title="Put every rule and option back to its default" disabled={total === 0} onClick={reset}>
				Reset all
			</Button>

			<Count role="status">{summaryText(hasLevels(language) ? state.config.level : null, total)}</Count>
		</Row>
	);
}
