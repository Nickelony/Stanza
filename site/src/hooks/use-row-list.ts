import type { RefObject } from 'preact';

import { rowElement } from '../components/row-list/row-element.ts';
import { matchesFilter, tokenize } from '../state/filter.ts';
import type { Selection } from '../state/selection.ts';
import type { AppState } from '../state/store.ts';
import { rovingFocus } from '../util/roving-focus.ts';

/** What a list needs to know of one row to decide whether the row shows. */
export interface RowFacts {
	id: string;

	/** The lowercase text the filter words must appear in. */
	haystack: string;
	changed: boolean;

	/** Whether something besides the filter lets the row show; a row the formatter hides never does. */
	allowed?: boolean;
}

export interface RowList {
	/** The rows in view, in the order of the list. */
	inView: readonly string[];

	/** The one row whose controls are in the tab order. */
	anchor: string | undefined;

	isShown(id: string): boolean;

	/** Moves the focus along the rows in view when the key is an arrow, Home or End, and leaves any other key to the browser. */
	navigate(event: KeyboardEvent, id: string): void;
}

/** The ID of the row that holds the focus, which stays in view so that setting it back to the default does not drop the focus onto the page. */
function focusedRowId(list: HTMLElement | null): string | undefined {
	const active = document.activeElement;
	return list?.contains(active) ? (active?.closest<HTMLElement>('li[data-id]')?.dataset.id ?? undefined) : undefined;
}

/** Moves the focus to the button that names a row. */
function focusRowButton(list: HTMLElement | null, id: string): void {
	rowElement(list, id)?.querySelector('button')?.focus();
}

/** Decides which rows of a list show under the filter, and which of them the tab key stops at. */
export function useRowList(kind: Selection['kind'], rows: readonly RowFacts[], state: AppState, list: RefObject<HTMLElement | null>): RowList {
	const tokens = tokenize(state.query);
	const kept = focusedRowId(list.current);

	const inView = rows
		.filter((row) => row.id === kept || ((row.allowed ?? true) && matchesFilter(row.haystack, tokens, state.changedOnly, row.changed)))
		.map((row) => row.id);

	const shown = new Set(inView);
	const selected = state.selection?.kind === kind ? state.selection.id : undefined;
	const roving = rovingFocus(inView, selected, { orientation: 'vertical', onMove: (id) => focusRowButton(list.current, id) });

	return { inView, anchor: roving.anchor, isShown: (id) => shown.has(id), navigate: roving.onKeyDown };
}
