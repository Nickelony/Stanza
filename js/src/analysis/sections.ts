import type { Container } from './types.ts';

/** Preserves a boundary between tight sections and an isolated unit between two tight sections. */
export function isSectionBoundary(gapIndex: number, isLoose: (index: number) => boolean, isTight: (index: number) => boolean): boolean {
	const before = isTight(gapIndex - 1);
	const after = isTight(gapIndex + 1);

	if (before && after) {
		return true;
	}

	// A lone unit has a blank line on its other side too, which sets it apart from a second section.
	if (before) {
		return isLoose(gapIndex + 1) && isTight(gapIndex + 2);
	}

	return after && isLoose(gapIndex - 1) && isTight(gapIndex - 2);
}

/** Reads authored section boundaries from a keeper's eligible links before any prospective gap projection. */
export function readAuthoredSections(container: Container, links: readonly boolean[]): ReadonlySet<number> {
	const isLoose = (index: number): boolean => {
		const range = container.gaps[index]?.blankRange;
		return links[index] === true && range !== undefined && range[1] > range[0];
	};

	const isTight = (index: number): boolean => links[index] === true && !isLoose(index);
	const sections = new Set<number>();

	for (let index = 0; index < links.length; index += 1) {
		if (isLoose(index) && isSectionBoundary(index, isLoose, isTight)) {
			sections.add(index);
		}
	}

	return sections;
}
