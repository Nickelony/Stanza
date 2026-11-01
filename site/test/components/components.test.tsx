// @vitest-environment happy-dom
import { cleanup, fireEvent, render, screen, within } from '@testing-library/preact';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { repoUrl } from '../../src/address/links.ts';
import { App } from '../../src/components/app/app.tsx';
import { logos } from '../../src/components/header/logos.ts';
import { csharp } from '../../src/languages/csharp.ts';
import { javascript } from '../../src/languages/javascript.ts';
import type { Language } from '../../src/languages/language.ts';
import { markup } from '../../src/languages/markup.ts';
import { python } from '../../src/languages/python.ts';
import { emptyConfig } from '../../src/state/config.ts';
import { PageProvider } from '../../src/state/page-context.tsx';
import { createStore, type AppState, type Store } from '../../src/state/store.ts';
import { catalog, option, rule } from '../fixtures.ts';

const rules = [rule('ZA0001'), rule('ZA0002', { undoneByCSharpier: true }), rule('ZA0003', { onByDefault: false }), rule('ZA0004')];

const options = [
	option('stanza_max_line_length'),
	option('stanza_multi_line_member_part', { kind: 'enum', choices: ['body', 'either'], defaultValue: 'either' }),
];

const sample = catalog(rules, options);

/** Renders the page of a language, C# unless told otherwise, over the sample catalog, and returns the store that the form lives in. */
function renderPage(initial: Partial<AppState> = {}, language: Language = csharp, shown = sample): Store {
	const store = createStore({
		config: emptyConfig(language.defaultSeverity),
		selection: { kind: 'rule', id: 'ZA0001' },
		query: '',
		changedOnly: false,
		variant: 0,
		...initial,
	});

	render(
		<PageProvider page={{ language, catalog: shown, store }}>
			<App />
		</PageProvider>,
	);

	return store;
}

const rowOf = (id: string): HTMLElement => document.querySelector<HTMLElement>(`li[data-id="${id}"]`)!;
const buttonOf = (id: string): HTMLButtonElement => rowOf(id).querySelector('button')!;
const flush = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 0));
const confirmOfWindow = window.confirm;

afterEach(() => {
	cleanup();
	window.confirm = confirmOfWindow;
	delete document.documentElement.dataset.theme;
	delete document.documentElement.dataset.marks;

	window.localStorage.removeItem('stanza-theme');
	window.localStorage.removeItem('stanza-marks');
});

describe('the list of rules', () => {
	it('keeps one row in the tab order, the selected one, and selects the row that takes the focus', async () => {
		const store = renderPage();

		expect([...document.querySelectorAll('#panel-rules li button')].map((button) => button.getAttribute('tabindex'))).toEqual([
			'0',
			'-1',
			'-1',
			'-1',
		]);

		buttonOf('ZA0003').focus();
		await flush();

		expect(store.get().selection).toEqual({ kind: 'rule', id: 'ZA0003' });
		expect(buttonOf('ZA0003').tabIndex).toBe(0);
		expect(buttonOf('ZA0001').tabIndex).toBe(-1);
	});

	it('moves the focus with the arrow keys, Home and End, and leaves other keys alone', async () => {
		renderPage();
		buttonOf('ZA0001').focus();

		fireEvent.keyDown(buttonOf('ZA0001'), { key: 'ArrowDown' });
		expect(document.activeElement).toBe(buttonOf('ZA0002'));

		fireEvent.keyDown(buttonOf('ZA0002'), { key: 'End' });
		expect(document.activeElement).toBe(buttonOf('ZA0004'));

		fireEvent.keyDown(buttonOf('ZA0004'), { key: 'Home' });

		expect(document.activeElement).toBe(buttonOf('ZA0001'));
		expect(fireEvent.keyDown(buttonOf('ZA0001'), { key: 'a' })).toBe(true);
		expect(document.activeElement).toBe(buttonOf('ZA0001'));
	});

	it('skips a row the filter hides', async () => {
		renderPage({ config: { ...emptyConfig(), formatter: 'csharpier' } });
		await flush();

		expect(rowOf('ZA0002').hidden).toBe(true);

		buttonOf('ZA0001').focus();
		fireEvent.keyDown(buttonOf('ZA0001'), { key: 'ArrowDown' });

		expect(document.activeElement).toBe(buttonOf('ZA0003'));
	});

	it('keeps the row that holds the focus in view when it stops matching Changed only', async () => {
		const store = renderPage({
			changedOnly: true,
			config: { ...emptyConfig(), rules: { ZA0002: 'warning' } },
			selection: { kind: 'rule', id: 'ZA0002' },
		});

		await flush();

		const select = rowOf('ZA0002').querySelector('select')!;

		expect(rowOf('ZA0001').hidden).toBe(true);

		select.focus();
		fireEvent.change(select, { target: { value: 'default' } });
		await flush();

		expect(store.get().config.rules).toEqual({});
		expect(rowOf('ZA0002').hidden).toBe(false);
	});

	it('writes the setting of a rule and selects it', async () => {
		const store = renderPage();
		const select = rowOf('ZA0004').querySelector('select')!;

		fireEvent.change(select, { target: { value: 'error' } });
		await flush();

		expect(store.get().config.rules).toEqual({ ZA0004: 'error' });
		expect(store.get().selection).toEqual({ kind: 'rule', id: 'ZA0004' });
	});
});

describe('the live regions', () => {
	it('are written only when their text changes, not on every move down the list', async () => {
		const store = renderPage();
		const count = screen.getByText('Moderate, no changes');
		const written = vi.fn();
		const observer = new MutationObserver(written);

		for (const region of document.querySelectorAll('[role="status"], [aria-live]')) {
			observer.observe(region, { childList: true, characterData: true, subtree: true });
		}

		buttonOf('ZA0003').focus();
		store.set({ selection: { kind: 'rule', id: 'ZA0004' } });
		await flush();

		expect(count.getAttribute('role')).toBe('status');
		expect(document.querySelectorAll('[aria-live]').length).toBeGreaterThan(0);
		expect(written).not.toHaveBeenCalled();

		store.set({ config: { ...emptyConfig(), rules: { ZA0004: 'error' } } });
		await flush();

		expect(count.textContent).toBe('Moderate plus 1 change');
		expect(written).toHaveBeenCalledTimes(1);
	});
});

describe('the tabs', () => {
	it('move with the arrow keys, select what the tab last showed, and leave one tab in the tab order', async () => {
		const store = renderPage();
		const rulesTab = screen.getByRole('tab', { name: /^Rules/ });

		fireEvent.keyDown(rulesTab, { key: 'ArrowRight' });
		await flush();

		const optionsTab = screen.getByRole('tab', { name: /^Options/ });

		expect(store.get().selection).toEqual({ kind: 'option', id: 'stanza_max_line_length' });
		expect(document.activeElement).toBe(optionsTab);
		expect(optionsTab.tabIndex).toBe(0);
		expect(rulesTab.tabIndex).toBe(-1);
		expect(document.getElementById('panel-rules')!.hidden).toBe(true);

		fireEvent.keyDown(optionsTab, { key: 'Home' });
		await flush();

		expect(store.get().selection).toEqual({ kind: 'rule', id: 'ZA0001' });
	});
});

describe('the level picker', () => {
	it('puts the check and the focus back on the level that stays when the visitor cancels', async () => {
		const store = renderPage({ config: { ...emptyConfig(), rules: { ZA0004: 'error' } } });
		const level = screen.getByRole('radiogroup', { name: 'Level' });
		const strong = within(level).getByLabelText('Strong') as HTMLInputElement;
		const moderate = within(level).getByLabelText('Moderate') as HTMLInputElement;

		window.confirm = () => false;
		strong.checked = true;

		fireEvent.change(strong);
		await flush();

		expect(store.get().config.level).toBe('moderate');
		expect(moderate.checked).toBe(true);
		expect(strong.checked).toBe(false);
		expect(document.activeElement).toBe(moderate);
	});

	it('fills the form from the level the visitor confirms', async () => {
		const store = renderPage({ config: { ...emptyConfig(), rules: { ZA0004: 'error' } } });
		const strong = within(screen.getByRole('radiogroup', { name: 'Level' })).getByLabelText('Strong') as HTMLInputElement;

		window.confirm = () => true;
		strong.checked = true;

		fireEvent.change(strong);
		await flush();

		expect(store.get().config.level).toBe('strong');
		expect(store.get().config.rules).toEqual({});
	});
});

describe('the options', () => {
	it('shows a refused value and keeps the last valid one until the box loses the focus', async () => {
		const store = renderPage({ selection: { kind: 'option', id: 'stanza_max_line_length' } });
		const box = screen.getByLabelText('Value of stanza_max_line_length') as HTMLInputElement;
		const message = document.getElementById('error-stanza_max_line_length')!;

		box.focus();
		fireEvent.input(box, { target: { value: '120' } });
		await flush();

		expect(store.get().config.options).toEqual({ stanza_max_line_length: '120' });

		fireEvent.input(box, { target: { value: 'wide' } });
		await flush();

		expect(box.getAttribute('aria-invalid')).toBe('true');
		expect(message.textContent).toMatch(/whole number/);
		expect(store.get().config.options).toEqual({ stanza_max_line_length: '120' });

		fireEvent.focusOut(box);
		await flush();

		expect(box.value).toBe('120');
		expect(message.textContent).toBe('');
	});
});

describe('a list option whose page closes the set of words', () => {
	const sources = option('stanza_sources', { kind: 'list', choices: ['project', 'stdlib'], defaultValue: 'project, stdlib', rules: ['ZA0001'] });

	it('refuses a word outside the set and keeps the last valid list', async () => {
		const store = renderPage({ selection: { kind: 'option', id: 'stanza_sources' } }, csharp, catalog(rules, [...options, sources]));
		const box = screen.getByLabelText('Value of stanza_sources') as HTMLInputElement;
		const message = document.getElementById('error-stanza_sources')!;

		box.focus();
		fireEvent.input(box, { target: { value: 'stdlib' } });
		await flush();

		expect(store.get().config.options).toEqual({ stanza_sources: 'stdlib' });

		fireEvent.input(box, { target: { value: 'stdlib, vendored' } });
		await flush();

		expect(box.getAttribute('aria-invalid')).toBe('true');
		expect(message.textContent).toMatch(/Use project, stdlib/);
		expect(store.get().config.options).toEqual({ stanza_sources: 'stdlib' });
	});
});

describe('the options of two rules that share a name', () => {
	const family = ['keep-single', 'separate-multi'];

	const shared = catalog(
		family.map((id) => rule(id, { level: 'minimal', options: [`${id}.objectMembers`] })),

		family.map((id) =>
			option('objectMembers', { kind: 'enum', choices: ['true', 'false'], defaultValue: 'true', global: false, scope: id, rules: [id] }),
		),
	);

	const select = (id: string): HTMLSelectElement => screen.getByLabelText(`Value of ${id}`) as HTMLSelectElement;

	it('lists a row for each rule that takes the name, with the rule below the key', () => {
		renderPage({ selection: { kind: 'option', id: 'keep-single.objectMembers' } }, javascript, shared);

		const rows = [...document.querySelectorAll('#panel-options li')];

		expect(rows.map((row) => row.getAttribute('data-id'))).toEqual(['keep-single.objectMembers', 'separate-multi.objectMembers']);
		expect(rows.map((row) => row.textContent)).toEqual([expect.stringContaining('keep-single'), expect.stringContaining('separate-multi')]);
		expect(rows.every((row) => row.textContent?.startsWith('objectMembers'))).toBe(true);
	});

	it('sets the option of one rule and leaves the other at its default, in the config and in the entry the page writes', async () => {
		const store = renderPage({ selection: { kind: 'option', id: 'separate-multi.objectMembers' } }, javascript, shared);

		fireEvent.change(select('separate-multi.objectMembers'), { target: { value: 'false' } });
		await flush();

		expect(store.get().config.options).toEqual({ 'separate-multi.objectMembers': 'false' });
		expect(store.get().selection).toEqual({ kind: 'option', id: 'separate-multi.objectMembers' });
		expect(select('keep-single.objectMembers').value).toBe('');
		expect(select('separate-multi.objectMembers').value).toBe('false');

		const written = document.body.textContent ?? '';

		expect(written).toContain("'stanza/separate-multi': ['error', { objectMembers: false }],");
		expect(written).not.toContain("'stanza/keep-single'");
	});

	it('names the option under each rule by its key, and selects the option of that rule from its chip', async () => {
		const store = renderPage({ selection: { kind: 'rule', id: 'separate-multi' } }, javascript, shared);

		fireEvent.click(screen.getByRole('button', { name: 'objectMembers' }));
		await flush();

		expect(store.get().selection).toEqual({ kind: 'option', id: 'separate-multi.objectMembers' });
	});
});

describe('the pages of the other languages', () => {
	it('offer the pickers the language has: a severity for JS, and none for Python', () => {
		renderPage({}, javascript);

		expect(screen.getByRole('radiogroup', { name: 'Severity' })).toBeTruthy();
		expect(screen.queryByRole('radiogroup', { name: 'On conflict with CSharpier' })).toBeNull();

		cleanup();
		renderPage({}, python);

		expect(screen.getByRole('radiogroup', { name: 'Level' })).toBeTruthy();
		expect(screen.queryByRole('radiogroup', { name: 'Severity' })).toBeNull();
		expect(screen.queryByRole('radiogroup', { name: 'On conflict with CSharpier' })).toBeNull();
	});

	it('label a rule of Python as enabled or disabled, never by a severity', () => {
		renderPage({}, python);

		const options = [...rowOf('ZA0001').querySelectorAll('option')].map((entry) => entry.textContent);

		expect(options).toEqual(['Level (Enabled)', 'Enabled', 'Disabled']);
	});

	it('show the half of a pair that its opposite replaces as off, and write both halves when the visitor enables both', () => {
		const pair = catalog(
			[rule('ZA3111', { opposite: 'ZA3112' }), rule('ZA3112', { onByDefault: false, opposite: 'ZA3111' })],
			[],
			[{ first: 'ZA3111', second: 'ZA3112', defaultHalf: 'ZA3111', ownerWhenBothEnabled: 'ZA3112' }],
		);

		const table = (): string => screen.getByRole('region', { name: 'Generated [tool.stanza] table' }).textContent ?? '';
		const choose = (id: string, value: string): void => void fireEvent.change(screen.getByLabelText(`Severity of ${id}`), { target: { value } });
		const store = renderPage({ selection: { kind: 'rule', id: 'ZA3111' } }, python, pair);

		expect(rowOf('ZA3111').querySelector('select option')?.textContent).toBe('Level (Enabled)');
		expect(rowOf('ZA3111').querySelector('[title="Off at this level"]')).toBeNull();

		choose('ZA3112', 'error');

		expect(rowOf('ZA3111').querySelector('select option')?.textContent).toBe('Level (Off; ZA3112 is enabled)');
		expect(rowOf('ZA3111').querySelector('[title="Off: ZA3112 is enabled"]')?.textContent).toBe('off');
		expect(table()).toBe('[tool.stanza]\nenable = ["ZA3112"]\n');

		choose('ZA3111', 'error');

		expect(store.get().config.rules).toEqual({ ZA3112: 'error', ZA3111: 'error' });
		expect(table()).toBe('[tool.stanza]\nenable = ["ZA3111", "ZA3112"]\n');
	});

	it('show the separating half of a JS pair as off once the tight half is enabled, and write the tight half alone', async () => {
		const separating = 'separate-multi-line-switch-cases';
		const tight = 'keep-multi-line-switch-cases-together';

		const pair = catalog(
			[rule(separating, { opposite: tight }), rule(tight, { level: 'opt-in', onByDefault: false, opposite: separating })],
			[],
			[{ first: separating, second: tight, defaultHalf: separating, ownerWhenBothEnabled: tight }],
		);

		const config = (): string => screen.getByRole('region', { name: 'Generated eslint.config.mjs' }).textContent ?? '';
		const choose = (id: string, value: string): void => void fireEvent.change(screen.getByLabelText(`Severity of ${id}`), { target: { value } });
		const store = renderPage({ selection: { kind: 'rule', id: separating } }, javascript, pair);

		expect(rowOf(separating).querySelector('select option')?.textContent).toBe('Level (Error)');
		expect(rowOf(separating).querySelector('[title="Off at this level"]')).toBeNull();

		choose(tight, 'error');
		store.set({ selection: { kind: 'rule', id: separating } });
		await flush();

		expect(rowOf(separating).querySelector('select option')?.textContent).toBe(`Level (Off; ${tight} is enabled)`);
		expect(rowOf(separating).querySelector(`[title="Off: ${tight} is enabled"]`)?.textContent).toBe('off');

		expect(document.querySelector('#preview')?.textContent).toContain(
			`Off: ${tight} is enabled and owns the gap, so the rule reports nothing there.`,
		);

		expect(config()).toContain(`'stanza/${tight}': 'error',`);
		expect(config()).not.toContain(`stanza/${separating}`);
	});

	it('write the block of output of the language, with a Download button and no Import or Every setting', () => {
		renderPage({}, javascript);

		expect(screen.getByRole('region', { name: 'Generated eslint.config.mjs' }).textContent).toContain('stanza.configs.moderate');
		expect(screen.queryByText('Import')).toBeNull();
		expect(screen.queryByText('Every setting')).toBeNull();
		expect(screen.getByText('Download')).toBeTruthy();

		cleanup();
		renderPage({}, python);

		expect(screen.getByRole('region', { name: 'Generated [tool.stanza] table' }).textContent).toBe('[tool.stanza]\n');
		expect(screen.getByText('Download').getAttribute('title')).toMatch(/only the table/);
	});
});

describe('the page of C#', () => {
	it('offers the formatter picker, the import and the choices about the file, which the other pages leave out', () => {
		renderPage();

		expect(screen.getByRole('radiogroup', { name: 'On conflict with CSharpier' })).toBeTruthy();
		expect(screen.getByText('Import')).toBeTruthy();
		expect(screen.getByText('Every setting')).toBeTruthy();
	});
});

describe('the page of Markup', () => {
	const unleveled = { level: 'no-level', onByDefault: false, readByMarkup: true } as const;

	const markupCatalog = {
		...catalog(
			[
				rule('ZA2001', { ...unleveled, markupOnByDefault: true }),
				rule('ZA2002', unleveled),
				rule('ZA2009', { ...unleveled, undoneByCSharpier: true }),
			],

			[option('stanza_markup_max_blank_lines', { audience: 'both', defaultValue: '1' })],
		),

		elsewhere: { page: 'C#', rules: [], options: ['stanza_markup_literals'] },
	};

	const renderMarkup = (initial: Partial<AppState> = {}): Store =>
		renderPage({ config: emptyConfig('suggestion', 'none'), selection: { kind: 'rule', id: 'ZA2001' }, ...initial }, markup, markupCatalog);

	it('has no level, severity or formatter picker, and sends the visitor to the page of the key it lacks', () => {
		renderMarkup();

		expect(screen.queryByRole('radiogroup', { name: 'Level' })).toBeNull();
		expect(screen.queryByRole('radiogroup', { name: 'Severity' })).toBeNull();
		expect(screen.queryByRole('radiogroup', { name: 'On conflict with CSharpier' })).toBeNull();

		const link = screen.getByRole('link', { name: 'C# page' });

		expect(link.getAttribute('href')).toBe(import.meta.env.BASE_URL);
		expect(link.parentElement?.textContent).toContain('stanza_markup_literals');
	});

	it('offers the import, Every setting and the markup section, as the C# page does', () => {
		renderMarkup();

		expect(screen.getByText('Import')).toBeTruthy();
		expect(screen.getByText('Every setting')).toBeTruthy();
		expect(screen.getByText('Markup section')).toBeTruthy();
		expect(screen.getByRole('region', { name: 'Generated .editorconfig' })).toBeTruthy();
		expect(screen.queryByRole('region', { name: 'MSBuild properties' })).toBeNull();
	});

	it('labels the choice of a rule by its default, never by a level, and flags the rules that are off', () => {
		renderMarkup();

		expect([...rowOf('ZA2001').querySelectorAll('option')].map((entry) => entry.textContent)).toEqual([
			'Default (Suggestion)',
			'Error',
			'Warning',
			'Suggestion',
			'Silent',
			'None (Disabled)',
		]);

		expect(rowOf('ZA2002').querySelector('select option')?.textContent).toBe('Default (Off)');
		expect(rowOf('ZA2001').querySelector('[title="Off until a line enables it"]')).toBeNull();
		expect(rowOf('ZA2002').querySelector('[title="Off until a line enables it"]')?.textContent).toBe('off');
	});

	it('colors the dot of a rule by its severity, since the page offers more than one', () => {
		renderMarkup({ config: { ...emptyConfig('suggestion', 'none'), rules: { ZA2002: 'error' } } });

		expect(rowOf('ZA2002').querySelector('[title="Error"]')).toBeTruthy();
		expect(rowOf('ZA2001').querySelector('[title="Suggestion"]')).toBeTruthy();
	});

	it('counts the changes alone, with no level in the summary', () => {
		const store = renderMarkup();

		expect(document.body.textContent).toContain('No changes');

		fireEvent.change(rowOf('ZA2002').querySelector('select')!, { target: { value: 'warning' } });

		expect(store.get().config.rules).toEqual({ ZA2002: 'warning' });
		expect(document.body.textContent).toContain('1 change');
		expect(document.body.textContent).not.toMatch(/Moderate|Strong|Minimal/);
	});

	it('writes the lines of the rules that are on into the file it shows', () => {
		renderMarkup();

		expect(screen.getByRole('region', { name: 'Generated .editorconfig' }).textContent).toBe(
			'[*.cs]\ndotnet_diagnostic.ZA2001.severity = suggestion\n',
		);
	});

	it('says in the status box that CSharpier undoes the layout of ZA2009 and that no level holds the rule', () => {
		renderMarkup({ selection: { kind: 'rule', id: 'ZA2009' } });

		const status = document.querySelector('#preview')?.textContent ?? '';

		expect(status).toContain('CSharpier undoes the layout this rule writes.');
		expect(status).not.toMatch(/level/i);
	});

	it('keeps the C# page free of the Markup page and names it', () => {
		renderPage();

		const link = screen.getByRole('link', { name: 'Markup page' });

		expect(link.getAttribute('href')).toBe(`${import.meta.env.BASE_URL}markup/`);
		expect(link.parentElement?.textContent).toContain('ZA2001 to ZA2010');
	});
});

describe('the language switch', () => {
	const base = import.meta.env.BASE_URL;

	it('links the pages and carries the picked level in the hash, and marks the page it is on', () => {
		renderPage({ config: { ...emptyConfig(), level: 'strong' } }, javascript);

		const links = within(screen.getByRole('navigation', { name: 'Language' })).getAllByRole('link');

		expect(links.map((link) => link.textContent)).toEqual(['C#', 'Markup', 'JS/TS', 'Python']);

		expect(links.map((link) => link.getAttribute('href'))).toEqual([
			`${base}#level=strong`,
			`${base}markup/#level=strong`,
			`${base}js/#level=strong`,
			`${base}py/#level=strong`,
		]);

		expect(links.map((link) => link.getAttribute('aria-current'))).toEqual([null, null, 'page', null]);
	});

	it('leaves the hash out for the default level', () => {
		renderPage();

		expect(
			within(screen.getByRole('navigation', { name: 'Language' }))
				.getAllByRole('link')[2]!
				.getAttribute('href'),
		).toBe(`${base}js/`);
	});
});

describe('the header', () => {
	it.each([csharp, markup, javascript, python])('shows the logo of the $id page', (language) => {
		renderPage({}, language);
		expect(document.querySelector('header img')?.getAttribute('src')).toBe(logos[language.id]);
	});

	it('links the repository by its mark alone, and carries no other project link', () => {
		renderPage();

		const header = within(document.querySelector<HTMLElement>('header')!);
		const link = header.getByRole('link', { name: 'Stanza on GitHub' });

		expect(link.getAttribute('href')).toBe(repoUrl);
		expect(link.textContent).toBe('');
		expect(header.queryByRole('link', { name: /wiki|readme|repository/i })).toBeNull();
	});

	it('puts the theme button before the marks button, which is pressed unless the visitor chose otherwise', () => {
		window.localStorage.removeItem('stanza-marks');
		renderPage();

		const header = within(document.querySelector<HTMLElement>('header')!);
		const buttons = header.getAllByRole('button');

		expect(buttons[0]!.textContent).toMatch(/ theme$/);
		expect(buttons[1]!.textContent).toContain('Diff marks');
		expect(buttons[1]!.getAttribute('aria-pressed')).toBe('true');
		expect(document.documentElement.dataset.marks).toBe('on');
	});

	it('turns the marks off and on again, and remembers the choice', () => {
		window.localStorage.removeItem('stanza-marks');
		renderPage();

		const button = within(document.querySelector<HTMLElement>('header')!).getByRole('button', { name: 'Diff marks' });

		fireEvent.click(button);

		expect(button.getAttribute('aria-pressed')).toBe('false');
		expect(document.documentElement.dataset.marks).toBe('off');
		expect(window.localStorage.getItem('stanza-marks')).toBe('off');

		fireEvent.click(button);

		expect(button.getAttribute('aria-pressed')).toBe('true');
		expect(document.documentElement.dataset.marks).toBe('on');
		expect(window.localStorage.getItem('stanza-marks')).toBe('on');
	});
});
