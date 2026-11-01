import styled from '@emotion/styled';

import { codeFace, srOnly } from '../primitives.ts';

import type { Mark, RegionProps } from './types.ts';

export const Region = styled.div<RegionProps>`
	position: relative;
	max-height: 55vh;
	padding: 4px 0;
	${codeFace};
	border: 1px solid var(--border);
	border-left: ${(props) => (props.$current ? '3px solid var(--accent)' : '1px solid var(--border)')};
	border-radius: var(--radius);
	opacity: ${(props) => (props.$dimmed ? 0.5 : 1)};

	&:focus-visible {
		outline-offset: -2px;
	}
`;

const marks = {
	add: { background: 'var(--add-bg)', color: 'var(--add-fg)' },
	del: { background: 'var(--del-bg)', color: 'var(--del-fg)' },
};

export const Line = styled.div<{ $mark: Mark | null; $plain: boolean }>`
	display: flex;
	min-height: 1.55em;
	padding-right: 10px;
	padding-left: ${(props) => (props.$plain ? '10px' : '0')};
	background: ${(props) => (props.$mark ? marks[props.$mark].background : 'transparent')};
	white-space: pre;

	/* Without the marks, the preview shows the code alone, and a blank line is an empty line. */
	html[data-marks='off'] & {
		background: none;
	}
`;

export const Gutter = styled.span<{ $mark: Mark | null }>`
	flex: none;
	width: 22px;
	color: ${(props) => (props.$mark ? marks[props.$mark].color : 'var(--muted)')};
	font-weight: ${(props) => (props.$mark ? 700 : 'inherit')};
	text-align: center;
	user-select: none;

	html[data-marks='off'] & {
		visibility: hidden;
	}
`;

export const Source = styled.span`
	flex: 1 0 auto;
`;

export const MarkNote = styled.span`
	${srOnly};

	html[data-marks='off'] & {
		display: none;
	}
`;

export const BlankNote = styled.span`
	color: var(--muted);
	font-family: var(--font);
	font-size: 11.5px;
	font-style: italic;

	html[data-marks='off'] & {
		display: none;
	}
`;
