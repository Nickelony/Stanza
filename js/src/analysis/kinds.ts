import type { ContainerKind } from './types.ts';

/** The containers whose units are statements. */
export const statementKinds: ReadonlySet<ContainerKind> = new Set(['program', 'block', 'static-block', 'module-block', 'switch-case']);

/** The containers whose statements the guard rules judge: a block, a switch case and the top level of a file, as the C# rules do. */
export const guardKinds: ReadonlySet<ContainerKind> = new Set(['program', 'block', 'switch-case']);

/** The containers whose units are members of a type. */
export const memberKinds: ReadonlySet<ContainerKind> = new Set(['class-body', 'interface-body', 'type-literal']);
