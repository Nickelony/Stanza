/** The arrow keys that step along a vertical list and along a horizontal strip. */
const steps = {
	vertical: { next: 'ArrowDown', previous: 'ArrowUp' },
	horizontal: { next: 'ArrowRight', previous: 'ArrowLeft' },
} as const;

export type Orientation = keyof typeof steps;

export interface RovingOptions<Id extends string> {
	/** The arrow keys that step: up and down along a list, left and right along a strip. */
	orientation: Orientation;

	/** Whether a step past an end goes round to the other end, as it does along a strip of tabs. */
	wrap?: boolean;

	/** Moves the focus, and whatever follows it, to the item a key chose. */
	onMove(id: Id): void;
}

export interface RovingFocus<Id extends string> {
	/** The one item left in the tab order. */
	anchor: Id | undefined;

	/** Moves along the items when the key is an arrow, Home or End, and leaves any other key to the browser. */
	onKeyDown(event: KeyboardEvent, id: Id): void;
}

/** Tells whether the key moves the focus along the items, so the page keeps it from scrolling. */
export function isNavigationKey(key: string, orientation: Orientation = 'vertical'): boolean {
	return key === 'Home' || key === 'End' || key === steps[orientation].next || key === steps[orientation].previous;
}

/** The item a navigation key moves the focus to from one of the items, or `undefined` for a key that moves none or a move past an end. */
export function movedId<Id extends string>(
	key: string,
	ids: readonly Id[],
	from: Id,
	orientation: Orientation = 'vertical',
	wrap = false,
): Id | undefined {
	const at = ids.indexOf(from);

	switch (key) {
		case 'Home':
			return ids[0];

		case 'End':
			return ids[ids.length - 1];

		case steps[orientation].next:
			return ids[wrap ? (at + 1) % ids.length : at + 1];

		case steps[orientation].previous:
			return ids[wrap ? (at + ids.length - 1) % ids.length : at - 1];

		default:
			return undefined;
	}
}

/** The one item left in the tab order: the selected one while it is among the items, and otherwise the first. */
export function anchorId<Id extends string>(ids: readonly Id[], selected: Id | undefined): Id | undefined {
	return ids.find((id) => id === selected) ?? ids[0];
}

/** One tab stop for a row of items, and the arrow, Home and End keys to move the focus along them. */
export function rovingFocus<Id extends string>(
	ids: readonly Id[],
	selected: Id | undefined,
	{ orientation, wrap = false, onMove }: RovingOptions<Id>,
): RovingFocus<Id> {
	return {
		anchor: anchorId(ids, selected),

		onKeyDown(event, id) {
			if (!isNavigationKey(event.key, orientation)) {
				return;
			}

			event.preventDefault();

			const target = movedId(event.key, ids, id, orientation, wrap);

			if (target !== undefined) {
				onMove(target);
			}
		},
	};
}
