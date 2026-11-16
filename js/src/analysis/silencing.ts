import type { TSESLint, TSESTree } from '@typescript-eslint/utils';

type SourceCode = Readonly<TSESLint.SourceCode>;

/** A directive comment as `SourceCode#getDisableDirectives` gives it. */
interface DisableDirective {
	readonly type: 'disable' | 'enable' | 'disable-line' | 'disable-next-line';
	readonly value: string;
	readonly node: { readonly loc: TSESTree.SourceLocation; readonly range: TSESTree.Range };
}

/** A place where a directive turns rules off or back on, with ESLint's 1-based column; no rule ID means every rule. */
interface Switch {
	readonly line: number;
	readonly column: number;
	readonly ruleIds: readonly string[];
	readonly disables: boolean;
}

/** What the `eslint-disable` comments of a file say. */
export interface Silencing {
	/** Tells whether a directive silences a rule's report at a position. */
	readonly silences: (ruleId: string, position: TSESTree.Position) => boolean;

	/** Tells whether a comment is a directive. */
	readonly isDirective: (comment: TSESTree.Comment) => boolean;
}

function compareSwitches(first: Switch, second: Switch): number {
	return first.line - second.line || first.column - second.column;
}

/** Reads the switches of one directive the way ESLint places them: a line directive covers its line, or the line below the comment. */
function readSwitches(directive: DisableDirective): { block: Switch[]; line: Switch[] } {
	const ruleIds = directive.value
		.split(',')
		.map((ruleId) => ruleId.trim())
		.filter((ruleId) => ruleId !== '');

	const { start, end } = directive.node.loc;

	switch (directive.type) {
		case 'disable':
		case 'enable':
			return { block: [{ line: start.line, column: start.column + 1, ruleIds, disables: directive.type === 'disable' }], line: [] };

		case 'disable-line':
			return {
				block: [],

				line: [
					{ line: start.line, column: 1, ruleIds, disables: true },
					{ line: start.line + 1, column: 0, ruleIds, disables: false },
				],
			};

		default:
			return {
				block: [],

				line: [
					{ line: end.line + 1, column: 1, ruleIds, disables: true },
					{ line: end.line + 2, column: 0, ruleIds, disables: false },
				],
			};
	}
}

/** Tells whether the last switch at or before the position that names the rule, or every rule, turns it off. */
function isSilencedBy(switches: readonly Switch[], ruleId: string, position: TSESTree.Position): boolean {
	let silenced = false;

	for (const entry of switches) {
		if (entry.line > position.line || (entry.line === position.line && entry.column > position.column + 1)) {
			break;
		}

		if (entry.ruleIds.length === 0 || entry.ruleIds.includes(ruleId)) {
			silenced = entry.disables;
		}
	}

	return silenced;
}

/**
 * Reads the `eslint-disable` comments of a file, so a keeper can hand back a gap ESLint would drop its report for.
 * ESLint applies block and line directives apart, and a report is silenced when either silences it.
 */
export function readSilencing(sourceCode: SourceCode): Silencing {
	const source = sourceCode as SourceCode & { getDisableDirectives?: () => { readonly directives: readonly DisableDirective[] } };
	const directives = source.getDisableDirectives?.().directives ?? [];

	if (directives.length === 0) {
		return { silences: () => false, isDirective: () => false };
	}

	const switches = directives.map(readSwitches);
	const block = switches.flatMap((entry) => entry.block).toSorted(compareSwitches);
	const line = switches.flatMap((entry) => entry.line).toSorted(compareSwitches);
	const starts = new Set(directives.map((directive) => directive.node.range[0]));

	return {
		silences: (ruleId, position) => isSilencedBy(block, ruleId, position) || isSilencedBy(line, ruleId, position),
		isDirective: (comment) => starts.has(comment.range[0]),
	};
}
