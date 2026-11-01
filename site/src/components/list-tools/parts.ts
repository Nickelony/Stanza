import styled from '@emotion/styled';

import { controlFace, srOnly } from '../primitives.ts';

export const Row = styled.div`
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 6px 12px;
	padding: 8px 12px;
	border-bottom: 1px solid var(--border);
`;

export const HiddenLabel = styled.label`
	${srOnly};
`;

export const Filter = styled.input`
	${controlFace};
	flex: 1 1 150px;
	min-width: 0;
	padding: 4px 8px;

	&::-webkit-search-cancel-button {
		margin-left: 6px;
	}
`;

export const Count = styled.span`
	margin-left: auto;
	color: var(--muted);
	font-size: 12px;
`;
