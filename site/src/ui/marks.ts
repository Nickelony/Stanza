import { h } from './dom.ts';
import { readStored, writeStored } from './storage.ts';

const storageKey = 'stanza-marks';

/** Whether the preview marks the lines a rule adds or removes; marked unless the visitor chose otherwise. */
function stored(): boolean {
	return readStored(storageKey) !== 'off';
}

function applyMarks(marked: boolean): void {
	document.documentElement.dataset.marks = marked ? 'on' : 'off';
}

/** Remembers the choice where storage allows, and applies it either way. */
function chooseMarks(marked: boolean): void {
	writeStored(storageKey, marked ? 'on' : 'off');
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
