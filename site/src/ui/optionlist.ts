import { checkOption, isOptionChanged } from '../config.ts';
import type { Option, SiteCatalog, Snippet } from '../model.ts';
import type { AppState, Store } from '../store.ts';
import { h } from './dom.ts';
import { optionDefaultLabel } from './labels.ts';
import { anchorRows, navigate, tokenize } from './listnav.ts';

interface OptionRow {
	key: string;
	element: HTMLLIElement;
	button: HTMLButtonElement;
	control: HTMLInputElement | HTMLSelectElement;
	error: HTMLElement;
	changedNote: HTMLSpanElement;
	option: Option<Snippet>;
}

export interface OptionList {
	element: HTMLElement;
	update(state: AppState): void;
}

const audienceNotes = { analyzer: '', both: '', markup: 'markup only' } as const;

/** Names the default as the dropdowns do; a default too long for the box is left to the tooltip and the preview. */
function placeholder(option: Option<Snippet>): string {
	const words = option.words.length > 0 ? `, or ${option.words.join(', ')}` : '';
	return option.defaultValue !== null && option.defaultValue.length <= 12 ? `Default (${option.defaultValue})${words}` : 'Default';
}

export function createOptionList(catalog: SiteCatalog, store: Store): OptionList {
	const rows: OptionRow[] = [];
	const empty = h('p', { class: 'empty', hidden: true }, 'No option matches the filter.');
	const list = h('ul', { class: 'rows' });
	const element = h('div', { class: 'list', id: 'option-list' }, empty, h('section', { class: 'group', aria: { label: 'Options' } }, list));

	for (const option of catalog.options) {
		const note = audienceNotes[option.audience];
		const changedNote = h('span', { class: 'sr-only' });

		const button = h(
			'button',
			{ type: 'button', class: 'row-main', title: `Default: ${option.defaultText}` },
			// A long key wraps after an underscore rather than running under the control beside it.
			h('span', { class: 'row-id mono' }, ...option.key.split('_').flatMap((part, index) => (index === 0 ? [part] : [h('wbr'), `_${part}`]))),
			changedNote,
			note ? h('span', { class: 'flag' }, note) : null,
		);

		// The message stays in the page and only its text changes, which is what a polite live region announces.
		const error = h('p', { class: 'field-error', id: `error-${option.key}`, aria: { live: 'polite' } });
		let control: HTMLInputElement | HTMLSelectElement;

		const write = (value: string | null): void => {
			const state = store.get();
			const options = { ...state.config.options };

			if (value === null) {
				delete options[option.key];
			} else {
				options[option.key] = value;
			}

			store.set({ config: { ...state.config, options }, selection: { kind: 'option', id: option.key } });
		};

		if (option.kind === 'enum') {
			// The label stays short enough for the box, so the tooltip carries the whole default.
			const select = h(
				'select',
				{ class: 'row-select', title: `Default: ${option.defaultText}` },
				h('option', { value: '' }, optionDefaultLabel(option)),
				...option.choices.map((choice) => h('option', { value: choice }, choice)),
			);

			select.addEventListener('change', () => write(select.value === '' ? null : select.value));
			control = select;
		} else {
			// A number that also takes a word names the word in the placeholder; a suggestion list would draw a dropdown arrow on a text box.
			const input = h('input', {
				type: 'text',
				class: 'row-input mono',
				placeholder: placeholder(option),
				title: `Default: ${option.defaultText}`,
				autocomplete: 'off',
				spellcheck: false,
				inputMode: option.kind === 'number' && option.words.length === 0 ? 'numeric' : 'text',
			});

			input.addEventListener('input', () => {
				const checked = checkOption(option, input.value);

				input.setAttribute('aria-invalid', checked.ok ? 'false' : 'true');
				error.textContent = checked.ok ? '' : checked.reason;

				// A typo leaves the last valid value in the configuration, and the blur below puts it back in the box.
				if (checked.ok) {
					write(checked.value);
				}
			});

			input.addEventListener('blur', () => {
				const checked = checkOption(option, input.value);

				if (!checked.ok) {
					input.value = store.get().config.options[option.key] ?? '';
					input.setAttribute('aria-invalid', 'false');
					error.textContent = '';
				}
			});

			control = input;
		}

		control.setAttribute('aria-label', `Value of ${option.key}`);
		control.setAttribute('aria-describedby', error.id);

		const li = h('li', { class: 'row option-row', dataset: { id: option.key } }, button, control, error);
		const row: OptionRow = { key: option.key, element: li, button, control, error, changedNote, option };

		li.addEventListener('focusin', () => {
			const selection = store.get().selection;

			if (selection?.kind !== 'option' || selection.id !== option.key) {
				store.set({ selection: { kind: 'option', id: option.key }, variant: 0 });
			}
		});

		button.addEventListener('click', () => store.set({ selection: { kind: 'option', id: option.key }, variant: 0 }));

		button.addEventListener('keydown', (event) => navigate(event, rows, row));

		list.append(li);
		rows.push(row);
	}

	const update = (state: AppState): void => {
		const tokens = tokenize(state.query);

		for (const row of rows) {
			const value = state.config.options[row.key];
			const changed = isOptionChanged(row.option, value);
			const visible = tokens.every((token) => row.key.includes(token)) && (!state.changedOnly || changed);
			const selected = state.selection?.kind === 'option' && state.selection.id === row.key;

			row.element.hidden = !visible;

			row.element.classList.toggle('selected', selected);
			row.element.classList.toggle('changed', changed);

			row.button.setAttribute('aria-current', selected ? 'true' : 'false');
			row.changedNote.textContent = changed ? 'changed' : '';

			if (document.activeElement !== row.control && row.control.value !== (value ?? '')) {
				row.control.value = value ?? '';
				row.control.setAttribute('aria-invalid', 'false');
				row.error.textContent = '';
			}
		}

		const inView = anchorRows(rows, (row) => state.selection?.kind === 'option' && state.selection.id === row.key);

		empty.hidden = inView.length > 0;
	};

	return { element, update };
}
