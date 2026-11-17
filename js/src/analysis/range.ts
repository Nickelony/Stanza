/** Lists the whole numbers from `start` up to, and not including, `end`; an empty list where `end` does not pass `start`. */
export function range(start: number, end: number): number[] {
	return Array.from({ length: Math.max(0, end - start) }, (_, offset) => start + offset);
}
