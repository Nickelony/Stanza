import { isGuard } from './syntax.ts';
import type { Container, Unit } from './types.ts';

/** Counts the units from `start` on that pass the test, up to the first that does not. */
export function countLeadingUnits(container: Container, start: number, test: (unit: Unit) => boolean): number {
	let index = start;

	for (let unit = container.units[index]; unit !== undefined && test(unit); unit = container.units[index]) {
		index += 1;
	}

	return index - start;
}

const guardCounts = new WeakMap<Container, number>();

/** Counts the guard clauses that open a container, read once per container because the guard roots of a container never change. */
export function countLeadingGuards(container: Container, guardRoots: ReadonlySet<string>): number {
	let guards = guardCounts.get(container);

	if (guards === undefined) {
		guards = countLeadingUnits(container, 0, (unit) => isGuard(unit.node, guardRoots));
		guardCounts.set(container, guards);
	}

	return guards;
}
