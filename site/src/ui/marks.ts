import { h } from './dom.ts';

const storageKey = 'stanza-marks';

/** Whether the preview marks the lines a rule adds or removes; marked unless the visitor chose otherwise. */
function stored(): boolean {
	try {
		return window.localStorage.getItem(storageKey) !== 'off';
	} catch {
		return true;
	}
}

function applyMarks(marked: boolean): void {
	document.documentElement.dataset.marks = marked ? 'on' : 'off';
}

/** Remembers the choice where storage allows, and applies it either way. */
function chooseMarks(marked: boolean): void {
	try {
		window.localStorage.setItem(storageKey, marked ? 'on' : 'off');
	} catch {
		// Storage can be blocked, and the choice then lasts for the visit only.
	}

	applyMarks(marked);
}

/** Builds the header checkbox that shows the code with or without the change marks. */
export function createMarksToggle(): HTMLElement {
	const marked = stored();
	const input = h('input', { type: 'checkbox', checked: marked });

	input.addEventListener('change', () => chooseMarks(input.checked));
	applyMarks(marked);

	return h('label', { class: 'check', title: 'Unchecked, the preview shows the code alone, blank lines as they are' }, input, 'Mark changes');
}
