import styled from '@emotion/styled';

import { sideBySide } from '../../util/breakpoints.ts';
import type { TokenKind } from '../../writers/colorize.ts';
import { codeFace } from '../primitives.ts';

export const Box = styled.section`
	border-top: 1px solid var(--border);
	background: var(--surface);
`;

export const Bar = styled.div<{ $separated: boolean }>`
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 6px 12px;
	padding: 8px 12px;
	border-top: ${(props) => (props.$separated ? '1px solid var(--border)' : '0')};
`;

export const Title = styled.h2`
	margin: 0;
	font-family: var(--mono);
	font-size: 13px;
	font-weight: 600;
`;

export const Note = styled.span`
	color: var(--muted);
	font-size: 12px;
`;

/** The status and the buttons of a bar wrap as one group, to the right of the row they land on. */
export const Actions = styled.div`
	display: flex;
	align-items: center;
	gap: var(--button-gap);
	margin-left: auto;
`;

export const CopyStatus = styled.span`
	color: var(--muted);
	font-size: 12px;
`;

export const Code = styled.pre<{ $compact: boolean }>`
	${codeFace};
	max-height: ${(props) => (props.$compact ? '14vh' : '30vh')};
	margin: 0;
	padding: ${(props) => (props.$compact ? '6px 12px 8px' : '8px 12px 12px')};
	border-top: 1px solid var(--border);

	/* The properties are a few lines, so the block keeps its height, and a wide screen does not shorten it. */
	${sideBySide} {
		max-height: ${(props) => (props.$compact ? '14vh' : '20vh')};
	}
`;

const tokenStyles: Record<TokenKind, string> = {
	comment: 'color: var(--muted);',
	section: 'font-weight: 700;',
	value: 'color: var(--accent);',
};

export const Colored = styled.span<{ $kind: TokenKind | null }>`
	${(props) => props.$kind && tokenStyles[props.$kind]};
`;
