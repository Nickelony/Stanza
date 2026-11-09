import styled from '@emotion/styled';

export const Head = styled.header`
	margin: 0;
`;

export const Heading = styled.h2`
	margin: 0;
	font-size: 16px;
	font-weight: 600;
`;

export const Id = styled.span`
	color: var(--muted);
	font-family: var(--mono);
	font-weight: 500;
`;

export const Tier = styled.p`
	margin: 2px 0 8px;
	color: var(--muted);
	font-size: 12.5px;
`;

export const Paragraph = styled.p`
	margin: 0 0 10px;
`;

export const Box = styled.div`
	margin: 0 0 12px;
	padding: 7px 10px;
	border: 1px solid var(--border);
	border-radius: var(--radius);
	background: var(--surface);
`;

export const StatusText = styled.p`
	margin: 0;
`;

export const StatusLine = styled.code`
	display: block;
	margin-top: 3px;
	color: var(--muted);
	font-size: 12.5px;
	overflow-wrap: anywhere;
	white-space: pre-wrap;
`;
