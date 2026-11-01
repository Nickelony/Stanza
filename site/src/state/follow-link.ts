import type { SiteCatalog } from './model.ts';
import type { Selection } from './selection.ts';
import { opening, type Store } from './store.ts';

/** Selects a rule or an option from a link in the preview, which renders the preview anew, and moves the focus to the new heading. */
export function followLink(catalog: SiteCatalog, store: Store, selection: Selection): void {
	store.set(opening(catalog, store.get().config, selection));

	// The preview renders on the next microtask, so the new heading exists only after it.
	queueMicrotask(() => document.querySelector<HTMLElement>('#preview h2')?.focus());
}
