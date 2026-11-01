import { css } from '@emotion/react';
import styled from '@emotion/styled';

import type { EffectiveState } from '../../state/config.ts';
import { phone, sideBySide } from '../../util/breakpoints.ts';
import { controlFace } from '../primitives.ts';

import type { ControlProps } from './types.ts';

export const GroupTitle = styled.h2`
	position: sticky;
	z-index: 1;
	top: 0;
	margin: 0;
	padding: 4px 12px;
	border-bottom: 1px solid var(--border);
	background: var(--surface-2);
	color: var(--muted);
	font-size: 12px;
	font-weight: 600;
`;

export const Rows = styled.ul`
	margin: 0;
	padding: 0;
	list-style: none;
`;

export const Empty = styled.p`
	margin: 0;
	padding: 14px 12px;
	color: var(--muted);
`;

/**
 * The panel is positioned so that the screen-reader notes inside its rows are clipped with it.
 * Unpositioned, each one sits at its row's place in the page and stretches the page by the height of the whole list.
 */
export const Panel = styled.div`
	position: relative;
	max-height: 58vh;
	max-height: 58dvh;
	overflow: auto;

	${sideBySide} {
		flex: 1;
		min-height: 0;
		max-height: none;
	}
`;

export const RowName = styled.span`
	display: contents;

	/* A phone stacks the ID above the title, so the title keeps the width of the row. */
	${phone} {
		display: flex;
		flex-direction: column;
		min-width: 0;
	}
`;

export const RowId = styled.span<{ $key?: boolean }>`
	flex: ${(props) => (props.$key ? '0 1 auto' : 'none')};
	min-width: ${(props) => (props.$key ? '0' : 'auto')};
	color: var(--muted);
	font-family: var(--mono);
	font-size: 12px;
`;

export const RowTitle = styled.span`
	min-width: 0;
	overflow-wrap: anywhere;
`;

export const Flag = styled.span`
	flex: none;
	margin-left: auto;
	padding: 0 4px;
	border: 1px solid var(--border-strong);
	border-radius: 3px;
	color: var(--muted);
	font-size: 11px;
`;

/** The key of an option, with the rule it belongs to below it, so a long rule name has a line of its own rather than crowd out the key. */
export const OptionName = styled.span`
	display: flex;
	flex: 0 1 auto;
	flex-direction: column;
	align-items: flex-start;
	min-width: 0;
	gap: 3px;
`;

/** The rule an option belongs to; it can be a long name, so it may wrap, and it never grows wider than its line. */
export const ScopeFlag = styled(Flag)`
	flex: none;
	min-width: 0;
	max-width: 100%;
	margin-left: 0;
	overflow-wrap: anywhere;
`;

const dotColors: Partial<Record<EffectiveState | 'enabled', string>> = {
	enabled: 'var(--accent)',
	error: 'var(--sev-error)',
	warning: 'var(--sev-warning)',
	suggestion: 'var(--sev-suggestion)',
};

const dashed = css`
	border-style: dashed;
	border-color: var(--border-strong);
	background: transparent;
`;

/** The plain box is the silent dot and the others fill it or dash it; a language with no severities shows `enabled`, which no severity colors. */
export const Dot = styled.span<{ $state: EffectiveState | 'enabled' }>`
	flex: none;
	align-self: center;
	width: 9px;
	height: 9px;
	border: 1px solid var(--muted);
	border-radius: 2px;

	${(props) => dotColors[props.$state] !== undefined && `border-color: ${dotColors[props.$state]}; background: ${dotColors[props.$state]};`};
	${(props) => (props.$state === 'none' || props.$state === 'off') && dashed};

	@media (forced-colors: active) {
		forced-color-adjust: none;
	}
`;

const controlSize = css`
	${controlFace};
	width: 11rem;
	max-width: 100%;

	/* One fixed height, because the code font of a text box would otherwise make it taller than a dropdown. */
	height: 28px;
	padding: 0 3px;

	${phone} {
		width: 9rem;
		height: 36px;
		margin: 0 0 0 4px;
	}
`;

const changedControl = (props: ControlProps) =>
	props.$changed &&
	css`
		border-color: var(--accent);

		@media (forced-colors: active) {
			border-width: 2px;
			border-color: Highlight;
		}
	`;

/** A dropdown draws its text a pixel low in the shared height, so its padding lifts it level with a text box. */
export const RowSelect = styled.select<ControlProps>`
	${controlSize};
	padding-bottom: 2px;

	${phone} {
		padding-bottom: 0;
	}

	${changedControl};
`;

/** A dropdown insets its text a little inside its padding, so a text box adds as much to start its text in the same column. */
export const RowInput = styled.input<ControlProps>`
	${controlSize};
	padding-left: 7px;
	font-family: var(--mono);

	/* The placeholder names the default as the dropdowns do, so it takes their font; a typed value keeps the code font. */
	/* The muted color reads in both themes, where the browser's own placeholder gray all but vanishes in the dark one. */
	&::placeholder {
		opacity: 1;
		color: var(--muted);
		font-family: var(--font);
	}

	${changedControl};

	&[aria-invalid='true'] {
		border-color: var(--del-fg);
	}
`;

/** The left margin matches the padding of the row button, so the message starts under the key. */
export const FieldError = styled.p`
	grid-column: 1 / -1;
	margin: -3px 0 5px 9px;
	color: var(--del-fg);
	font-size: 12px;

	/* The message is a live region that stays in the page, so with no text it takes no room. */
	&:empty {
		margin: 0;
	}
`;

export const Item = styled.li<{ $selected: boolean; $changed: boolean }>`
	display: grid;
	grid-template-columns: minmax(0, 1fr) auto;
	align-items: center;
	gap: 0 8px;
	padding-right: 8px;
	border-bottom: 1px solid var(--border);
	border-left: 3px solid ${(props) => (props.$changed ? 'var(--accent)' : 'transparent')};
	background: ${(props) => (props.$selected ? 'var(--accent-soft)' : 'transparent')};

	&[hidden] {
		display: none;
	}

	@media (forced-colors: active) {
		/* The outline sits inside the row, because the list clips what is outside it. */
		outline: ${(props) => (props.$selected ? '2px solid Highlight' : 'none')};
		outline-offset: -2px;

		/* The marker is a border, which the forced palette would turn into text color. */
		${(props) => props.$changed && 'border-left-color: Highlight;'};
	}
`;

export const Main = styled.button`
	display: flex;
	align-items: center;
	min-width: 0;
	padding: 6px 4px 6px 9px;
	border: 0;
	background: none;
	text-align: left;
	cursor: pointer;
	gap: 8px;

	${phone} {
		min-height: 44px;
		padding-block: 8px;
	}
`;
