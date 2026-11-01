/** The row of a list that `ListRow` wrote for the rule or option with this ID, or `undefined` when the panel has none; an ID is no CSS-safe selector, so the rows are compared, not queried. */
export function rowElement(panel: ParentNode | null, id: string): HTMLElement | undefined {
	return [...(panel?.querySelectorAll<HTMLElement>('li[data-id]') ?? [])].find((row) => row.dataset.id === id);
}
