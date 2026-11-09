import { useStore } from '../../hooks/use-page.ts';
import { isSelected, selecting } from '../../state/store.ts';
import { scrollToPreview } from '../../util/scrolling.ts';

import { Item, Main } from './parts.ts';
import type { ListRowProps, RowButtonProps } from './types.ts';

/** The row of a list: it selects its rule or option when it takes the focus, and the controls inside it are its children. */
export function ListRow({ selection, selected, changed, shown, children }: ListRowProps) {
	const store = useStore();

	const selectOnFocus = (): void => {
		if (!isSelected(store.get(), selection)) {
			store.set(selecting(selection));
		}
	};

	return (
		<Item data-id={selection.id} hidden={!shown} $selected={selected} $changed={changed} onFocus={selectOnFocus}>
			{children}
		</Item>
	);
}

/** The button that names a row: it selects the row on a click, and the arrow keys move the focus along the list. */
export function RowButton({ selection, title, selected, anchored, list, children }: RowButtonProps) {
	const store = useStore();

	const choose = (): void => {
		store.set(selecting(selection));
		scrollToPreview();
	};

	return (
		<Main
			type="button"
			title={title}
			aria-current={selected ? 'true' : 'false'}
			tabIndex={anchored ? 0 : -1}
			onClick={choose}
			onKeyDown={(event) => list.navigate(event, selection.id)}
		>
			{children}
		</Main>
	);
}
