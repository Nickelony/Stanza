import type { TSESLint, TSESTree } from '@typescript-eslint/utils';

import { unwrapExpression } from './syntax.ts';

type Node = TSESTree.Node;

/** The keys that lead to a type, which reads no variable of the code. */
const typeKeys: ReadonlySet<string> = new Set(['typeAnnotation', 'typeArguments', 'typeParameters', 'returnType']);

/** Reads the child nodes of a node, leaving out its parent and its types. */
function readChildren(node: Node): Node[] {
	const children: Node[] = [];

	for (const [key, value] of Object.entries(node)) {
		if (key === 'parent' || typeKeys.has(key)) {
			continue;
		}

		for (const item of Array.isArray(value) ? (value as unknown[]) : [value]) {
			if (typeof item === 'object' && item !== null && typeof (item as { type?: unknown }).type === 'string') {
				children.push(item as Node);
			}
		}
	}

	return children;
}

/** Visits a node and every node below it, the parent before its children. */
function walk(node: Node, visit: (node: Node, parent: Node | undefined) => void, parent?: Node): void {
	visit(node, parent);

	for (const child of readChildren(node)) {
		walk(child, visit, node);
	}
}

/** Tells whether an identifier names a property or a key instead of reading a variable, as `b` does in `a.b` and in `{ b: 1 }`. */
function isPropertyName(node: TSESTree.Identifier, parent: Node | undefined): boolean {
	if (parent === undefined) {
		return false;
	}

	if (parent.type === 'MemberExpression') {
		return parent.property === node && !parent.computed;
	}

	if (parent.type === 'Property' || parent.type === 'PropertyDefinition' || parent.type === 'MethodDefinition') {
		return parent.key === node && !parent.computed && !(parent.type === 'Property' && parent.shorthand && parent.value === node);
	}

	return false;
}

/** Collects a pattern's binding identifiers, leaving member targets out. */
function collectPatternIdentifiers(pattern: Node | null, identifiers: Set<TSESTree.Identifier>): void {
	if (pattern !== null) {
		pattern = unwrapExpression(pattern);
	}

	switch (pattern?.type) {
		case 'Identifier':
			identifiers.add(pattern);
			break;

		case 'ObjectPattern':
			for (const property of pattern.properties) {
				collectPatternIdentifiers(property.type === 'RestElement' ? property.argument : property.value, identifiers);
			}

			break;

		case 'ArrayPattern':
			pattern.elements.forEach((element) => collectPatternIdentifiers(element, identifiers));
			break;

		case 'RestElement':
			collectPatternIdentifiers(pattern.argument, identifiers);
			break;

		case 'AssignmentPattern':
			collectPatternIdentifiers(pattern.left, identifiers);
			break;

		default:
			break;
	}
}

/** Reads the assignment an expression statement makes, through the wrappers that only change a type. */
function readAssignment(statement: Node): TSESTree.AssignmentExpression | undefined {
	if (statement.type !== 'ExpressionStatement') {
		return undefined;
	}

	const expression = unwrapExpression(statement.expression);

	return expression.type === 'AssignmentExpression' ? expression : undefined;
}

/**
 * Reads the names a statement binds: the declarators of a `const`, `let`, `var` or `using` declaration that give a value, patterns included,
 * or the target of an assignment statement. Any other statement binds none.
 */
export function readBoundNames(statement: Node): ReadonlySet<string> {
	return new Set([...readBoundIdentifiers(statement)].map((identifier) => identifier.name));
}

function readBoundIdentifiers(statement: Node): ReadonlySet<TSESTree.Identifier> {
	const identifiers = new Set<TSESTree.Identifier>();

	if (statement.type === 'VariableDeclaration') {
		for (const declarator of statement.declarations) {
			if (declarator.init !== null) {
				collectPatternIdentifiers(declarator.id, identifiers);
			}
		}

		return identifiers;
	}

	collectPatternIdentifiers(readAssignment(statement)?.left ?? null, identifiers);

	return identifiers;
}

type Binding = object | string;

export interface ValueReferences {
	readonly bindingOf: (node: Node) => Binding | undefined;
	readonly boundOf: (statement: Node) => ReadonlySet<Binding>;
	readonly readsOf: (expression: Node) => ReadonlySet<Binding>;
	readonly readsBinding: (statement: Node, expression: Node) => boolean;

	/** Tells whether an expression reads a value the statement binds as it runs: a nested function or class body runs later, and a write to the binding replaces it. */
	readonly readsImmediately: (statement: Node, expression: Node) => boolean;
}

/** Indexes native binding identities and value-read flags once per file, caching each queried expression and binding statement. */
export function readValueReferences(sourceCode: Readonly<TSESLint.SourceCode>): ValueReferences {
	let bindings: Map<Node, Binding> | undefined;

	const readIdentifiers = new Set<Node>();
	const bound = new WeakMap<Node, ReadonlySet<Binding>>();
	const reads = new WeakMap<Node, ReadonlySet<Binding>>();

	const bindingOf = (node: Node): Binding | undefined => {
		if (bindings === undefined) {
			bindings = new Map();

			for (const scope of sourceCode.scopeManager?.scopes ?? []) {
				for (const variable of scope.variables) {
					for (const identifier of variable.identifiers) {
						bindings.set(identifier, variable);
					}
				}

				for (const reference of scope.references) {
					bindings.set(reference.identifier, reference.resolved ?? reference.identifier.name);

					if (reference.isRead() && (!('isValueReference' in reference) || reference.isValueReference)) {
						readIdentifiers.add(reference.identifier);
					}
				}
			}
		}

		return bindings.get(unwrapExpression(node));
	};

	const boundOf = (statement: Node): ReadonlySet<Binding> => {
		let result = bound.get(statement);

		if (result === undefined) {
			result = new Set([...readBoundIdentifiers(statement)].flatMap((identifier) => bindingOf(identifier) ?? []));
			bound.set(statement, result);
		}

		return result;
	};

	const readsOf = (expression: Node): ReadonlySet<Binding> => {
		let result = reads.get(expression);

		if (result === undefined) {
			const referenced = new Set<Binding>();

			bindingOf(expression);

			walk(expression, (node) => {
				const binding = readIdentifiers.has(node) ? bindingOf(node) : undefined;

				if (binding !== undefined) {
					referenced.add(binding);
				}
			});

			result = referenced;
			reads.set(expression, result);
		}

		return result;
	};

	const readsImmediately = (statement: Node, expression: Node): boolean => {
		bindingOf(expression);

		const names = readBoundNames(statement);
		const targets = boundOf(statement);
		const isBound = (node: Node): boolean => targets.has(bindingOf(node) ?? '');
		const pending: [Node, Node | undefined][] = [[expression, undefined]];

		let found = false;

		for (let entry = pending.pop(); entry !== undefined; entry = pending.pop()) {
			const [node, parent] = entry;

			if (node.type === 'ArrowFunctionExpression' || node.type === 'FunctionExpression') {
				continue;
			}

			if (node.type === 'ClassExpression') {
				pending.push(...(node.superClass === null ? [] : [[node.superClass, node] as [Node, Node]]));
				continue;
			}

			if (writesBinding(node, isBound)) {
				return false;
			}

			found ||= readIdentifiers.has(node) ? isBound(node) : isJsxValueName(node, parent, names);
			pending.push(...readChildren(node).map((child): [Node, Node] => [child, node]));
		}

		return found;
	};

	return {
		bindingOf,
		boundOf,
		readsOf,
		readsBinding: (statement, expression) => [...boundOf(statement)].some((binding) => readsOf(expression).has(binding)),
		readsImmediately,
	};
}

/** Tells whether a node assigns or updates a binding, which replaces the value an earlier statement gave it. */
function writesBinding(node: Node, isBound: (node: Node) => boolean): boolean {
	if (node.type === 'UpdateExpression') {
		return isBound(unwrapExpression(node.argument));
	}

	if (node.type !== 'AssignmentExpression') {
		return false;
	}

	const targets = new Set<TSESTree.Identifier>();

	collectPatternIdentifiers(node.left, targets);

	return [...targets].some(isBound);
}

/**
 * Tells whether a JSX name reads a variable that a parser without JSX references leaves unresolved:
 * a capitalized element name, or the object of a member element name.
 */
function isJsxValueName(node: Node, parent: Node | undefined, names: ReadonlySet<string>): boolean {
	if (node.type !== 'JSXIdentifier' || !names.has(node.name)) {
		return false;
	}

	if (parent?.type === 'JSXMemberExpression') {
		return parent.object === node;
	}

	return (parent?.type === 'JSXOpeningElement' || parent?.type === 'JSXClosingElement') && parent.name === node && !/^[a-z]/.test(node.name);
}

/** The globals that read like a literal, so a seed may hold them. */
const literalGlobals: ReadonlySet<string> = new Set(['undefined', 'NaN', 'Infinity']);

/** Tells whether a value reads no variable: an identifier may only be the callee of a call or a constructor, as in `new Map()` or `BigInt(0)`. */
function readsNoVariable(value: Node): boolean {
	let reads = false;

	walk(value, (node, parent) => {
		if (node.type !== 'Identifier' || literalGlobals.has(node.name) || isPropertyName(node, parent)) {
			return;
		}

		const isCallee = (parent?.type === 'CallExpression' || parent?.type === 'NewExpression') && parent.callee === node;

		reads ||= !isCallee;
	});

	return !reads;
}

/** Reads the name a statement seeds: one plain name given a value that reads no variable, such as `0`, `[]`, `{}`, `null` or `new Map()`. */
export function readSeededName(statement: Node): string | undefined {
	let target: Node | undefined;
	let value: Node | null | undefined;

	if (statement.type === 'VariableDeclaration') {
		const [declarator] = statement.declarations;

		if (statement.declarations.length !== 1 || declarator === undefined) {
			return undefined;
		}

		target = declarator.id;
		value = declarator.init;
	} else {
		const assignment = readAssignment(statement);

		if (assignment?.operator !== '=') {
			return undefined;
		}

		target = assignment.left;
		value = assignment.right;
	}

	if (target?.type !== 'Identifier' || value === null || value === undefined) {
		return undefined;
	}

	return readsNoVariable(value) ? target.name : undefined;
}

export type Loop = TSESTree.ForStatement | TSESTree.ForInStatement | TSESTree.ForOfStatement | TSESTree.WhileStatement | TSESTree.DoWhileStatement;

/** Tells whether a statement is a loop: `for`, `for...in`, `for...of`, `while` or `do...while`. */
export function isLoop(node: Node): node is Loop {
	return (
		node.type === 'ForStatement' ||
		node.type === 'ForInStatement' ||
		node.type === 'ForOfStatement' ||
		node.type === 'WhileStatement' ||
		node.type === 'DoWhileStatement'
	);
}

/** The methods of the built-in collections that add to them, so `items.push(x)` builds `items` but `client.send(x)` only uses `client`. */
const builders: ReadonlySet<string> = new Set(['push', 'unshift', 'add', 'set', 'splice']);

/** Tells whether a node assigns the name, updates it or calls a builder method on it. */
function buildsAt(node: Node, matches: (target: Node) => boolean): boolean {
	if (node.type === 'AssignmentExpression') {
		return writesMatchingTarget(node.left, matches);
	}

	if (node.type === 'UpdateExpression') {
		return matches(unwrapExpression(node.argument));
	}

	if (node.type !== 'CallExpression' || node.callee.type !== 'MemberExpression' || node.callee.computed) {
		return false;
	}

	const { object, property } = node.callee;

	return property.type === 'Identifier' && builders.has(property.name) && matches(unwrapExpression(object));
}

function writesMatchingTarget(target: Node, matches: (target: Node) => boolean): boolean {
	const node = unwrapExpression(target);

	switch (node.type) {
		case 'Identifier':
			return matches(node);

		case 'ObjectPattern':
			return node.properties.some((property) => writesMatchingTarget(property.type === 'RestElement' ? property.argument : property.value, matches));

		case 'ArrayPattern':
			return node.elements.some((element) => element !== null && writesMatchingTarget(element, matches));

		case 'RestElement':
			return writesMatchingTarget(node.argument, matches);

		case 'AssignmentPattern':
			return writesMatchingTarget(node.left, matches);

		default:
			return false;
	}
}

/** Reads the names each loop body builds once, so every seed above a loop is a lookup and a run of seeds costs no more than its length. */
export function readSeedBuilder(references: ValueReferences): (statement: Node, loop: Loop, name: string) => boolean {
	const builtOf = new WeakMap<Loop, Map<string, Set<Binding | undefined>>>();

	const readBuilt = (loop: Loop): Map<string, Set<Binding | undefined>> => {
		const built = new Map<string, Set<Binding | undefined>>();
		const pending: Node[] = [loop.body];

		// Each built name is recorded and the target reads as unmatched, so every target of a node is visited.
		const record = (target: Node): boolean => {
			if (target.type === 'Identifier') {
				const bindings = built.get(target.name) ?? new Set();

				bindings.add(references.bindingOf(target));
				built.set(target.name, bindings);
			}

			return false;
		};

		while (pending.length > 0) {
			const node = pending.pop();

			// A nested function or class body runs later, so what it builds is no accumulation of the loop.
			if (
				node === undefined ||
				node.type === 'FunctionDeclaration' ||
				node.type === 'FunctionExpression' ||
				node.type === 'ArrowFunctionExpression' ||
				node.type === 'ClassDeclaration' ||
				node.type === 'ClassExpression'
			) {
				continue;
			}

			buildsAt(node, record);
			pending.push(...readChildren(node));
		}

		return built;
	};

	return (statement, loop, name) => {
		const target = statement.type === 'VariableDeclaration' ? statement.declarations[0]?.id : readAssignment(statement)?.left;
		const seeded = target === undefined ? undefined : references.bindingOf(target);

		let built = builtOf.get(loop);

		if (built === undefined) {
			built = readBuilt(loop);
			builtOf.set(loop, built);
		}

		return built.get(name)?.has(seeded) === true;
	};
}

/** Tells whether a node is `null` or `undefined`. */
function isNullish(node: Node): boolean {
	return (node.type === 'Literal' && node.value === null && !('regex' in node)) || (node.type === 'Identifier' && node.name === 'undefined');
}

/** Tells whether a node is a variable name, restricted to the given names when present. */
function isBoundName(node: Node, names: ReadonlySet<string> | undefined): boolean {
	return node.type === 'Identifier' && (names === undefined || names.has(node.name));
}

/**
 * Tells whether the condition of an assertion narrows one of the names: the name itself, `x != null`, `x !== undefined`,
 * `typeof x === 'string'`, `x instanceof T`, or an `&&` that holds one of these.
 */
export function narrowsAnyName(test: Node, names?: ReadonlySet<string>): boolean {
	if (test.type === 'LogicalExpression') {
		return test.operator === '&&' && (narrowsAnyName(test.left, names) || narrowsAnyName(test.right, names));
	}

	if (test.type !== 'BinaryExpression') {
		return isBoundName(test, names);
	}

	const { operator, left, right } = test;

	if (operator === 'instanceof') {
		return isBoundName(left, names);
	}

	if (operator === '!=' || operator === '!==') {
		return (isBoundName(left, names) && isNullish(right)) || (isNullish(left) && isBoundName(right, names));
	}

	if (operator === '==' || operator === '===') {
		const isTypeOf = (side: Node): boolean => side.type === 'UnaryExpression' && side.operator === 'typeof' && isBoundName(side.argument, names);
		const isText = (side: Node): boolean => side.type === 'Literal' && typeof side.value === 'string';

		return (isTypeOf(left) && isText(right)) || (isText(left) && isTypeOf(right));
	}

	return false;
}
