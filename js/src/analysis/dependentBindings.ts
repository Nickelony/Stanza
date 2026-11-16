import type { TSESTree } from '@typescript-eslint/utils';

import type { ValueReferences } from './bindings.ts';
import { statementKinds } from './kinds.ts';
import { readAuthoredSections } from './sections.ts';
import type { Container, Unit } from './types.ts';

interface Binding {
	readonly statement: TSESTree.Node;
	readonly value: TSESTree.Node;
}

/** Patterns, resources and exported or ambient declarations do not participate in ordinary binding chains. */
function readBinding(unit: Unit): Binding | undefined {
	const node = unit.node;

	if (
		unit.isMultiLine ||
		node.type !== 'VariableDeclaration' ||
		node.declare ||
		!['const', 'let', 'var'].includes(node.kind) ||
		node.declarations.length !== 1
	) {
		return undefined;
	}

	const declaration = node.declarations[0];

	return declaration?.id.type === 'Identifier' && declaration.init !== null ? { statement: node, value: declaration.init } : undefined;
}

export interface DependentBindingGroups {
	readonly links: readonly boolean[];
	readonly sections: ReadonlySet<number>;
}

/** Reads each binding and each consuming initializer once, then joins only adjacent runtime value dependencies. */
export function readDependentBindingGroups(container: Container, references: ValueReferences): DependentBindingGroups {
	const bindings = container.units.map((unit) => (statementKinds.has(container.kind) ? readBinding(unit) : undefined));

	const links = container.gaps.map((gap, index) => {
		const previous = bindings[index];
		const next = bindings[index + 1];

		return (
			!gap.hasComment && !gap.isJoined && previous !== undefined && next !== undefined && references.readsImmediately(previous.statement, next.value)
		);
	});

	const sections = readAuthoredSections(container, links);

	return { links, sections };
}

export type DependentBindingReader = (container: Container) => DependentBindingGroups;
