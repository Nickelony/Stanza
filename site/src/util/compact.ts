import { compactLinks, expandLinks } from '../address/links.ts';

const fullToken = /<span class="(t\d+)">([^<]*)<\/span>/g;
const compactToken = /<(t\d+)>([^<]*)<\/t>/g;

/**
 * The form of the HTML in the catalog: a link keeps only its address, and a highlighted token is `<t3>text</t>`.
 * The catalog holds thousands of both, so the chunk the browser parses is smaller, and `expandHtml` writes each back before the page uses it.
 * It fails on markup that `expandHtml` would not write back as it stood, so the page never shows a changed link or token.
 */
export function compactHtml(html: string): string {
	const compact = compactLinks(html).replace(fullToken, '<$1>$2</t>');

	if (expandHtml(compact) !== html) {
		throw new Error(`The markup cannot be written in the compact form: ${html.slice(0, 80)}`);
	}

	return compact;
}

/** Writes the links and the tokens back that `compactHtml` shortened. */
export function expandHtml(html: string): string {
	return expandLinks(html).replace(compactToken, '<span class="$1">$2</span>');
}
