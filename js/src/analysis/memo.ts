import type { Container } from './types.ts';

/** Wraps a read of a container so that each container is read once. */
export function readOncePerContainer<Result extends object | number | boolean>(
	read: (container: Container) => Result,
): (container: Container) => Result {
	const results = new WeakMap<Container, Result>();

	return (container) => {
		let result = results.get(container);

		if (result === undefined) {
			result = read(container);
			results.set(container, result);
		}

		return result;
	};
}
