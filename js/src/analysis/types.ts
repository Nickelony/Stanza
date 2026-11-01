import type { TSESTree } from '@typescript-eslint/utils';

export type ContainerKind =
	| 'program'
	| 'block'
	| 'static-block'
	| 'module-block'
	| 'class-body'
	| 'interface-body'
	| 'type-literal'
	| 'switch'
	| 'switch-case'
	| 'jsx'
	| 'object'
	| 'array'
	| 'enum'
	| 'arguments'
	| 'parameters';

/** One sibling of a container: a statement, a member, a case, a JSX child or an element of a list. */
export interface Unit {
	/** The node of the unit; the shebang of a file has none, so it holds the `Program` node. */
	readonly node: TSESTree.Node;
	readonly firstLine: number;
	readonly lastLine: number;
	readonly isMultiLine: boolean;
	readonly isShebang: boolean;

	/** True for JSX text that holds content, which no blank line may split from its neighbors. */
	readonly isText: boolean;

	/** The first line of the `{/* note *\/}` comments that lead a JSX child, when there are any. */
	readonly jsxLeadLine: number | undefined;

	/** The text of each `{/* note *\/}` comment that leads a JSX child, from the first to the last. */
	readonly jsxLeadTexts: readonly string[];
}

/** The space between two neighboring units, read once so every rule sees the same facts. */
export interface Gap {
	/** The comments that stand directly above the next unit and travel with it. */
	readonly leadingComments: readonly TSESTree.Comment[];

	/** True when any comment, a trailing one included, stands between the two units. */
	readonly hasComment: boolean;

	/** True when a blank line stands between the previous unit and the next unit with its leading comments. */
	readonly isBlank: boolean;

	/** True when no blank line can stand here: the units share a line, or they must stay together. */
	readonly isJoined: boolean;

	/** The index of the start of the line above which a blank line goes. */
	readonly insertAt: number;

	/** The range of the blank lines between the previous unit and the next one, or undefined when no blank line stands there or text other than whitespace does. */
	readonly blankRange: readonly [number, number] | undefined;

	/** The line ending the file uses at the gap, so a fix keeps a CRLF file CRLF. */
	readonly eol: string;
}

export interface Container {
	readonly kind: ContainerKind;
	readonly node: TSESTree.Node;
	readonly units: readonly Unit[];

	/** The gap between `units[index]` and `units[index + 1]`. */
	readonly gaps: readonly Gap[];
}
