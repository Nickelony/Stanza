/** Reads the text a highlighted line shows: its markup without the tags, with the characters the build escaped restored. */
export function lineText(html: string): string {
	return html
		.replace(/<[^>]+>/g, '')
		.replaceAll('&lt;', '<')
		.replaceAll('&gt;', '>')
		.replaceAll('&quot;', '"')
		.replaceAll('&amp;', '&');
}
