import type { TSESTree } from '@typescript-eslint/utils';

import { readKeyName, unwrapExport } from './syntax.ts';
import type { Container, Unit } from './types.ts';

type Node = TSESTree.Node;

/** Reads the name of a function declaration or overload signature. */
function readFunctionName(node: Node): string | undefined {
	if (node.type !== 'FunctionDeclaration' && node.type !== 'TSDeclareFunction') {
		return undefined;
	}

	return node.id?.name ?? 'default';
}

function isFunctionOverload(previous: Node, next: Node): boolean {
	const signature = unwrapExport(previous);
	const following = unwrapExport(next);

	return signature.type === 'TSDeclareFunction' && readFunctionName(signature) === readFunctionName(following);
}

type ClassMethod = TSESTree.MethodDefinition | TSESTree.TSAbstractMethodDefinition;

function isClassMethod(node: Node): node is ClassMethod {
	return node.type === 'MethodDefinition' || node.type === 'TSAbstractMethodDefinition';
}

/** Tells whether two members share a key whose name can be read, because two unreadable keys are not known to match. */
function haveSameKnownKey(previous: ClassMethod, next: ClassMethod): boolean {
	const name = readKeyName(previous.key, previous.computed);
	return name !== undefined && name === readKeyName(next.key, next.computed);
}

/** Tells whether two class members are overloads of one method: the first has no body and both share a name. */
function isMethodOverload(previous: ClassMethod, next: ClassMethod): boolean {
	return previous.value.type === 'TSEmptyBodyFunctionExpression' && previous.static === next.static && haveSameKnownKey(previous, next);
}

/** Tells whether two class members are the getter and the setter of one property. */
function isAccessorPair(previous: ClassMethod, next: ClassMethod): boolean {
	const kinds = new Set([previous.kind, next.kind]);
	return kinds.has('get') && kinds.has('set') && previous.static === next.static && haveSameKnownKey(previous, next);
}

/** Tells whether two interface members are signatures of one overloaded method, call or constructor. */
function isSignatureOverload(previous: Node, next: Node): boolean {
	if (previous.type === 'TSMethodSignature' && next.type === 'TSMethodSignature') {
		const name = readKeyName(previous.key, previous.computed);
		return name !== undefined && name === readKeyName(next.key, next.computed);
	}

	return (
		(previous.type === 'TSCallSignatureDeclaration' && next.type === 'TSCallSignatureDeclaration') ||
		(previous.type === 'TSConstructSignatureDeclaration' && next.type === 'TSConstructSignatureDeclaration')
	);
}

/** The JSX containers read so far, with whether an element among their children holds text. */
const textContainers = new WeakMap<Container, boolean>();

/** Tells whether a JSX container has text among its children, reading its units once. */
function hasText(container: Container): boolean {
	let result = textContainers.get(container);

	if (result === undefined) {
		result = container.units.some((unit) => unit.isText);
		textContainers.set(container, result);
	}

	return result;
}

/**
 * Tells whether a blank line between two neighbors would split a pair that has to stay together.
 * An element with text among its children keeps none, because Prettier removes the blank lines of such an element.
 */
export function isGluedPair(container: Container, previous: Unit, next: Unit): boolean {
	const first = previous.node;
	const second = next.node;

	if (container.kind === 'jsx') {
		return hasText(container);
	}

	if (container.kind === 'switch') {
		return first.type === 'SwitchCase' && first.consequent.length === 0;
	}

	if (container.kind === 'class-body') {
		return isClassMethod(first) && isClassMethod(second) && (isMethodOverload(first, second) || isAccessorPair(first, second));
	}

	if (container.kind === 'interface-body' || container.kind === 'type-literal') {
		return isSignatureOverload(first as Node, second as Node);
	}

	return isFunctionOverload(first as Node, second as Node);
}
