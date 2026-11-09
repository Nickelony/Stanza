import { usePage } from '../../../hooks/use-page.ts';
import { followLink } from '../../../state/follow-link.ts';
import { selectionOf } from '../../../state/selection.ts';

/** A link inside a page to another rule or option selects it here instead of leaving for the wiki. */
export function useSelectingLinks() {
	const { catalog, store } = usePage();

	return (event: MouseEvent): void => {
		if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey) {
			return;
		}

		const link = (event.target as HTMLElement).closest<HTMLAnchorElement>('a[data-select]');
		const selected = link?.dataset.select ? selectionOf(catalog, link.dataset.select) : null;

		if (selected) {
			event.preventDefault();
			followLink(catalog, store, selected);
		}
	};
}
