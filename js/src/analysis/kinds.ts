import type { ContainerKind } from './types.ts';

/** The containers whose units are statements. */
export const statementKinds: ReadonlySet<ContainerKind> = new Set(['program', 'block', 'static-block', 'module-block', 'switch-case']);

/** The containers whose units are members of a type. */
export const memberKinds: ReadonlySet<ContainerKind> = new Set(['class-body', 'interface-body', 'type-literal']);
