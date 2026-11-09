import { useSyncExternalStore } from 'preact/compat';

import type { AppState } from '../state/store.ts';

import { useStore } from './use-page.ts';

/** The state of the form; the component renders again when it changes. */
export function useAppState(): AppState {
	const store = useStore();
	return useSyncExternalStore(store.subscribe, store.get);
}
