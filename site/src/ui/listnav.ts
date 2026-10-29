/** What the rule list and the option list share in a row: the button that selects it and the control that sets it. */
export interface NavRow {
	element: HTMLElement;
	button: HTMLButtonElement;
	control: HTMLSelectElement | HTMLInputElement;
}

type Target = (visible: NavRow[], at: number) => NavRow | undefined;

const targets = new Map<string, Target>([
	['ArrowDown', (visible, at) => visible[at + 1]],
	['ArrowUp', (visible, at) => visible[at - 1]],
	['Home', (visible) => visible[0]],
	['End', (visible) => visible[visible.length - 1]],
]);

/** Splits the filter text into the lowercase words a row must contain. */
export function tokenize(query: string): string[] {
	return query
		.toLowerCase()
		.split(/\s+/)
		.filter((token) => token !== '');
}

/** Moves the focus along the visible rows with the arrow keys, Home and End, as a list of buttons does. */
export function navigate(event: KeyboardEvent, rows: readonly NavRow[], row: NavRow): void {
	const target = targets.get(event.key);

	if (!target) {
		return;
	}

	const visible = rows.filter((candidate) => !candidate.element.hidden);

	event.preventDefault();
	target(visible, visible.indexOf(row))?.button.focus();
}

/** Leaves one row in the tab order, the selected one while it shows and otherwise the first, and returns the rows in view. */
export function anchorRows<R extends NavRow>(rows: readonly R[], isSelected: (row: R) => boolean): R[] {
	const visible = rows.filter((row) => !row.element.hidden);
	const anchor = visible.find(isSelected) ?? visible[0];

	for (const row of rows) {
		row.button.tabIndex = row === anchor ? 0 : -1;
		row.control.tabIndex = row === anchor ? 0 : -1;
	}

	return visible;
}
