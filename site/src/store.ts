import type { Config, Selection } from './config.ts';

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
	subscribe(listener: (state: AppState) => void): void;
}

export function createStore(initial: AppState): Store {
	let state = initial;
	const listeners: Array<(state: AppState) => void> = [];

	return {
		get: () => state,

		set(patch) {
			state = { ...state, ...patch };

			for (const listener of listeners) {
				listener(state);
			}
		},

		subscribe(listener) {
			listeners.push(listener);
		},
	};
}
