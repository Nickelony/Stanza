export function* pages(list: string[]): Generator<string> {
	const first = list[0];
	yield first;
	yield* list.slice(1);
	const last = list.at(-1);

	yield last ?? '';

	yield 'end';
	return;
}
export class Pager {
	*[Symbol.iterator](): Generator<number> {
		console.log('start');
		yield 1;
		yield 2;
		console.log('stop');
	}
}
export function describeKey(key: string): string {
	switch (key) {
		case 'a':

		case 'b':
			return 'letter';
		case 'c':
		case 'd':
			return 'other';

		case 'e':
		// Falls through to the default label.

		default:

		case 'f':
			return 'none';
	}
}
