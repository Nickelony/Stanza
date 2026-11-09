import { useRef } from 'preact/hooks';

import { useAppState } from '../../hooks/use-app-state.ts';
import { usePage } from '../../hooks/use-page.ts';
import { firstChangedSelection, firstSelection, isSelectionChanged, type Selection } from '../../state/selection.ts';
import { opening } from '../../state/store.ts';
import { rovingFocus } from '../../util/roving-focus.ts';

import { panelId, tabId } from './ids.ts';
import { Strip, Tab } from './parts.ts';

const tabs = [
	{ kind: 'rule', name: 'rules' },
	{ kind: 'option', name: 'options' },
] as const;

const kinds = tabs.map((tab) => tab.kind);

/** The tabs of the rules and the options; the selection follows the tab, and the tab remembers its last selection. */
export function Tabs() {
	const { catalog, store } = usePage();
	const state = useAppState();
	const remembered = useRef<Partial<Record<Selection['kind'], Selection>>>({});
	const shown = state.selection?.kind === 'option' ? 'option' : 'rule';

	const switchTab = (kind: Selection['kind']): void => {
		const { config, changedOnly, selection: current } = store.get();

		if (current) {
			remembered.current[current.kind] = current;
		}

		const wanted = remembered.current[kind] ?? firstSelection(catalog, kind) ?? current;
		const isChanged = wanted !== null && isSelectionChanged(catalog, config, wanted);

		// Changed only lists the changed rows alone, so the tab opens on one of them when the row it would open on is not changed.
		const target = changedOnly && !isChanged ? (firstChangedSelection(catalog, config, kind) ?? wanted) : wanted;

		store.set(opening(catalog, config, target));
	};

	const roving = rovingFocus(kinds, shown, {
		orientation: 'horizontal',
		wrap: true,

		onMove(kind) {
			switchTab(kind);
			document.getElementById(tabId(kind))?.focus();
		},
	});

	return (
		<Strip role="tablist" aria-label="Rules and options">
			{tabs.map(({ kind, name }) => (
				<Tab
					key={kind}
					type="button"
					role="tab"
					id={tabId(kind)}
					aria-controls={panelId(kind)}
					aria-selected={kind === shown}
					tabIndex={kind === roving.anchor ? 0 : -1}
					onClick={() => switchTab(kind)}
					onKeyDown={(event) => roving.onKeyDown(event, kind)}
				>
					{name === 'rules' ? `Rules (${catalog.rules.length})` : `Options (${catalog.options.length})`}
				</Tab>
			))}
		</Strip>
	);
}
