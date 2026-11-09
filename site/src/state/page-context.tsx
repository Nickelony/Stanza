import { createContext, type ComponentChildren } from 'preact';

import type { Language } from '../languages/language.ts';

import type { SiteCatalog } from './model.ts';
import type { Store } from './store.ts';

/** What every component of a page reads: the language it shows, the catalog of that language and the state of the form. */
export interface Page {
	language: Language;
	catalog: SiteCatalog;
	store: Store;
}

export const PageContext = createContext<Page | null>(null);

export function PageProvider({ page, children }: { page: Page; children: ComponentChildren }) {
	return <PageContext.Provider value={page}>{children}</PageContext.Provider>;
}
