import styled from '@emotion/styled';

import { sideBySide } from '../../util/breakpoints.ts';

export const Layout = styled.main`
	${sideBySide} {
		display: grid;
		grid-template-columns: minmax(420px, min(34vw, 720px)) minmax(0, 1fr);
		flex: 1;
		min-height: 0;
	}
`;

export const LeftPane = styled.div`
	display: flex;
	flex-direction: column;
	min-width: 0;
	border-bottom: 1px solid var(--border);
	background: var(--surface);

	${sideBySide} {
		min-height: 0;
		border-right: 1px solid var(--border);
		border-bottom: 0;
	}
`;

export const RightPane = styled.div`
	min-width: 0;

	${sideBySide} {
		display: grid;
		/* The column takes the pane's width and not its widest line, so a long line of the output scrolls inside its block. */
		grid-template-columns: minmax(0, 1fr);
		grid-template-rows: minmax(0, 1fr) auto;
		min-height: 0;
	}
`;
