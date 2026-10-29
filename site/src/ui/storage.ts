/** Reads a value from local storage, or null when it is missing or storage is blocked. */
export function readStored(key: string): string | null {
	try {
		return window.localStorage.getItem(key);
	} catch {
		return null;
	}
}

/** Remembers a value where storage allows; blocked storage keeps the choice for the visit only. */
export function writeStored(key: string, value: string): void {
	try {
		window.localStorage.setItem(key, value);
	} catch {
		// Storage can be blocked, and the choice then lasts for the visit only.
	}
}
