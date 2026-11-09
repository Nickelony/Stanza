import styled from '@emotion/styled';

export const Strip = styled.div`
	display: flex;
	gap: 2px;
	padding: 0 12px;
	border-bottom: 1px solid var(--border);
`;

export const Tab = styled.button`
	position: relative;
	top: 1px;
	padding: 5px 12px;
	border: 1px solid transparent;
	border-bottom: 0;
	border-radius: var(--radius) var(--radius) 0 0;
	background: none;
	color: var(--muted);
	cursor: pointer;

	&[aria-selected='true'] {
		border-color: var(--border);
		background: var(--surface);
		color: var(--text);
		font-weight: 600;
	}
`;
