import logoUrl from '../../icon/Stanza.png';
import { changedCount, decodeHash, emptyConfig, encodeHash, type Selection } from './config.ts';
import type { SiteCatalog } from './model.ts';
import { type AppState, createStore } from './store.ts';
import { h } from './ui/dom.ts';
import { createMarksToggle } from './ui/marks.ts';
import { createOptionList } from './ui/optionlist.ts';
import { createOutput } from './ui/output.ts';
import { createPreview } from './ui/preview.ts';
import { createRuleList, revealRule } from './ui/rulelist.ts';
import { createThemeToggle } from './ui/theme.ts';

const tabs = ['rules', 'options'] as const;

/** Builds the page inside the root element and keeps it in step with the address hash. */
export function mountApp(root: HTMLElement, catalog: SiteCatalog): void {
	const fromHash = decodeHash(catalog, window.location.hash);
	const first: Selection | null = catalog.rules[0] ? { kind: 'rule', id: catalog.rules[0].id } : null;
	const selection = fromHash.selection ?? first;

	const store = createStore({
		config: fromHash.config,
		selection,
		query: '',
		changedOnly: false,
		variant: 0,
	});

	const ruleList = createRuleList(catalog, store);
	const optionList = createOptionList(catalog, store);
	const preview = createPreview(catalog, store);
	const output = createOutput(catalog, store);
	const remembered: Partial<Record<'rule' | 'option', Selection>> = {};

	// --- The left pane
	const filter = h('input', { type: 'search', id: 'filter', class: 'filter', autocomplete: 'off', spellcheck: false });
	const changedOnly = h('input', { type: 'checkbox', id: 'changed-only' });
	const count = h('span', { class: 'count', role: 'status' });
	const reset = h('button', { type: 'button', class: 'btn', title: 'Put every rule and option back to its default' }, 'Reset all');

	const tabButtons = tabs.map((name) => {
		const button = h(
			'button',
			{ type: 'button', class: 'tab', role: 'tab', id: `tab-${name}` },
			name === 'rules' ? `Rules (${catalog.rules.length})` : `Options (${catalog.options.length})`,
		);

		button.setAttribute('aria-controls', `panel-${name}`);
		button.addEventListener('click', () => switchTab(name));

		button.addEventListener('keydown', (event) => {
			if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') {
				return;
			}

			const next = tabs[(tabs.indexOf(name) + (event.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length]!;

			event.preventDefault();
			switchTab(next);
			document.getElementById(`tab-${next}`)?.focus();
		});

		return button;
	});

	const rulesPanel = h('div', { class: 'panel', role: 'tabpanel', id: 'panel-rules', aria: { labelledby: 'tab-rules' } }, ruleList.element);
	const optionsPanel = h('div', { class: 'panel', role: 'tabpanel', id: 'panel-options', aria: { labelledby: 'tab-options' } }, optionList.element);

	function switchTab(name: (typeof tabs)[number]): void {
		const kind = name === 'rules' ? 'rule' : 'option';
		const current = store.get().selection;

		if (current) {
			remembered[current.kind] = current;
		}

		const target =
			remembered[kind] ??
			(kind === 'rule' ? catalog.rules[0] && { kind, id: catalog.rules[0].id } : catalog.options[0] && { kind, id: catalog.options[0].key });

		store.set({ selection: target ?? current, variant: 0, query: '' });
	}

	// The address is replaced rather than pushed, so Back cannot undo a reset; the visitor confirms it first.
	reset.addEventListener('click', () => {
		const config = store.get().config;
		const total = changedCount(catalog, config);

		if (!window.confirm(`Put ${total === 1 ? 'the 1 changed setting' : `all ${total} changed settings`} back to the default?`)) {
			return;
		}

		store.set({ config: { ...emptyConfig(), everySetting: config.everySetting, markupSection: config.markupSection } });
	});

	filter.addEventListener('input', () => store.set({ query: filter.value }));
	changedOnly.addEventListener('change', () => store.set({ changedOnly: changedOnly.checked }));

	const left = h(
		'div',
		{ class: 'pane pane-left' },
		h('p', { class: 'intro' }, 'Choose a severity for each rule and a value for each option. The preview and the .editorconfig follow your choices.'),
		h('div', { class: 'tablist', role: 'tablist', aria: { label: 'Rules and options' } }, ...tabButtons),
		h(
			'div',
			{ class: 'tools' },
			h('label', { class: 'sr-only', htmlFor: 'filter' }, 'Filter by ID or name'),
			filter,
			h('label', { class: 'check', htmlFor: 'changed-only' }, changedOnly, 'Changed only'),
			reset,
			count,
		),
		rulesPanel,
		optionsPanel,
	);

	const right = h(
		'div',
		{ class: 'pane pane-right' },
		h('section', { class: 'preview-pane', aria: { label: 'Preview' } }, preview.element),
		output.element,
	);

	const header = h(
		'header',
		{ class: 'top' },
		h('h1', {}, h('img', { class: 'logo', src: logoUrl, alt: '', width: 22, height: 22 }), 'Stanza rule configurator'),
		h(
			'nav',
			{ class: 'links', aria: { label: 'Project' } },
			h('a', { href: catalog.repoUrl, target: '_blank', rel: 'noopener' }, 'Repository'),
			h('a', { href: catalog.wikiHomeUrl, target: '_blank', rel: 'noopener' }, 'Wiki'),
			createMarksToggle(),
			createThemeToggle(),
		),
	);

	root.replaceChildren(header, h('main', { class: 'layout' }, left, right));

	// --- State to view
	const render = (state: AppState): void => {
		const onRules = state.selection?.kind !== 'option';

		tabButtons.forEach((button, index) => {
			const selected = (tabs[index] === 'rules') === onRules;

			button.setAttribute('aria-selected', String(selected));
			button.tabIndex = selected ? 0 : -1;
		});

		rulesPanel.hidden = !onRules;
		optionsPanel.hidden = onRules;
		filter.placeholder = onRules ? 'Filter rules by ID or title' : 'Filter options by key';

		if (filter.value !== state.query) {
			filter.value = state.query;
		}

		changedOnly.checked = state.changedOnly;

		ruleList.update(state);
		optionList.update(state);
		preview.update(state);
		output.update(state);

		const total = changedCount(catalog, state.config);

		count.textContent = total === 0 ? 'No change' : `${total} changed`;
		reset.disabled = total === 0;
	};

	let lastHash = window.location.hash.replace(/^#/, '');

	const writeHash = (state: AppState): void => {
		const hash = encodeHash(catalog, state);

		// Typing in the filter changes the state but not the address, and a rewrite of the address is not free.
		if (hash === lastHash) {
			return;
		}

		lastHash = hash;

		try {
			window.history.replaceState(null, '', hash === '' ? window.location.pathname + window.location.search : `#${hash}`);
		} catch {
			// A sandboxed frame can refuse to rewrite the address; the page still works.
		}
	};

	store.subscribe(render);
	store.subscribe(writeHash);

	render(store.get());

	window.addEventListener('hashchange', () => {
		if (window.location.hash.replace(/^#/, '') === lastHash) {
			return;
		}

		const next = decodeHash(catalog, window.location.hash);

		store.set({
			config: next.config,
			selection: next.selection ?? store.get().selection,
		});
	});

	// A link inside a page to another rule or option selects it here instead of leaving for the wiki.
	root.addEventListener('click', (event) => {
		if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey) {
			return;
		}

		const link = (event.target as HTMLElement).closest<HTMLAnchorElement>('a[data-select]');
		const id = link?.dataset.select;

		if (!id) {
			return;
		}

		const kind = catalog.rules.some((rule) => rule.id === id) ? 'rule' : catalog.options.some((option) => option.key === id) ? 'option' : null;

		if (!kind) {
			return;
		}

		event.preventDefault();
		store.set({ selection: { kind, id }, variant: 0, query: '', changedOnly: false });

		if (kind === 'rule') {
			revealRule(ruleList, id);
		} else {
			optionList.element.querySelector(`[data-id="${id}"]`)?.scrollIntoView({ block: 'nearest' });
		}
	});

	window.requestAnimationFrame(() => {
		if (selection?.kind === 'rule') {
			revealRule(ruleList, selection.id);
		}
	});
}
