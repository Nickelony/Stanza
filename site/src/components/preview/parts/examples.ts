import styled from '@emotion/styled';

import { controlFace } from '../../primitives.ts';

export const Example = styled.section`
	margin: 0 0 12px;
`;

/** The Before and After code, side by side where the pane is wide enough. */
export const Pair = styled.div`
	display: grid;
	grid-template-columns: minmax(0, 1fr);
	gap: 10px;

	@container (min-width: 640px) {
		grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
	}
`;

export const Segments = styled.div`
	display: grid;
	gap: 10px;
`;

export const Figure = styled.figure`
	min-width: 0;
	margin: 0;
`;

export const Caption = styled.figcaption`
	margin-bottom: 3px;
	color: var(--muted);
	font-size: 12px;
`;

export const Cap = styled.span`
	font-weight: 600;
`;

export const Variants = styled.div`
	display: flex;
	flex-wrap: wrap;
	gap: 4px;
	margin-bottom: 6px;
`;

export const Variant = styled.button`
	${controlFace};
	padding: 1px 8px;
	cursor: pointer;

	&[aria-pressed='true'] {
		border-color: var(--accent);
		background: var(--accent-soft);
	}

	&:hover {
		background: var(--surface-2);
	}

	@media (forced-colors: active) {
		&[aria-pressed='true'] {
			forced-color-adjust: none;
			background: Highlight;
			color: HighlightText;
		}
	}
`;
