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

/** What the disable and enable directives of a file say. */
export interface Silencing {
	/** Tells whether a directive silences a rule's report at a position. */
	readonly silences: (ruleId: string, position: TSESTree.Position) => boolean;

	/** Tells whether a comment is an ESLint disable or enable directive. */
	readonly isEslintDirective: (comment: TSESTree.Comment) => boolean;
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

/** Tells whether a switch stands after the position, which ESLint compares with a 1-based column. */
function isAfter(entry: Switch, position: TSESTree.Position): boolean {
	return entry.line > position.line || (entry.line === position.line && entry.column > position.column + 1);
}

/** Tells whether the last switch at or before the position turns the rule off, finding it by bisection in the switches that name the rule or every rule. */
function isSilencedBy(switches: readonly Switch[], position: TSESTree.Position): boolean {
	let low = 0;
	let high = switches.length;

	while (low < high) {
		const middle = (low + high) >>> 1;

		if (isAfter(switches[middle] as Switch, position)) {
			high = middle;
		} else {
			low = middle + 1;
		}
	}

	return low > 0 && (switches[low - 1] as Switch).disables;
}

/** Keeps the switches that name the rule or every rule, in their order. */
function filterRuleSwitches(switches: readonly Switch[], ruleId: string): readonly Switch[] {
	return switches.filter((entry) => entry.ruleIds.length === 0 || entry.ruleIds.includes(ruleId));
}

/**
 * Reads the disable and enable directives of a file, so a keeper can hand back a gap ESLint would drop its report for.
 * ESLint applies block and line directives apart, and a report is silenced when either silences it.
 */
export function readSilencing(sourceCode: SourceCode): Silencing {
	const source = sourceCode as SourceCode & { getDisableDirectives?: () => { readonly directives: readonly DisableDirective[] } };
	const directives = source.getDisableDirectives?.().directives ?? [];

	if (directives.length === 0) {
		return { silences: () => false, isEslintDirective: () => false };
	}

	const switches = directives.map(readSwitches);
	const block = switches.flatMap((entry) => entry.block).toSorted(compareSwitches);
	const line = switches.flatMap((entry) => entry.line).toSorted(compareSwitches);
	const starts = new Set(directives.map((directive) => directive.node.range[0]));
	const byRule = new Map<string, { block: readonly Switch[]; line: readonly Switch[] }>();

	const readRuleSwitches = (ruleId: string): { block: readonly Switch[]; line: readonly Switch[] } => {
		let entry = byRule.get(ruleId);

		if (entry === undefined) {
			entry = { block: filterRuleSwitches(block, ruleId), line: filterRuleSwitches(line, ruleId) };
			byRule.set(ruleId, entry);
		}

		return entry;
	};

	return {
		silences: (ruleId, position) => {
			const entry = readRuleSwitches(ruleId);
			return isSilencedBy(entry.block, position) || isSilencedBy(entry.line, position);
		},

		isEslintDirective: (comment) => starts.has(comment.range[0]),
	};
}
