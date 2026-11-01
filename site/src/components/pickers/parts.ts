import styled from '@emotion/styled';

import { srOnly } from '../primitives.ts';

export const Box = styled.div`
	display: flex;
	flex-direction: column;
	gap: 8px;
	padding: 8px 12px 10px;
	border-bottom: 1px solid var(--border);
	container-type: inline-size;
`;

export const Name = styled.div`
	margin-bottom: 2px;
	color: var(--muted);
	font-size: 12px;
`;

/** The joined buttons; the radio inputs inside stay in the page for the keyboard and the screen reader, and the face of each label is what shows. */
export const Group = styled.div`
	display: flex;
	width: 100%;
	overflow-x: auto;
	border: 1px solid var(--border-strong);
	border-radius: var(--radius);
`;

export const Segment = styled.label`
	display: flex;
	position: relative;
	flex: 1 1 0;
	cursor: pointer;

	& + & {
		border-left: 1px solid var(--border-strong);
	}

	&:has(input:disabled) {
		cursor: default;
	}
`;

export const Radio = styled.input`
	${srOnly};
`;

export const Face = styled.span<{ $tight: boolean }>`
	display: flex;
	flex: 1;
	align-items: center;
	justify-content: center;
	padding: 2px 9px;
	background: var(--surface);
	white-space: nowrap;

	label:hover > & {
		background: var(--surface-2);
	}

	input:checked + & {
		background: var(--accent);
		color: var(--surface);
	}

	/* The ring sits inside the group, because the group clips what is outside it. */
	input:focus-visible + & {
		outline: 2px solid var(--focus);
		outline-offset: -3px;
	}

	input:checked:focus-visible + & {
		outline-color: var(--surface);
	}

	input:disabled + & {
		opacity: 0.6;
		background: var(--surface-2);
		color: var(--muted);
	}

	input:disabled:checked + & {
		background: var(--border);
		color: var(--muted);
	}

	/* A narrow pane sets the buttons smaller, so the four levels fit side by side. */
	@container (max-width: 395px) {
		min-height: 32px;
		padding: 2px ${(props) => (props.$tight ? '4px' : '6px')};
		font-size: 12px;
		line-height: 1.3;
	}

	/* Windows High Contrast replaces every background color, so the picked segment would look unmarked. */
	@media (forced-colors: active) {
		input:checked + & {
			forced-color-adjust: none;
			background: Highlight;
			color: HighlightText;
		}
	}
`;

export const Hint = styled.p`
	margin: 3px 0 0;
	color: var(--muted);
	font-size: 12px;
`;
