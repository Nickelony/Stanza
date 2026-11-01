/** Matches the comments that annotate the line below them for a tool, which do not introduce a group. */
const toolDirective =
	/^\s*(?:@ts-(?:expect-error|ignore|nocheck|check)|eslint[- ]|istanbul |c8 |v8 |prettier-ignore|biome-ignore|oxlint-|@__PURE__|#__PURE__|@vite-ignore|webpackChunkName)/;

/** Tells whether the text of a comment is a tool directive, such as `@ts-expect-error` or `eslint-disable-next-line`. */
export function isToolDirective(text: string): boolean {
	return toolDirective.test(text);
}
