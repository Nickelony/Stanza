/** A number with its noun in the singular or the plural, such as `1 rule` or `3 rules`. */
export function counted(number: number, noun: string): string {
	return `${number} ${noun}${number === 1 ? '' : 's'}`;
}
