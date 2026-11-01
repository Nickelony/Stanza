import { useEffect } from 'preact/hooks';

import { encodeHash, patchFromHash } from '../../../address/hash.ts';
import { usePage } from '../../../hooks/use-page.ts';
import { addressHash, replaceAddressHash } from '../../../util/address-hash.ts';

/** Writes the configuration into the address as it changes, and reads it back when the visitor edits the address. */
export function useAddressHash(): void {
	const { catalog, language, store } = usePage();

	useEffect(() => {
		let lastHash = addressHash();

		const stop = store.subscribe((state) => {
			const hash = encodeHash(catalog, state, language);

			// Typing in the filter changes the state but not the address, and a rewrite of the address is not free.
			if (hash === lastHash) {
				return;
			}

			lastHash = hash;
			replaceAddressHash(hash);
		});

		const onHashChange = (): void => {
			if (addressHash() !== lastHash) {
				store.set(patchFromHash(catalog, addressHash(), language));
			}
		};

		window.addEventListener('hashchange', onHashChange);

		return () => {
			stop();
			window.removeEventListener('hashchange', onHashChange);
		};
	}, [catalog, language, store]);
}
