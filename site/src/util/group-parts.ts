import type { Block, ExampleRef } from '../state/model.ts';

export type Grouped<P> = { kind: 'notes'; blocks: Block[] } | Exclude<P, Block>;

const isBlock = (part: { kind: string }): part is Block => part.kind === 'p' || part.kind === 'ul' || part.kind === 'table';

/** Lays out the parts of a page in the order it writes them: a run of text shares one box, and an example or the value table stands where the page puts it. */
export function groupParts<P extends Block | ExampleRef | { kind: 'values' }>(parts: readonly P[]): Grouped<P>[] {
	const grouped: Grouped<P>[] = [];

	let notes: Block[] | null = null;

	for (const part of parts) {
		if (isBlock(part)) {
			if (notes === null) {
				notes = [];
				grouped.push({ kind: 'notes', blocks: notes });
			}

			notes.push(part);
		} else {
			grouped.push(part as Exclude<P, Block>);
			notes = null;
		}
	}

	return grouped;
}
