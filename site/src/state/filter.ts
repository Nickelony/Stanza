/** Splits the filter text into the lowercase words a row must contain. */
export function tokenize(query: string): string[] {
	return query
		.toLowerCase()
		.split(/\s+/)
		.filter((token) => token !== '');
}

/** Tells whether a row is listed under the filter: the words are in its text, and a changed row only when Changed only is on. */
export function matchesFilter(haystack: string, tokens: readonly string[], changedOnly: boolean, changed: boolean): boolean {
	return tokens.every((token) => haystack.includes(token)) && (!changedOnly || changed);
}
