import { useLayoutEffect } from 'preact/hooks';

import { useAppState } from '../../../hooks/use-app-state.ts';
import { rowElement } from '../../row-list/row-element.ts';
import { panelId } from '../../tabs/ids.ts';

/** Scrolls the row of the selected rule or option into the list, which a link or a chip may have moved it out of. */
export function useRevealedRow() {
	const { selection } = useAppState();

	useLayoutEffect(() => {
		if (selection) {
			const panel = document.getElementById(panelId(selection.kind));
			rowElement(panel, selection.id)?.scrollIntoView({ block: 'nearest' });
		}
	}, [selection?.kind, selection?.id]);
}
