// @vitest-environment happy-dom
import { afterEach, describe, expect, it, vi } from 'vitest';

import { readStored, writeStored } from '../../src/util/storage.ts';

/** Makes the page's storage refuse every call, as a browser does with site data blocked. */
function blockStorage(): void {
	const refuse = (): never => {
		throw new DOMException('blocked', 'SecurityError');
	};

	vi.spyOn(window, 'localStorage', 'get').mockReturnValue({ getItem: refuse, setItem: refuse } as unknown as Storage);
}

afterEach(() => {
	vi.restoreAllMocks();
	window.localStorage.clear();
});

describe('the stored choices', () => {
	it('read back what was written, and read null for a key never written', () => {
		writeStored('theme', 'dark');

		expect(readStored('theme')).toBe('dark');
		expect(readStored('marks')).toBeNull();
	});

	it('read null when storage is blocked', () => {
		writeStored('theme', 'dark');
		blockStorage();

		expect(readStored('theme')).toBeNull();
	});

	it('write nothing and throw nothing when storage is blocked', () => {
		blockStorage();
		expect(() => writeStored('theme', 'dark')).not.toThrow();
		vi.restoreAllMocks();
		expect(readStored('theme')).toBeNull();
	});
});
