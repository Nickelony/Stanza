import { useLayoutEffect, useState } from 'preact/hooks';

import { readStored, writeStored } from '../util/storage.ts';

export interface StoredToggle<V extends string> {
	/** The key the choice is kept under in local storage. */
	key: string;

	/** The `data-` attribute of the root element that carries the choice, which the styles read. */
	attribute: string;

	/** The two values, which the toggle switches between. */
	values: readonly [V, V];

	/** The value to start from, given the one kept from an earlier visit, or `null` when there is none or it is none of the two. */
	initial(stored: V | null): V;
}

/** A choice between two values that lasts across visits: kept in local storage, and written to an attribute of the root element. */
export function useStoredToggle<V extends string>({ key, attribute, values, initial }: StoredToggle<V>): [V, () => void] {
	const [value, setValue] = useState<V>(() => {
		const stored = readStored(key);
		return initial(values.find((candidate) => candidate === stored) ?? null);
	});

	useLayoutEffect(() => {
		document.documentElement.dataset[attribute] = value;
	}, [attribute, value]);

	const toggle = (): void => {
		const next = value === values[0] ? values[1] : values[0];

		writeStored(key, next);
		setValue(next);
	};

	return [value, toggle];
}
