import styled from '@emotion/styled';

import { sideBySide } from '../../../util/breakpoints.ts';

export const Pane = styled.section`
	background: var(--bg);
	container-type: inline-size;

	${sideBySide} {
		min-height: 0;
		overflow: auto;
	}
`;

export const Page = styled.div`
	padding: 12px 16px 16px;
`;

export const Placeholder = styled.p`
	margin: 0;
	color: var(--muted);
`;
