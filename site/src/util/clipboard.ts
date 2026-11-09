/** Puts the text on the clipboard, by the clipboard API where the page may use it and by a hidden text area where it may not. */
export async function copyText(text: string): Promise<boolean> {
	try {
		await navigator.clipboard.writeText(text);
		return true;
	} catch {
		const area = document.createElement('textarea');

		area.value = text;
		area.readOnly = true;
		area.style.position = 'fixed';
		area.style.opacity = '0';

		document.body.append(area);
		area.select();

		try {
			return document.execCommand('copy');
		} catch {
			return false;
		} finally {
			area.remove();
		}
	}
}

/** Hands the text to the browser as a file to save. */
export function saveFile(name: string, text: string): void {
	// A text type makes Chromium add `.txt` to a name that has no known extension, so the type is a generic one.
	const url = URL.createObjectURL(new Blob([text], { type: 'application/octet-stream' }));
	const link = document.createElement('a');

	link.href = url;
	link.download = name;

	document.body.append(link);

	link.click();
	link.remove();

	// The browser starts the download once the click returns, so the address stays valid until then.
	window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
