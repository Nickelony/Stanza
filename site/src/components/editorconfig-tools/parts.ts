import styled from '@emotion/styled';

import { srOnly } from '../primitives.ts';

export const Report = styled.section`
	max-height: 24vh;
	margin: 0;
	padding: 8px 12px;
	overflow: auto;
	border-top: 1px solid var(--border);
`;

export const LiveStatus = styled.p`
	${srOnly};
`;

export const Summary = styled.p`
	margin: 0 0 4px;
`;

export const Notes = styled.ul`
	margin: 0 0 6px;
	padding-left: 18px;
`;

export const Reason = styled.span`
	color: var(--muted);
`;

export const Warnings = styled.section`
	max-height: 24vh;
	margin: 0;
	padding: 8px 12px;
	overflow: auto;
	border-top: 1px solid var(--border);
	border-left: 3px solid var(--sev-warning);
`;

export const WarningsTitle = styled.h3`
	margin: 0 0 4px;
	font-size: 12.5px;
	font-weight: 600;
`;

export const WarningList = styled.ul`
	margin: 0;
	padding-left: 18px;
`;
