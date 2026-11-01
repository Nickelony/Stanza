import type { TSESTree } from '@typescript-eslint/utils';

import type { Environment } from './environment.ts';
import { memberKinds } from './kinds.ts';
import { isAssignment, readStatementCall, unwrapExport } from './syntax.ts';
import type { Container } from './types.ts';

type Node = TSESTree.Node;

export type Family = 'declaration' | 'invocation' | 'assertion' | 'await' | 'assignment' | 'test' | 'member' | 'modifier';

/** What a unit is for a run: neighbors with the same id are one run. */
export interface RunKey {
	readonly family: Family;
	readonly id: string;
}

function createKey(family: Family, detail: string): RunKey {
	return { family, id: `${family}:${detail}` };
}

function classifyCall(statement: Node, environment: Environment): RunKey | undefined {
	const call = readStatementCall(statement);

	if (call === undefined) {
		return undefined;
	}

	if (call === 'await') {
		return environment.awaitEnabled ? createKey('await', 'await') : undefined;
	}

	const [root] = call.names;

	if (root !== undefined && environment.testRoots.has(root)) {
		return createKey('test', 'test');
	}

	// The root nearest the call names the run, so `chai.expect(a)` and `expect(b)` share one and `assert.ok(c)` has another.
	const assertionRoot = call.names.findLast((name) => environment.assertionRoots.has(name));

	if (assertionRoot !== undefined) {
		return createKey('assertion', assertionRoot);
	}

	if (call.isAwaited && environment.awaitEnabled) {
		return createKey('await', 'await');
	}

	return createKey('invocation', call.names.join('.'));
}

/** Tells which run a statement belongs to, or undefined when it belongs to none. */
function classifyStatement(node: Node, environment: Environment): RunKey | undefined {
	const target = unwrapExport(node);
	const prefix = target === node ? '' : 'export ';

	if (target.type === 'VariableDeclaration') {
		return createKey('declaration', `${prefix}${target.declare ? 'declare ' : ''}${target.kind}`);
	}

	if (target.type === 'TSTypeAliasDeclaration') {
		return createKey('declaration', `${prefix}type`);
	}

	if (isAssignment(target)) {
		return createKey('assignment', 'assignment');
	}

	return classifyCall(target, environment);
}

/** Tells which kind of member a class, interface or type literal member is. */
function classifyMember(node: Node): RunKey | undefined {
	switch (node.type) {
		case 'PropertyDefinition':
		case 'TSAbstractPropertyDefinition':
		case 'AccessorProperty':
		case 'TSAbstractAccessorProperty':
		case 'TSPropertySignature':
			return createKey('member', 'property');

		case 'MethodDefinition':
		case 'TSAbstractMethodDefinition':
			return createKey('member', node.kind === 'method' ? 'method' : node.kind === 'constructor' ? 'constructor' : 'accessor');

		case 'TSMethodSignature':
			return createKey('member', node.kind === 'method' ? 'method' : 'accessor');

		case 'StaticBlock':
			return createKey('member', 'static-block');

		case 'TSCallSignatureDeclaration':
			return createKey('member', 'call');

		case 'TSConstructSignatureDeclaration':
			return createKey('member', 'construct');

		case 'TSIndexSignature':
			return createKey('member', 'index');

		default:
			return undefined;
	}
}

/** Tells which access, `static` and `readonly` modifiers a member writes; a member with no access is public, and a `#name` member is private. */
function readModifiers(node: Node): string {
	let access: string = 'public';
	let isStatic = false;
	let isReadonly = false;

	switch (node.type) {
		case 'PropertyDefinition':
		case 'TSAbstractPropertyDefinition':
		case 'AccessorProperty':
		case 'TSAbstractAccessorProperty':
		case 'MethodDefinition':
		case 'TSAbstractMethodDefinition':
			access = node.key.type === 'PrivateIdentifier' ? 'private' : (node.accessibility ?? 'public');
			isStatic = node.static;
			isReadonly = 'readonly' in node && node.readonly === true;

			break;

		case 'TSPropertySignature':
			isReadonly = node.readonly === true;
			break;

		case 'StaticBlock':
			isStatic = true;
			break;

		default:
			break;
	}

	return `${access}:${isStatic ? 'static' : 'instance'}:${isReadonly ? 'readonly' : 'mutable'}`;
}

/** Tells which kind of member, with which modifiers, a member of a class, interface or type literal is. */
function classifyModifierGroup(node: Node): RunKey | undefined {
	const kind = classifyMember(node);
	return kind === undefined ? undefined : createKey('modifier', `${kind.id}:${readModifiers(node)}`);
}

export interface Runs {
	readonly keys: readonly (RunKey | undefined)[];

	/** The index of the first unit of the run that holds each unit. */
	readonly starts: readonly number[];

	/** The index of the last unit of the run that holds each unit. */
	readonly ends: readonly number[];
}

/** Splits the units of a container into runs of neighbors that share a key; `byModifiers` reads the members by their modifiers too. */
export function readRuns(container: Container, environment: Environment, byModifiers = false): Runs {
	const classify = !memberKinds.has(container.kind) ? classifyStatement : byModifiers ? classifyModifierGroup : classifyMember;
	const keys = container.units.map((unit) => (unit.isShebang ? undefined : classify(unit.node, environment)));
	const starts: number[] = [];
	const ends: number[] = [];

	for (let index = 0; index < keys.length; index += 1) {
		const key = keys[index];
		const previousStart = starts[index - 1];
		const continuesRun = key !== undefined && previousStart !== undefined && keys[index - 1]?.id === key.id;

		starts.push(continuesRun ? previousStart : index);
	}

	for (let index = keys.length - 1; index >= 0; index -= 1) {
		const next = ends[index + 1];
		const continuesRun = next !== undefined && starts[index + 1] === starts[index];

		ends[index] = continuesRun ? next : index;
	}

	return { keys, starts, ends };
}

/** Reads the families of the runs of two or more units that end or begin at a gap. */
export function readBoundaryFamilies(runs: Runs, gapIndex: number): Family[] {
	const before = runs.keys[gapIndex];
	const after = runs.keys[gapIndex + 1];

	if (before !== undefined && before.id === after?.id) {
		return [];
	}

	const families: Family[] = [];

	if (before !== undefined && (runs.starts[gapIndex] as number) < gapIndex) {
		families.push(before.family);
	}

	if (after !== undefined && (runs.ends[gapIndex + 1] as number) > gapIndex + 1) {
		families.push(after.family);
	}

	return families;
}
