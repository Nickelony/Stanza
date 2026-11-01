import styled from '@emotion/styled';

import { headerStacks } from '../../util/breakpoints.ts';
import { Button } from '../primitives.ts';

export const Bar = styled.header`
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	min-height: var(--top-height);
	padding: 6px 12px;
	border-bottom: 1px solid var(--border);
	background: var(--surface);
	gap: 4px 16px;

	/* Below the width the header needs for one row, the title and the language links take a row each, and the actions stand beside both. */
	${headerStacks} {
		display: grid;
		grid-template-columns: minmax(0, 1fr) auto;
		align-items: center;
		gap: 4px 12px;
	}
`;

export const Title = styled.h1`
	display: flex;
	align-items: center;
	gap: 8px;
	margin: 0;
	font-size: 15px;
	font-weight: 600;
`;

export const Logo = styled.img`
	flex: none;
	border-radius: var(--radius);
`;

/** The repository link and the buttons stay together on one line, the link centered against the buttons, at the right end of the bar. */
export const Actions = styled.div`
	display: flex;
	align-items: center;
	gap: 8px;
	margin-left: auto;

	${headerStacks} {
		grid-column: 2;
		grid-row: 1 / span 2;
		align-self: center;
	}
`;

/** The theme button and the marks button in the tab order and on screen alike; below 620 px they stack at the width of the wider. */
export const Controls = styled.div`
	display: flex;
	align-items: center;
	gap: var(--button-gap);

	${headerStacks} {
		flex-direction: column;
		align-items: stretch;
	}
`;

export const RepositoryLink = styled.a`
	display: flex;
	flex: none;
	padding: 4px;
	border-radius: var(--radius);
	color: var(--text);

	&:hover {
		color: var(--accent);
	}
`;

/** The links between the pages of the languages, the current one underlined. */
export const LanguageLinks = styled.nav`
	display: flex;
	flex-wrap: wrap;
	align-items: center;
	gap: 4px 10px;

	& a {
		padding: 1px 0;
		border-bottom: 2px solid transparent;
		text-decoration: none;
	}

	& a:hover {
		border-bottom-color: var(--border-strong);
	}

	& a[aria-current='page'] {
		border-bottom-color: var(--accent);
		color: var(--text);
		font-weight: 600;
	}
`;

/** The check marks the on state; the label is centered alone when it is off, and with the check as one group when it is on. */
export const Check = styled.svg`
	flex: none;
	width: 12px;
	height: 12px;
	stroke-width: 2;
	stroke: currentColor;
	fill: none;
`;

/** Filled with the accent when on, as the picked segment of a picker is. */
export const MarksButton = styled(Button)`
	display: inline-flex;
	align-items: center;
	justify-content: center;
	min-width: 110px;
	white-space: nowrap;
	gap: 6px;

	&[aria-pressed='true'],
	&[aria-pressed='true']:hover:not(:disabled) {
		border-color: var(--accent);
		background: var(--accent);
		color: var(--surface);
	}

	&[aria-pressed='true']:focus-visible {
		outline-color: var(--surface);
	}

	/* Windows High Contrast replaces every background color, and the check is the cue that does not depend on one. */
	@media (forced-colors: active) {
		&[aria-pressed='true'],
		&[aria-pressed='true']:hover:not(:disabled) {
			forced-color-adjust: none;
			border-color: ButtonText;
			background: Highlight;
			color: HighlightText;
		}

		&[aria-pressed='true']:focus-visible {
			outline-color: HighlightText;
		}
	}
`;
