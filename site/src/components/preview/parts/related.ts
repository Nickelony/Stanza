import styled from '@emotion/styled';

import { controlFace } from '../../primitives.ts';

export const Related = styled.p`
	margin: 12px 0 0;
`;

export const RelatedLabel = styled.span`
	color: var(--muted);
`;

export const Chip = styled.button`
	${controlFace};
	margin: 0 4px 4px 0;
	padding: 1px 8px;
	font-family: var(--mono);
	font-size: 12px;
	cursor: pointer;

	&:hover {
		background: var(--surface-2);
	}
`;

export const OpenPage = styled.p`
	margin: 12px 0 0;
`;
