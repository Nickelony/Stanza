import styled from '@emotion/styled';

export const NotesBox = styled.div`
	margin-top: 12px;

	& p {
		margin: 0 0 8px;
	}

	& ul {
		margin: 0 0 8px;
		padding-left: 20px;
	}

	& li {
		margin: 3px 0;
	}
`;

export const TableWrap = styled.div`
	margin: 0 0 12px;
	overflow-x: auto;
`;

export const Table = styled.table`
	width: 100%;
	border-collapse: collapse;
	font-size: 13px;

	& th,
	& td {
		padding: 4px 8px;
		border: 1px solid var(--border);
		text-align: left;
		vertical-align: top;
	}

	& th {
		background: var(--surface-2);
		font-weight: 600;
	}
`;

export const ValueRow = styled.tr<{ $active: boolean }>`
	& td {
		background: ${(props) => (props.$active ? 'var(--accent-soft)' : 'transparent')};
	}

	/* The cells hold links, which would not read on a Highlight background, so the active row gets an outline. */
	@media (forced-colors: active) {
		& td {
			outline: ${(props) => (props.$active ? '2px solid Highlight' : 'none')};
			outline-offset: -2px;
		}
	}
`;
