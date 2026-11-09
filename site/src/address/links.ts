export const repoUrl = 'https://github.com/Nickelony/Stanza';
export const wikiUrl = `${repoUrl}/wiki`;

/** What every link of a page carries; the catalog leaves it out and `expandLinks` writes it back. */
const external = ' target="_blank" rel="noopener"';

/** Stands for the wiki address at the start of a link of the catalog. */
const wikiMark = '~/';

/** The ID of the rule or the key of the option that the wiki page named by this path belongs to, or `undefined` for any other page. */
function selectedId(slug: string): string | undefined {
	return /^(ZA\d{4})-/.exec(slug)?.[1] ?? /^(stanza_[a-z0-9_]+)(?:#.*)?$/.exec(slug)?.[1];
}

/** The address of a file of the repository on GitHub, where the JS and Python docs live. */
export const blobUrl = `${repoUrl}/blob/main`;

/** The ID or name of the rule that the page under `js/docs/rules` or `py/docs/rules` at this address belongs to. */
function guideRuleId(url: string): string | undefined {
	return (
		new RegExp(`^${blobUrl}/js/docs/rules/([a-z-]+)\\.md(?:#.*)?$`).exec(url)?.[1] ??
		new RegExp(`^${blobUrl}/py/docs/rules/(ZA3\\d{3})-[a-z-]+\\.md(?:#.*)?$`).exec(url)?.[1]
	);
}

/** The attributes of a link in a page: it opens in a new tab, and a link to a rule or an option also selects it in place. */
export function pageLinkAttributes(url: string): string {
	const id = url.startsWith(`${wikiUrl}/`) ? selectedId(url.slice(wikiUrl.length + 1)) : guideRuleId(url);
	return `${external}${id === undefined ? '' : ` data-select="${id}"`}`;
}

const fullAnchor = /<a href="([^"]*)" target="_blank" rel="noopener"(?: data-select="[^"]*")?>/g;
const compactAnchor = /<a href="([^"]*)">/g;

/**
 * Shortens each link of a page to its address, with the wiki address replaced by a mark, because the catalog holds hundreds of them.
 * It fails on a link that `expandLinks` would not write back as it stood, so a link is never changed on the way.
 */
export function compactLinks(html: string): string {
	return html.replace(fullAnchor, (anchor, href: string) => {
		const compact = `<a href="${href.startsWith(`${wikiUrl}/`) ? wikiMark + href.slice(wikiUrl.length + 1) : href}">`;

		if (expandLinks(compact) !== anchor) {
			throw new Error(`The link ${anchor} cannot be written in the compact form.`);
		}

		return compact;
	});
}

/** Writes the attributes back on each link that `compactLinks` shortened. */
export function expandLinks(html: string): string {
	if (!html.includes('<a href="')) {
		return html;
	}

	return html.replace(compactAnchor, (_anchor, href: string) => {
		const slug = href.startsWith(wikiMark) ? href.slice(wikiMark.length) : undefined;
		const expanded = slug === undefined ? href : `${wikiUrl}/${slug}`;

		return `<a href="${expanded}"${pageLinkAttributes(expanded)}>`;
	});
}
