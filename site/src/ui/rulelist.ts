import { effectiveState, isRuleChanged, settingOf, severities } from '../config.ts';
import type { Severity, SiteCatalog } from '../model.ts';
import type { AppState, Store } from '../store.ts';
import { h } from './dom.ts';
import { defaultLabel, severityLabels, stateLabels } from './labels.ts';
import { anchorRows, navigate, tokenize } from './listnav.ts';

interface RuleRow {
	id: string;
	haystack: string;
	element: HTMLLIElement;
	button: HTMLButtonElement;
	control: HTMLSelectElement;
	dot: HTMLSpanElement;
	changedNote: HTMLSpanElement;
	rule: SiteCatalog['rules'][number];
}

interface Section {
	element: HTMLElement;
	rows: RuleRow[];
}

export interface RuleList {
	element: HTMLElement;
	update(state: AppState): void;
}

export function createRuleList(catalog: SiteCatalog, store: Store): RuleList {
	const sections: Section[] = [];
	const rows: RuleRow[] = [];
	const empty = h('p', { class: 'empty', hidden: true }, 'No rule matches the filter.');
	const element = h('div', { class: 'list', id: 'rule-list' }, empty);

	for (const group of catalog.groups) {
		const heading = h('h3', { class: 'group-title' }, group);
		const list = h('ul', { class: 'rows' });
		const section: Section = { element: h('section', { class: 'group', aria: { label: group } }, heading, list), rows: [] };

		for (const rule of catalog.rules.filter((candidate) => candidate.group === group)) {
			const dot = h('span', { class: 'dot', aria: { hidden: 'true' } });
			const changedNote = h('span', { class: 'sr-only' });

			const button = h(
				'button',
				{ type: 'button', class: 'row-main', title: rule.summary },
				dot,
				h('span', { class: 'row-id' }, rule.id),
				h('span', { class: 'row-title' }, rule.title),
				changedNote,
				rule.onByDefault ? null : h('span', { class: 'flag', title: 'Off by default' }, 'off'),
			);

			const select = h(
				'select',
				{ class: 'row-select', title: defaultLabel(rule) },
				h('option', { value: 'default' }, defaultLabel(rule)),
				...severities.map((severity) => h('option', { value: severity }, severityLabels[severity])),
			);

			select.setAttribute('aria-label', `Severity of ${rule.id}`);

			const li = h('li', { class: 'row', dataset: { id: rule.id } }, button, select);

			const row: RuleRow = {
				id: rule.id,
				haystack: `${rule.id} ${rule.title}`.toLowerCase(),
				element: li,
				button,
				control: select,
				dot,
				changedNote,
				rule,
			};

			select.addEventListener('change', () => {
				const state = store.get();
				const rules = { ...state.config.rules };

				if (select.value === 'default') {
					delete rules[rule.id];
				} else {
					rules[rule.id] = select.value as Severity;
				}

				store.set({ config: { ...state.config, rules }, selection: { kind: 'rule', id: rule.id } });
			});

			li.addEventListener('focusin', () => {
				const selection = store.get().selection;

				if (selection?.kind !== 'rule' || selection.id !== rule.id) {
					store.set({ selection: { kind: 'rule', id: rule.id }, variant: 0 });
				}
			});

			button.addEventListener('click', () => store.set({ selection: { kind: 'rule', id: rule.id }, variant: 0 }));
			button.addEventListener('keydown', (event) => navigate(event, rows, row));

			list.append(li);

			section.rows.push(row);
			rows.push(row);
		}

		sections.push(section);
		element.append(section.element);
	}

	const update = (state: AppState): void => {
		const tokens = tokenize(state.query);

		for (const row of rows) {
			const setting = settingOf(state.config, row.rule);
			const changed = isRuleChanged(setting);

			// A row that holds the focus stays, so setting it back to the default does not drop the focus onto the page.
			const visible =
				(tokens.every((token) => row.haystack.includes(token)) && (!state.changedOnly || changed)) || row.element.contains(document.activeElement);

			const selected = state.selection?.kind === 'rule' && state.selection.id === row.id;
			const effective = effectiveState(row.rule, setting);

			row.element.hidden = !visible;

			row.element.classList.toggle('selected', selected);
			row.element.classList.toggle('changed', changed);

			row.button.setAttribute('aria-current', selected ? 'true' : 'false');
			row.changedNote.textContent = changed ? 'changed' : '';
			row.dot.className = `dot dot-${effective}`;
			row.dot.title = stateLabels[effective];

			if (row.control.value !== setting) {
				row.control.value = setting;
			}
		}

		const inView = anchorRows(rows, (row) => state.selection?.kind === 'rule' && state.selection.id === row.id);

		for (const section of sections) {
			section.element.hidden = section.rows.every((row) => row.element.hidden);
		}

		empty.hidden = inView.length > 0;
	};

	return { element, update };
}

/** Scrolls the row of a rule into view when a link, not the list, selected it. */
export function revealRule(list: RuleList, id: string): void {
	list.element.querySelector(`[data-id="${id}"]`)?.scrollIntoView({ block: 'nearest' });
}
