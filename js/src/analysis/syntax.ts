import type { TSESTree } from '@typescript-eslint/utils';

type Node = TSESTree.Node;

/** Strips the wrappers that change a type or a chain but not what a statement does. */
function unwrapExpression(node: Node): Node {
	let current = node;

	while (
		current.type === 'ChainExpression' ||
		current.type === 'TSAsExpression' ||
		current.type === 'TSNonNullExpression' ||
		current.type === 'TSTypeAssertion' ||
		current.type === 'TSSatisfiesExpression'
	) {
		current = current.expression;
	}

	return current;
}

/** Reads the name of a member key, or undefined when it is computed from something other than a literal. */
export function readKeyName(node: Node, computed: boolean): string | undefined {
	if (node.type === 'Identifier' && !computed) {
		return node.name;
	}

	if (node.type === 'PrivateIdentifier') {
		return `#${node.name}`;
	}

	if (node.type === 'Literal' && (typeof node.value === 'string' || typeof node.value === 'number')) {
		return String(node.value);
	}

	return undefined;
}

/** Reads the names along a callee from left to right, such as `expect`, `rejects` and `toThrow`; undefined when a link has no name. */
function readCalleeNames(callee: Node): string[] | undefined {
	const names: string[] = [];
	let current: Node = callee;

	for (;;) {
		current = unwrapExpression(current);

		if (current.type === 'Identifier') {
			names.unshift(current.name);
			return names;
		}

		if (current.type === 'ThisExpression' || current.type === 'Super') {
			names.unshift(current.type === 'Super' ? 'super' : 'this');
			return names;
		}

		if (current.type === 'CallExpression') {
			current = current.callee;
			continue;
		}

		if (current.type !== 'MemberExpression') {
			return undefined;
		}

		const name = readKeyName(current.property, current.computed);

		if (name === undefined) {
			return undefined;
		}

		names.unshift(name);
		current = current.object;
	}
}

export interface StatementCall {
	readonly names: readonly string[];
	readonly isAwaited: boolean;
}

/** Reads the call an expression statement makes, with an `await` in front of it when there is one. */
export function readStatementCall(statement: Node): StatementCall | 'await' | undefined {
	if (statement.type !== 'ExpressionStatement') {
		return undefined;
	}

	let expression = unwrapExpression(statement.expression);
	const isAwaited = expression.type === 'AwaitExpression';

	if (expression.type === 'AwaitExpression') {
		expression = unwrapExpression(expression.argument);
	}

	if (expression.type !== 'CallExpression') {
		return isAwaited ? 'await' : undefined;
	}

	const names = readCalleeNames(expression.callee);

	return names === undefined ? (isAwaited ? 'await' : undefined) : { names, isAwaited };
}

/** Tells whether a statement is a call, awaited or not, through a chain that starts at `super`, such as `super(a)` and `super.load()`. */
export function isSuperCall(node: Node | undefined): boolean {
	const call = node === undefined ? undefined : readStatementCall(node);
	return typeof call === 'object' && call.names[0] === 'super';
}

/** Tells whether a statement assigns: `=`, a compound assignment such as `+=`, or an increment or decrement. */
export function isAssignment(node: Node): boolean {
	if (node.type !== 'ExpressionStatement') {
		return false;
	}

	const { type } = unwrapExpression(node.expression);

	return type === 'AssignmentExpression' || type === 'UpdateExpression';
}

export type JumpStatement = TSESTree.ReturnStatement | TSESTree.ThrowStatement | TSESTree.BreakStatement | TSESTree.ContinueStatement;

/** Tells whether a statement leaves its block: `return`, `throw`, `break` or `continue`. */
export function isJump(node: Node): node is JumpStatement {
	return node.type === 'ReturnStatement' || node.type === 'ThrowStatement' || node.type === 'BreakStatement' || node.type === 'ContinueStatement';
}

/** Tells whether a statement is a lone jump, written with braces or without. */
function isLoneJump(node: Node): boolean {
	if (node.type === 'BlockStatement') {
		const [only] = node.body;
		return node.body.length === 1 && only !== undefined && isJump(only);
	}

	return isJump(node);
}

/** Tells whether a statement is an `if` with no `else` that leaves the block, or a call of one of the listed helpers. */
export function isGuard(node: Node, helpers: ReadonlySet<string>): boolean {
	if (node.type === 'IfStatement') {
		return node.alternate === null && isLoneJump(node.consequent);
	}

	const call = readStatementCall(node);

	return typeof call === 'object' && call.names.some((name) => helpers.has(name));
}

/** Tells whether a statement is a directive of the prologue, such as `'use strict'`. */
export function isDirective(node: Node): boolean {
	return node.type === 'ExpressionStatement' && typeof node.directive === 'string';
}

/** Tells whether a statement imports, which is what the import section of a file holds. */
export function isImport(node: Node): boolean {
	return node.type === 'ImportDeclaration' || node.type === 'TSImportEqualsDeclaration';
}

/** Reads the declaration an `export` statement carries, or the statement itself. */
export function unwrapExport(node: Node): Node {
	if (node.type === 'ExportNamedDeclaration' && node.declaration) {
		return node.declaration;
	}

	if (node.type === 'ExportDefaultDeclaration') {
		return node.declaration;
	}

	return node;
}
