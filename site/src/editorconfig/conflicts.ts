import { effectiveState, isEnabledByFile, isReporting, type Config } from '../state/config.ts';
import type { Catalog } from '../state/model.ts';

export interface Conflict {
	/** `pair` is two halves of a pair that a file enables together, and `formatter` is a rule CSharpier undoes. */
	kind: 'pair' | 'formatter';

	/** The rules the warning is about, in the order of the pair table or the catalog. */
	ids: string[];

	/** For a pair, the half that reports the gap or construct. */
	owner?: string;

	/** The warning as plain text; the IDs in it can be turned into links. */
	message: string;
}

/** Lists the rules of the configuration that fight each other, and, with the csharpier formatter, those that fight it. */
export function findConflicts<S>(catalog: Catalog<S>, config: Config): Conflict[] {
	const conflicts: Conflict[] = [];
	const byId = new Map(catalog.rules.map((rule) => [rule.id, rule]));

	for (const pair of catalog.pairs) {
		const first = byId.get(pair.first);
		const second = byId.get(pair.second);

		if (!first || !second || !isEnabledByFile(first, config) || !isEnabledByFile(second, config)) {
			continue;
		}

		const owner = pair.ownerWhenBothEnabled;
		const other = owner === pair.first ? pair.second : pair.first;

		conflicts.push({
			kind: 'pair',
			ids: [pair.first, pair.second],
			owner,
			message: `${pair.first} and ${pair.second} judge the same gap or construct and both are enabled, so ${owner} reports it and ${other} reports nothing. Enable one of them.`,
		});
	}

	if (config.formatter === 'csharpier') {
		for (const rule of catalog.rules) {
			if (rule.csharp?.undoneByCSharpier && isReporting(effectiveState(rule, config))) {
				conflicts.push({
					kind: 'formatter',
					ids: [rule.id],
					message: `${rule.id} is enabled, but CSharpier undoes the layout it writes, so the two undo each other. Leave the rule off.`,
				});
			}
		}
	}

	return conflicts;
}
