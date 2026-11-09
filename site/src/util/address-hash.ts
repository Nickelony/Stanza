/** The address hash without its `#`, which is how the store writes it. */
export const addressHash = (): string => window.location.hash.replace(/^#/, '');

/** Puts the hash in the address in place of the one there, so Back does not step through every pick. */
export function replaceAddressHash(hash: string): void {
	try {
		window.history.replaceState(null, '', hash === '' ? window.location.pathname + window.location.search : `#${hash}`);
	} catch {
		// A sandboxed frame can refuse to rewrite the address; the page still works.
	}
}
