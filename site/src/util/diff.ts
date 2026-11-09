export interface LineDiff {
	/** The indexes of the lines of the first text that the second text does not keep. */
	removed: Set<number>;

	/** The indexes of the lines of the second text that the first text does not have. */
	added: Set<number>;
}

/** Compares two texts line by line, by the longest common subsequence; trailing whitespace does not count. */
export function diffLines(before: readonly string[], after: readonly string[]): LineDiff {
	const a = before.map((line) => line.trimEnd());
	const b = after.map((line) => line.trimEnd());
	const table: number[][] = Array.from({ length: a.length + 1 }, () => Array.from({ length: b.length + 1 }, () => 0));

	for (let i = a.length - 1; i >= 0; i--) {
		for (let j = b.length - 1; j >= 0; j--) {
			table[i]![j] = a[i] === b[j] ? table[i + 1]![j + 1]! + 1 : Math.max(table[i + 1]![j]!, table[i]![j + 1]!);
		}
	}

	const removed = new Set<number>();
	const added = new Set<number>();

	let i = 0;
	let j = 0;

	while (i < a.length && j < b.length) {
		if (a[i] === b[j]) {
			i++;
			j++;
		} else if (table[i + 1]![j]! >= table[i]![j + 1]!) {
			removed.add(i++);
		} else {
			added.add(j++);
		}
	}

	while (i < a.length) {
		removed.add(i++);
	}

	while (j < b.length) {
		added.add(j++);
	}

	return { removed, added };
}
