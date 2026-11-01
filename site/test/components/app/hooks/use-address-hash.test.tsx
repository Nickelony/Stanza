// @vitest-environment happy-dom
import { cleanup, render } from '@testing-library/preact';
import { act } from 'preact/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useAddressHash } from '../../../../src/components/app/hooks/use-address-hash.ts';
import { csharp } from '../../../../src/languages/csharp.ts';
import { emptyConfig } from '../../../../src/state/config.ts';
import { PageProvider } from '../../../../src/state/page-context.tsx';
import { createStore, type Store } from '../../../../src/state/store.ts';
import { sample } from '../../../fixtures.ts';

function Probe() {
	useAddressHash();
	return null;
}

/** Mounts the hook over a store that starts at the default configuration with nothing selected. */
function mount(): Store {
	const store = createStore({ config: emptyConfig(), selection: null, query: '', changedOnly: false, variant: 0 });

	render(
		<PageProvider page={{ language: csharp, catalog: sample, store }}>
			<Probe />
		</PageProvider>,
	);

	return store;
}

const clearAddress = (): void => window.history.replaceState(null, '', window.location.pathname);

beforeEach(clearAddress);

afterEach(() => {
	cleanup();
	vi.restoreAllMocks();
	clearAddress();
});

describe('the address hash', () => {
	it('is rewritten when the configuration changes', () => {
		const store = mount();

		store.set({ config: { ...emptyConfig(), level: 'strong' } });

		expect(window.location.hash).toBe('#level=strong');
	});

	it('is left alone when only the filter changes', () => {
		const store = mount();
		const replace = vi.spyOn(window.history, 'replaceState');

		store.set({ query: 'blank', changedOnly: true });

		expect(replace).not.toHaveBeenCalled();
		expect(window.location.hash).toBe('');
	});

	it('patches the store when the visitor edits the address', () => {
		const store = mount();

		window.history.replaceState(null, '', '#level=minimal');
		window.dispatchEvent(new Event('hashchange'));

		expect(store.get().config.level).toBe('minimal');
	});

	it('stops reading the address once the page unmounts', () => {
		const store = mount();

		act(cleanup);

		window.history.replaceState(null, '', '#level=minimal');
		window.dispatchEvent(new Event('hashchange'));

		expect(store.get().config.level).toBe('moderate');
	});

	it('stops writing the address once the page unmounts', () => {
		const store = mount();
		act(cleanup);
		store.set({ config: { ...emptyConfig(), level: 'strong' } });
		expect(window.location.hash).toBe('');
	});
});
