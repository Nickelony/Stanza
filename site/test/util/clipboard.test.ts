// @vitest-environment happy-dom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { copyText, saveFile } from '../../src/util/clipboard.ts';

/** Gives the page what the platform lacks or what the test must watch. */
function stub<T extends object>(target: T, name: string, value: unknown): void {
	Object.defineProperty(target, name, { value, configurable: true, writable: true });
}

afterEach(() => {
	vi.restoreAllMocks();
	vi.useRealTimers();
	document.body.replaceChildren();
});

describe('copying text', () => {
	const copied: string[] = [];

	let writeText: ReturnType<typeof vi.fn>;
	let execCommand: ReturnType<typeof vi.fn>;

	beforeEach(() => {
		copied.length = 0;
		writeText = vi.fn(() => Promise.resolve());

		// The fallback copies from a text area, so the stub reads it while the text area is still in the page.
		execCommand = vi.fn(() => {
			copied.push(document.querySelector('textarea')?.value ?? '');
			return true;
		});

		stub(navigator, 'clipboard', { writeText });
		stub(document, 'execCommand', execCommand);
	});

	it('goes through the clipboard API where the page may use it', async () => {
		await expect(copyText('a = 1')).resolves.toBe(true);
		expect(writeText).toHaveBeenCalledWith('a = 1');
		expect(execCommand).not.toHaveBeenCalled();
	});

	it('falls back to a hidden text area when the API refuses, and removes the text area', async () => {
		writeText.mockRejectedValue(new DOMException('denied', 'NotAllowedError'));

		await expect(copyText('a = 1')).resolves.toBe(true);
		expect(execCommand).toHaveBeenCalledWith('copy');
		expect(copied).toEqual(['a = 1']);
		expect(document.querySelector('textarea')).toBeNull();
	});

	it('says false when the fallback fails, and still removes the text area', async () => {
		writeText.mockRejectedValue(new DOMException('denied', 'NotAllowedError'));

		execCommand.mockImplementation(() => {
			throw new Error('unsupported');
		});

		await expect(copyText('a = 1')).resolves.toBe(false);
		expect(document.querySelector('textarea')).toBeNull();
	});

	it('says false when the fallback reports that nothing was copied', async () => {
		writeText.mockRejectedValue(new DOMException('denied', 'NotAllowedError'));
		execCommand.mockReturnValue(false);

		await expect(copyText('a = 1')).resolves.toBe(false);
	});
});

describe('saving a file', () => {
	it('clicks a link that downloads the text under the name, then revokes the address', async () => {
		vi.useFakeTimers();

		const blobs: Blob[] = [];
		const createObjectURL = vi.fn((blob: Blob) => (blobs.push(blob), 'blob:test'));
		const revokeObjectURL = vi.fn();
		const clicked: { download: string; href: string }[] = [];

		stub(URL, 'createObjectURL', createObjectURL);
		stub(URL, 'revokeObjectURL', revokeObjectURL);

		// The link leaves the page after the click, so the stub records it while it is there.
		vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
			clicked.push({ download: this.download, href: this.href });
		});

		saveFile('.editorconfig', 'root = true');

		expect(clicked).toEqual([{ download: '.editorconfig', href: 'blob:test' }]);
		expect(document.querySelector('a')).toBeNull();
		expect(blobs[0]?.type).toBe('application/octet-stream');
		expect(await blobs[0]?.text()).toBe('root = true');

		// The browser starts the download after the click returns, so the address stays valid for a moment.
		expect(revokeObjectURL).not.toHaveBeenCalled();

		vi.advanceTimersByTime(1000);

		expect(revokeObjectURL).toHaveBeenCalledWith('blob:test');
	});
});
