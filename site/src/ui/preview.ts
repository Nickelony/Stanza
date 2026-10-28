import { type EffectiveState, effectiveState, isOptionChanged, isRuleChanged, settingOf } from '../config.ts';
import { diffLines } from '../diff.ts';
import type { Block, ExampleRef, Option, Rule, SiteCatalog, Snippet } from '../model.ts';
import type { AppState, Store } from '../store.ts';
import { h, html } from './dom.ts';
import { stateLabels } from './labels.ts';

export interface Preview {
	element: HTMLElement;
	update(state: AppState): void;
}

const isBlank = (text: string): boolean => text.trim() === '';

const present = (node: HTMLElement | null): HTMLElement[] => (node ? [node] : []);

type Mark = 'add' | 'del' | null;

/** Draws a snippet with a gutter, and marks the lines the other side does not have. */
function codeBlock(snippet: Snippet, marked: ReadonlySet<number>, mark: Exclude<Mark, null>, label: string): HTMLElement {
	const gutter = mark === 'add' ? '+' : '−';
	const word = mark === 'add' ? 'added' : 'removed';

	return h(
		'div',
		{ class: 'code', tabIndex: 0, role: 'region', aria: { label } },
		...snippet.lines.map((line, index) => {
			const changed = marked.has(index);
			const source = changed && isBlank(line.text) ? '<span class="blank-note">blank line</span>' : line.html;

			return h(
				'div',
				{ class: `ln${changed ? ` ${mark}` : ''}` },
				h('span', { class: 'gut', aria: { hidden: 'true' } }, changed ? gutter : ''),
				changed ? h('span', { class: 'sr-only' }, `${word}: `) : null,
				html('span', source, 'src'),
			);
		}),
	);
}

function plainBlock(snippet: Snippet, label: string): HTMLElement {
	return h(
		'div',
		{ class: 'code plain', tabIndex: 0, role: 'region', aria: { label } },
		...snippet.lines.map((line) => h('div', { class: 'ln' }, html('span', line.html, 'src'))),
	);
}

function figure(caption: string, detail: string, body: HTMLElement, className = ''): HTMLElement {
	return h(
		'figure',
		{ class: `snippet ${className}`.trim() },
		h('figcaption', {}, h('span', { class: 'cap' }, caption), detail ? h('span', { class: 'cap-detail' }, ` - ${detail}`) : null),
		body,
	);
}

function blockNode(block: Block): HTMLElement {
	if (block.kind === 'p') {
		return html('p', block.html);
	}

	if (block.kind === 'ul') {
		return h('ul', {}, ...block.items.map((item) => html('li', item)));
	}

	return h(
		'div',
		{ class: 'table-wrap' },
		h(
			'table',
			{},
			h('thead', {}, h('tr', {}, ...block.head.map((cell) => html('th', cell)))),
			h('tbody', {}, ...block.rows.map((row) => h('tr', {}, ...row.map((cell) => html('td', cell))))),
		),
	);
}

const isBlock = (part: { kind: string }): part is Block => part.kind === 'p' || part.kind === 'ul' || part.kind === 'table';

/** Lays out the parts of a page in the order it writes them: a run of text shares one box, and an example or the value table stands where the page puts it. */
function flow<P extends Block | ExampleRef | { kind: 'values' }>(
	parts: readonly P[],
	place: (part: Exclude<P, Block>) => HTMLElement,
): HTMLElement[] {
	const nodes: HTMLElement[] = [];

	let notes: HTMLElement | null = null;

	for (const part of parts) {
		if (isBlock(part)) {
			if (!notes) {
				notes = h('div', { class: 'notes' });
				nodes.push(notes);
			}

			notes.append(blockNode(part));
		} else {
			nodes.push(place(part as Exclude<P, Block>));
			notes = null;
		}
	}

	return nodes;
}

function chips(label: string, ids: string[], store: Store, kind: 'rule' | 'option'): HTMLElement | null {
	if (ids.length === 0) {
		return null;
	}

	return h(
		'p',
		{ class: 'related' },
		h('span', { class: 'related-label' }, `${label} `),
		...ids.map((id) => h('button', { type: 'button', class: 'chip mono', onclick: () => store.set({ selection: { kind, id }, variant: 0 }) }, id)),
	);
}

function wikiLink(url: string, name: string): HTMLElement {
	return h('p', { class: 'wiki-link' }, h('a', { href: url, target: '_blank', rel: 'noopener' }, `Open ${name} on the wiki`));
}

const statusText: Record<EffectiveState, string> = {
	error: 'Reports as an error, which fails a build.',
	warning: 'Reports as a warning in the IDE and in a build.',
	suggestion: 'Reports as a suggestion in the IDE; a build never reports it.',
	silent: 'Enabled without a report; the rule still owns its gap in a pair.',
	none: 'Disabled: the rule reports nothing, and a bulk key never reaches it.',
	off: 'Off by default: the rule reports nothing until a line enables it.',
};

function ruleStatus<S>(rule: Rule<S>, state: AppState): { text: string; line: string | null; off: boolean } {
	const setting = settingOf(state.config, rule);
	const effective = effectiveState(rule, setting);
	const changed = isRuleChanged(rule, setting);
	const markupNote = rule.markupOnByDefault && setting === 'default' ? ' stanza-markup applies it unless a file disables it.' : '';
	const line = changed && setting !== 'default' ? `dotnet_diagnostic.${rule.id}.severity = ${setting}` : null;

	const lead = effective === 'off' || effective === 'none' ? '' : `${stateLabels[effective]}. `;

	return {
		text: `${lead}${statusText[effective]}${markupNote}`,
		line,
		off: effective === 'off' || effective === 'none',
	};
}

export function createPreview(catalog: SiteCatalog, store: Store): Preview {
	// The status box is made once and only its text changes, so a screen reader hears a change of state and not each move down the list.
	const status = h('p', { class: 'status', role: 'status' });
	const statusLine = h('code', { class: 'status-line' });
	const statusBox = h('div', { class: 'status-box', hidden: true }, status, statusLine);
	const top = h('div', { class: 'preview-part' });
	const bottom = h('div', { class: 'preview-part' });
	const element = h('div', { class: 'preview', id: 'preview', aria: { live: 'off' } }, top, statusBox, bottom);

	let shownKey = '';
	let dynamic: (state: AppState) => void = () => undefined;

	const rules = new Map(catalog.rules.map((rule) => [rule.id, rule]));
	const options = new Map(catalog.options.map((option) => [option.key, option]));

	const showRule = (rule: Rule<Snippet>, variant: number): void => {
		const afterPanes: HTMLElement[] = [];
		const multiple = rule.examples.some((example) => example.afters.length > 1);

		const examples = rule.examples.map((example, number) => {
			const label = rule.examples.length > 1 ? `Example ${number + 1}` : 'Example';

			// Code the rule leaves as written has nothing to compare, so it shows once, without marks.
			if (example.unchanged) {
				return h(
					'section',
					{ class: 'example', aria: { label } },
					figure('Unchanged', example.beforeLabel, codeBlock(example.before, new Set(), 'add', 'Code the rule leaves as written')),
				);
			}

			const after = example.afters[Math.min(variant, example.afters.length - 1)]!;

			const diff = diffLines(
				example.before.lines.map((line) => line.text),
				after.snippet.lines.map((line) => line.text),
			);

			const pair = h(
				'div',
				{ class: 'pair' },
				figure('Before', example.beforeLabel, codeBlock(example.before, diff.removed, 'del', 'Code before the fix')),
				figure('After', after.label, codeBlock(after.snippet, diff.added, 'add', 'Code after the fix'), 'after'),
			);

			afterPanes.push(pair.querySelector('.after') as HTMLElement);

			return h(
				'section',
				{ class: 'example', aria: { label } },
				multiple && example.afters.length > 1
					? h(
							'div',
							{ class: 'variants', role: 'group', aria: { label: 'Variant of the After code' } },
							...example.afters.map((candidate, index) =>
								h(
									'button',
									{
										type: 'button',
										class: 'variant',
										onclick: () => store.set({ variant: index }),
										title: candidate.label,
										aria: { pressed: String(index === Math.min(variant, example.afters.length - 1)) },
									},
									`Variant ${index + 1}`,
								),
							),
						)
					: null,
				pair,
			);
		});

		top.replaceChildren(
			h(
				'header',
				{ class: 'pv-head' },
				h('h2', {}, h('span', { class: 'mono pv-id' }, rule.id), ' ', rule.title),
				h('p', { class: 'tier' }, rule.tierText),
			),
			html('p', rule.descriptionHtml, 'desc'),
		);

		bottom.replaceChildren(
			...flow(rule.parts, (part) => examples[part.index]!),
			...present(chips('Options', rule.options, store, 'option')),
			wikiLink(rule.wikiUrl, rule.id),
		);

		statusBox.hidden = false;

		dynamic = (state) => {
			const info = ruleStatus(rule, state);

			status.textContent = info.text;
			statusLine.textContent = info.line ?? 'No line is written for this rule.';

			for (const pane of afterPanes) {
				pane.classList.toggle('inactive', info.off);
			}
		};
	};

	const showOption = (option: Option<Snippet>): void => {
		const valueRows: HTMLTableRowElement[] = [];
		const segments: Array<{ value: string; isDefault: boolean; node: HTMLElement }> = [];

		const examples = option.examples.map((example) => {
			const nodes = example.segments.map((segment) => {
				const node = figure(segment.label || 'Example', '', plainBlock(segment.snippet, segment.label || 'Example'), 'segment');

				segments.push({ value: segment.value, isDefault: segment.isDefault, node });

				return node;
			});

			return h('section', { class: 'example', aria: { label: 'Example' } }, h('div', { class: 'segments' }, ...nodes));
		});

		const table = h(
			'div',
			{ class: 'table-wrap' },
			h(
				'table',
				{ class: 'values' },
				h('thead', {}, h('tr', {}, h('th', {}, 'Value'), h('th', {}, 'Effect'))),
				h(
					'tbody',
					{},
					...option.values.map((value) => {
						const row = h('tr', {}, html('td', value.valueHtml), html('td', value.effectHtml));

						valueRows.push(row);

						return row;
					}),
				),
			),
		);

		top.replaceChildren(
			h(
				'header',
				{ class: 'pv-head' },
				h('h2', {}, h('span', { class: 'mono pv-id' }, option.key)),
				h('p', { class: 'tier' }, `Default: ${option.defaultText}`),
			),
			html('p', option.descriptionHtml, 'desc'),
			html('p', option.readByHtml, 'read-by'),
		);

		bottom.replaceChildren(
			...flow(option.parts, (part) => (part.kind === 'example' ? examples[part.index]! : table)),
			...present(chips('Rules', option.rules, store, 'rule')),
			wikiLink(option.wikiUrl, option.key),
		);

		statusBox.hidden = false;

		dynamic = (state) => {
			const value = state.config.options[option.key];
			const effective = value ?? option.defaultValue;

			status.textContent =
				value === undefined
					? 'Left at its default; no line is written.'
					: isOptionChanged(option, value)
						? 'Set to a value other than the default.'
						: 'Set to the default value.';

			statusLine.textContent = isOptionChanged(option, value) ? `${option.key} = ${value}` : 'No line is written for this key.';

			option.values.forEach((entry, index) => {
				valueRows[index]!.classList.toggle('active', effective !== null && entry.literals.includes(effective));
			});

			for (const segment of segments) {
				const active = effective !== null && segment.value === effective;
				const byDefault = value === undefined && segment.isDefault;

				segment.node.classList.toggle('active', active || byDefault);
			}
		};
	};

	const showNothing = (): void => {
		top.replaceChildren(
			h('p', { class: 'placeholder' }, 'Select a rule or an option to see what it does. The preview shows the code before and after the rule runs.'),
		);

		bottom.replaceChildren();
		statusBox.hidden = true;

		dynamic = () => undefined;
	};

	const update = (state: AppState): void => {
		const selection = state.selection;
		const key = selection ? `${selection.kind}:${selection.id}:${state.variant}` : 'none';

		if (key !== shownKey) {
			shownKey = key;

			if (selection?.kind === 'rule' && rules.has(selection.id)) {
				showRule(rules.get(selection.id)!, state.variant);
			} else if (selection?.kind === 'option' && options.has(selection.id)) {
				showOption(options.get(selection.id)!);
			} else {
				showNothing();
			}
		}

		dynamic(state);
	};

	return { element, update };
}
