import { useContext } from 'preact/hooks';

import type { Language } from '../languages/language.ts';
import type { SiteCatalog } from '../state/model.ts';
import { PageContext, type Page } from '../state/page-context.tsx';
import type { Store } from '../state/store.ts';

export function usePage(): Page {
	const page = useContext(PageContext);

	if (page === null) {
		throw new Error('A component of the page was rendered outside its provider.');
	}

	return page;
}

/** The language the page shows. */
export const useLanguage = (): Language => usePage().language;

/** The catalog of that language. */
export const useCatalog = (): SiteCatalog => usePage().catalog;

/** The store the form lives in, for a component that writes to it and does not render from it. */
export const useStore = (): Store => usePage().store;
