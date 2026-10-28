import { generateEditorConfig } from '../editorconfig.ts';
import type { SiteCatalog } from '../model.ts';
import type { AppState, Store } from '../store.ts';
import { h } from './dom.ts';

export interface Output {
	element: HTMLElement;
	update(state: AppState): void;
}

function colorize(text: string): HTMLElement[] {
	return text
		.replace(/\n$/, '')
		.split('\n')
		.map((line) => {
			if (line.startsWith('#')) {
				return h('span', { class: 'oc' }, line, '\n');
			}

			if (line.startsWith('[')) {
				return h('span', { class: 'os' }, line, '\n');
			}

			const at = line.indexOf(' = ');

			return at < 0
				? h('span', {}, line, '\n')
				: h('span', {}, h('span', { class: 'ok' }, line.slice(0, at)), ' = ', h('span', { class: 'ov' }, line.slice(at + 3)), '\n');
		});
}

async function copyText(text: string): Promise<boolean> {
	try {
		await navigator.clipboard.writeText(text);
		return true;
	} catch {
		const area = h('textarea', { value: text, readOnly: true });

		area.style.position = 'fixed';
		area.style.opacity = '0';
		document.body.append(area);
		area.select();

		try {
			return document.execCommand('copy');
		} catch {
			return false;
		} finally {
			area.remove();
		}
	}
}

export function createOutput(catalog: SiteCatalog, store: Store): Output {
	let text = '';
	let timer: number | undefined;

	const code = h('code', {});
	const status = h('span', { class: 'copy-status', role: 'status' });

	const everySetting = h('input', { type: 'checkbox', id: 'opt-every-setting' });
	const markup = h('input', { type: 'checkbox', id: 'opt-markup' });

	const announce = (message: string): void => {
		status.textContent = message;
		window.clearTimeout(timer);
		timer = window.setTimeout(() => (status.textContent = ''), 2500);
	};

	const copy = h(
		'button',
		{ type: 'button', class: 'btn', onclick: async () => announce((await copyText(text)) ? 'Copied' : 'Copy failed; select the text instead') },
		'Copy',
	);

	const download = h(
		'button',
		{
			type: 'button',
			class: 'btn',
			onclick: () => {
				const url = URL.createObjectURL(new Blob([text], { type: 'text/plain;charset=utf-8' }));
				const link = h('a', { href: url, download: '.editorconfig' });

				document.body.append(link);
				link.click();
				link.remove();
				URL.revokeObjectURL(url);
			},
		},
		'Download',
	);

	everySetting.addEventListener('change', () => store.set({ config: { ...store.get().config, everySetting: everySetting.checked } }));
	markup.addEventListener('change', () => store.set({ config: { ...store.get().config, markupSection: markup.checked } }));

	const element = h(
		'section',
		{ class: 'output', aria: { labelledby: 'output-title' } },
		h(
			'div',
			{ class: 'output-bar' },
			h('h2', { id: 'output-title' }, '.editorconfig'),
			h('label', { class: 'check', htmlFor: 'opt-every-setting' }, everySetting, 'Every setting'),
			h(
				'label',
				{
					class: 'check',
					htmlFor: 'opt-markup',
					title: `A section for ${catalog.markupExtensions
						.slice(0, 3)
						.map((extension) => `.${extension}`)
						.join(', ')} and the other files stanza-markup lays out`,
				},
				markup,
				'Markup section',
			),
			h('span', { class: 'spacer' }),
			status,
			copy,
			download,
		),
		h('pre', { class: 'output-code', tabIndex: 0, aria: { label: 'Generated .editorconfig' } }, code),
	);

	const update = (state: AppState): void => {
		text = generateEditorConfig(catalog, state.config);
		code.replaceChildren(...colorize(text));
		everySetting.checked = state.config.everySetting;
		markup.checked = state.config.markupSection;
	};

	return { element, update };
}
