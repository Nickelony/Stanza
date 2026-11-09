import type { Config } from './config.ts';
import type { Catalog } from './model.ts';
import { isSelectionChanged, type Selection } from './selection.ts';

export interface AppState {
	config: Config;

	/** What the preview shows; the list the selection belongs to is the one in view. */
	selection: Selection | null;
	query: string;
	changedOnly: boolean;

	/** The index of the After variant the preview shows. */
	variant: number;
}

export interface Store {
	get(): AppState;
	set(patch: Partial<AppState>): void;

	/** Calls the listener after every change; the function it returns stops that. */
	subscribe(listener: (state: AppState) => void): () => void;
}

export function createStore(initial: AppState): Store {
	let state = initial;
	const listeners: Array<(state: AppState) => void> = [];

	return {
		get: () => state,

		set(patch) {
			state = { ...state, ...patch };

			for (const listener of listeners.slice()) {
				listener(state);
			}
		},

		subscribe(listener) {
			listeners.push(listener);

			return () => {
				listeners.splice(listeners.indexOf(listener), 1);
			};
		},
	};
}

/** Changes some fields of the configuration and leaves the others as they are. */
export function setConfig(store: Store, change: Partial<Config>): void {
	store.set({ config: { ...store.get().config, ...change } });
}

/** The patch that shows a selection from its first variant, which every way of picking a rule or an option writes. */
export function selecting(selection: Selection | null): Partial<AppState> {
	return { selection, variant: 0 };
}

/** Tells whether the state selects this rule or option. */
export function isSelected(state: AppState, selection: Selection): boolean {
	return state.selection?.kind === selection.kind && state.selection.id === selection.id;
}

/**
 * The patch that opens a selection under a configuration with its row in view.
 * The text filter always clears, and Changed only stays on only while the selection is a changed row, which that filter shows.
 */
export function opening<S>(catalog: Catalog<S>, config: Config, selection: Selection | null): Partial<AppState> {
	const kept = selection !== null && isSelectionChanged(catalog, config, selection);
	return { ...selecting(selection), query: '', ...(kept ? {} : { changedOnly: false }) };
}
