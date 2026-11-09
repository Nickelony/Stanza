import type { TSESLint, TSESTree } from '@typescript-eslint/utils';

import { isGluedPair } from './gluing.ts';
import type { Container, ContainerKind, Gap, Unit } from './types.ts';

type Node = TSESTree.Node;
type SourceCode = Readonly<TSESLint.SourceCode>;

function createUnit(node: Node, overrides: Partial<Unit> = {}): Unit {
	const { start, end } = node.loc;

	return {
		node,
		firstLine: start.line,
		lastLine: end.line,
		isMultiLine: start.line !== end.line,
		isShebang: false,
		isText: false,
		jsxLeadLine: undefined,
		jsxLeadTexts: [],
		...overrides,
	};
}

/** Tells whether a JSX child is a `{/* note *\/}` comment. */
function isJsxComment(node: Node): boolean {
	return node.type === 'JSXExpressionContainer' && node.expression.type === 'JSXEmptyExpression';
}

/** Tells whether a JSX child is `{' '}`, which Prettier reads as a space of the text. */
function isJsxSpace(node: Node): boolean {
	return node.type === 'JSXExpressionContainer' && node.expression.type === 'Literal' && node.expression.value === ' ';
}

/** Builds the units of a JSX element: line breaks between children are insignificant, text is content. */
function createJsxUnits(children: readonly TSESTree.JSXChild[], sourceCode: SourceCode): Unit[] {
	const units: Unit[] = [];

	for (const child of children) {
		if (child.type === 'JSXText' && child.value.trim() === '' && child.value.includes('\n')) {
			continue;
		}

		units.push(createUnit(child, { isText: child.type === 'JSXText' || isJsxSpace(child) }));
	}

	const merged: Unit[] = [];

	for (const unit of units.toReversed()) {
		const following = merged[0];

		if (
			isJsxComment(unit.node) &&
			following !== undefined &&
			!following.isText &&
			(following.jsxLeadLine ?? following.firstLine) - unit.lastLine <= 1
		) {
			merged[0] = {
				...following,
				jsxLeadLine: unit.firstLine,
				jsxLeadTexts: [...sourceCode.getCommentsInside(unit.node).map((comment) => comment.value), ...following.jsxLeadTexts],
			};

			continue;
		}

		merged.unshift(unit);
	}

	return merged;
}

/** Reads the text of the shebang line, which is no node and so needs a unit of its own. */
function createShebangUnit(program: TSESTree.Program, sourceCode: SourceCode): Unit | undefined {
	if (!sourceCode.text.startsWith('#!')) {
		return undefined;
	}

	return createUnit(program, { firstLine: 1, lastLine: 1, isMultiLine: false, isShebang: true });
}

interface Members {
	readonly kind: ContainerKind;
	readonly units: Unit[];
}

/** Reads the units of the nodes that hold the elements of a list, or undefined for any other node. */
function readElements(node: Node): Members | undefined {
	switch (node.type) {
		case 'ObjectExpression':
			return { kind: 'object', units: node.properties.map((child) => createUnit(child)) };

		case 'ArrayExpression':
			return { kind: 'array', units: node.elements.flatMap((child) => (child === null ? [] : [createUnit(child)])) };

		case 'TSEnumDeclaration':
			// A parser older than the enum body node keeps the members on the declaration.
			return {
				kind: 'enum',
				units: (node.body?.members ?? (node as { members?: TSESTree.TSEnumMember[] }).members ?? []).map((child) => createUnit(child)),
			};

		case 'CallExpression':
		case 'NewExpression':
			return { kind: 'arguments', units: node.arguments.map((child) => createUnit(child)) };

		case 'FunctionDeclaration':
		case 'FunctionExpression':
		case 'ArrowFunctionExpression':
		case 'TSDeclareFunction':
		case 'TSEmptyBodyFunctionExpression':
		case 'TSFunctionType':
		case 'TSConstructorType':
		case 'TSMethodSignature':
		case 'TSCallSignatureDeclaration':
		case 'TSConstructSignatureDeclaration':
			return { kind: 'parameters', units: node.params.map((child) => createUnit(child)) };

		default:
			return undefined;
	}
}

/** Reads the units of the nodes that hold siblings, or undefined for any other node. */
function readMembers(node: Node, sourceCode: SourceCode): Members | undefined {
	switch (node.type) {
		case 'Program': {
			const shebang = createShebangUnit(node, sourceCode);
			return { kind: 'program', units: [...(shebang ? [shebang] : []), ...node.body.map((child) => createUnit(child))] };
		}

		case 'BlockStatement':
			return { kind: 'block', units: node.body.map((child) => createUnit(child)) };

		case 'StaticBlock':
			return { kind: 'static-block', units: node.body.map((child) => createUnit(child)) };

		case 'TSModuleBlock':
			return { kind: 'module-block', units: node.body.map((child) => createUnit(child)) };

		case 'ClassBody':
			return { kind: 'class-body', units: node.body.map((child) => createUnit(child)) };

		case 'TSInterfaceBody':
			return { kind: 'interface-body', units: node.body.map((child) => createUnit(child)) };

		case 'TSTypeLiteral':
			return { kind: 'type-literal', units: node.members.map((child) => createUnit(child)) };

		case 'SwitchStatement':
			return { kind: 'switch', units: node.cases.map((child) => createUnit(child)) };

		case 'SwitchCase':
			return { kind: 'switch-case', units: node.consequent.map((child) => createUnit(child)) };

		case 'JSXElement':
		case 'JSXFragment':
			return { kind: 'jsx', units: createJsxUnits(node.children, sourceCode) };

		default:
			return undefined;
	}
}

/** Reads the line ending the file uses at an index, so a fix keeps a CRLF file CRLF. */
function readLineEnding(text: string, lineStart: number): string {
	return text.slice(lineStart - 2, lineStart) === '\r\n' ? '\r\n' : '\n';
}

/** Reads the facts of the gap between two neighbors; comments that start on the previous unit's last line trail it. */
function createGap(sourceCode: SourceCode, container: Container, previous: Unit, next: Unit): Gap {
	const between = container.kind === 'jsx' ? [] : sourceCode.getCommentsBefore(next.node);
	const leadingComments: TSESTree.Comment[] = [];

	let leadLine = next.jsxLeadLine ?? next.firstLine;

	for (const comment of between.toReversed()) {
		if (comment.loc.start.line <= previous.lastLine || leadLine - comment.loc.end.line > 1) {
			break;
		}

		leadingComments.unshift(comment);
		leadLine = comment.loc.start.line;
	}

	let previousEnd = previous.lastLine;

	for (const comment of between) {
		if (comment.loc.start.line <= previous.lastLine) {
			previousEnd = Math.max(previousEnd, comment.loc.end.line);
		}
	}

	const insertAt = sourceCode.getIndexFromLoc({ line: leadLine, column: 0 });
	const isBlank = leadLine - previousEnd >= 2;
	const blankStart = isBlank ? sourceCode.getIndexFromLoc({ line: previousEnd + 1, column: 0 }) : insertAt;

	return {
		leadingComments,
		hasComment: between.length > 0,
		isBlank,
		isJoined: leadLine - previousEnd <= 0 || isGluedPair(container, previous, next),
		insertAt,
		blankRange: isBlank && /^\s*$/.test(sourceCode.text.slice(blankStart, insertAt)) ? [blankStart, insertAt] : undefined,
		eol: readLineEnding(sourceCode.text, insertAt),
	};
}

function* walkNodes(sourceCode: SourceCode): Generator<Node> {
	const pending: Node[] = [sourceCode.ast];

	for (let node = pending.pop(); node !== undefined; node = pending.pop()) {
		yield node;

		for (const key of sourceCode.visitorKeys[node.type] ?? []) {
			const child: unknown = (node as unknown as Record<string, unknown>)[key];

			for (const item of Array.isArray(child) ? child : [child]) {
				if (typeof item === 'object' && item !== null && typeof (item as Node).type === 'string') {
					pending.push(item as Node);
				}
			}
		}
	}
}

/** Reads every container of a file with at least two units, and the gaps between those units; the lists are read only when asked for. */
export function collectContainers(sourceCode: SourceCode, includeLists: boolean): Container[] {
	const containers: Container[] = [];

	for (const node of walkNodes(sourceCode)) {
		const members = readMembers(node, sourceCode) ?? (includeLists ? readElements(node) : undefined);

		if (members === undefined || members.units.length < 2) {
			continue;
		}

		const gaps: Gap[] = [];
		const container: Container = { kind: members.kind, node, units: members.units, gaps };

		members.units.forEach((next, index) => {
			const previous = members.units[index - 1];

			if (previous !== undefined) {
				gaps.push(createGap(sourceCode, container, previous, next));
			}
		});

		containers.push(container);
	}

	return containers;
}
