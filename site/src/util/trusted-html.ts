import { expandHtml } from './compact.ts';

const expanded = new Map<string, { __html: string }>();

/** The trusted HTML of the catalog as a component takes it, which the build produced from the pages, escaped, and whose links it shortened. */
export function trusted(markup: string): { __html: string } {
	let html = expanded.get(markup);

	if (html === undefined) {
		html = { __html: expandHtml(markup) };
		expanded.set(markup, html);
	}

	return html;
}
