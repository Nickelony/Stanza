import { render } from 'preact';

import { decodeHash } from '../address/hash.ts';
import { App } from '../components/app/app.tsx';
import type { Language } from '../languages/language.ts';
import type { SiteCatalog } from '../state/model.ts';
import { PageProvider } from '../state/page-context.tsx';
import { firstSelection } from '../state/selection.ts';
import { createStore } from '../state/store.ts';
import { addressHash } from '../util/address-hash.ts';

import '../style.css';

/** Renders the page of a language into the element that holds it, with the form read from the address. */
export function startPage(language: Language, catalog: SiteCatalog, tokenCss: string): void {
	const root = document.getElementById('app');

	if (!root) {
		throw new Error('The page has no #app element.');
	}

	const style = document.createElement('style');

	style.textContent = tokenCss;
	document.head.append(style);

	const fromHash = decodeHash(catalog, addressHash(), language);

	const store = createStore({
		config: fromHash.config,
		selection: fromHash.selection ?? firstSelection(catalog, 'rule'),
		query: '',
		changedOnly: false,
		variant: 0,
	});

	render(
		<PageProvider page={{ language, catalog, store }}>
			<App />
		</PageProvider>,

		root,
	);
}
