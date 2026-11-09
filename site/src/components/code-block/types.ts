import type { Snippet } from '../../state/model.ts';

export type Mark = 'add' | 'del';

export interface RegionProps {
	$dimmed: boolean;
	$current: boolean;
}

export interface CodeBlockProps {
	snippet: Snippet;

	/** The accessible name of the region, which takes the focus so that a keyboard can scroll it. */
	label: string;

	/** The lines the other side does not have, and the mark they carry; a block with no mark shows no gutter marks. */
	marked?: ReadonlySet<number>;
	mark?: Mark;

	/** A block with no gutter, which insets its lines instead. */
	plain?: boolean;
	dimmed?: boolean;
	current?: boolean;
}
