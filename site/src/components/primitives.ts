import { css } from '@emotion/react';
import styled from '@emotion/styled';

/** Hides text from the eye and keeps it for a screen reader. */
export const srOnly = css`
	position: absolute;
	width: 1px;
	height: 1px;
	margin: -1px;
	padding: 0;
	overflow: hidden;
	border: 0;
	white-space: nowrap;
	clip-path: inset(50%);
`;

export const ScreenReaderOnly = styled.span`
	${srOnly};
`;

/** The look every button, chip, text box and dropdown shares. */
export const controlFace = css`
	border: 1px solid var(--border-strong);
	border-radius: var(--radius);
	background: var(--surface);
`;

/** The face of a code block, in the preview and in the output. */
export const codeFace = css`
	overflow: auto;
	background: var(--code-bg);
	color: var(--code-fg);
	font-family: var(--mono);
	font-size: 12.5px;
	line-height: 1.55;
	tab-size: 4;
`;

export const Button = styled.button`
	${controlFace};
	padding: 3px 10px;
	cursor: pointer;

	&:hover:not(:disabled) {
		background: var(--surface-2);
	}

	&:disabled {
		opacity: 0.5;
		cursor: default;
	}
`;

export const CheckLabel = styled.label`
	display: inline-flex;
	align-items: center;
	gap: 6px;
	cursor: pointer;
`;

export const Mono = styled.span`
	font-family: var(--mono);
`;
