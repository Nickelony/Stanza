import { isAssertion } from './assertions.ts';
import type { Environment } from './environment.ts';
import { statementKinds } from './kinds.ts';
import { readAuthoredSections } from './sections.ts';
import { readDirectReceiver, readStatementCall } from './syntax.ts';
import type { Container } from './types.ts';

type Node = Container['node'];

/** Reads the selected, concrete receiver of an ordinary operation, excluding its terminal member. */
export function readReceiverKey(node: Node, environment: Environment, includeWrites = false): string | undefined {
	if (environment.receiverRoots.size === 0 || node.loc.start.line !== node.loc.end.line) {
		return undefined;
	}

	const call = readStatementCall(node);

	if (typeof call === 'object' && (environment.testRoots.has(call.names[0] ?? '') || isAssertion(node, environment))) {
		return undefined;
	}

	const written = readDirectReceiver(node, includeWrites);
	const names = written !== undefined && ['this', 'self', 'super'].includes(written[0] ?? '') ? written.slice(1) : written;

	if (
		names === undefined ||
		names.length === 0 ||
		(!environment.receiverRoots.has('*') && !names.some((name) => environment.receiverRoots.has(name)))
	) {
		return undefined;
	}

	return environment.receiverComparison === 'path' ? names.join('.') : names[0];
}

export interface ReceiverGroups {
	readonly keys: readonly (string | undefined)[];
	readonly starts: readonly number[];
	readonly ends: readonly number[];
	readonly edges: ReadonlySet<number>;
	readonly sections: ReadonlySet<number>;
}

/** Reads contiguous selected receiver groups and their outer boundaries, preserving comment and multiline barriers. */
export function readReceiverGroups(container: Container, environment: Environment): ReceiverGroups {
	const keys = container.units.map((unit) =>
		statementKinds.has(container.kind) && !unit.isMultiLine ? readReceiverKey(unit.node, environment, true) : undefined,
	);

	const starts: number[] = [];
	const ends: number[] = [];

	for (let index = 0; index < keys.length; index += 1) {
		const previous = starts[index - 1];

		starts[index] =
			keys[index] !== undefined &&
			previous !== undefined &&
			keys[index] === keys[index - 1] &&
			container.gaps[index - 1]?.hasComment === false &&
			!container.gaps[index - 1]?.isJoined
				? previous
				: index;
	}

	for (let index = keys.length - 1; index >= 0; index -= 1) {
		ends[index] = starts[index] === starts[index + 1] ? (ends[index + 1] ?? index) : index;
	}

	const edges = new Set<number>();

	for (let index = 0; index < container.gaps.length; index += 1) {
		if (starts[index] === starts[index + 1] || container.gaps[index]?.isJoined === true) {
			continue;
		}

		const before = keys[index] !== undefined && (ends[index] ?? index) > (starts[index] ?? index);
		const after = keys[index + 1] !== undefined && (ends[index + 1] ?? index + 1) > (starts[index + 1] ?? index + 1);

		if (before || after) {
			edges.add(index);
		}
	}

	const links = container.gaps.map(
		(gap, index) => keys[index] !== undefined && starts[index] === starts[index + 1] && !gap.hasComment && !gap.isJoined,
	);

	const sections = readAuthoredSections(container, links);

	return { keys, starts, ends, edges, sections };
}

export type ReceiverGroupReader = (container: Container) => ReceiverGroups;

/** Tells whether explicit receiver separation terminates an ordinary run at this gap. */
export function isReceiverEdge(container: Container, index: number, environment: Environment, receiverGroupsOf: ReceiverGroupReader): boolean {
	return environment.receiverSeparationEnabled && receiverGroupsOf(container).edges.has(index);
}
