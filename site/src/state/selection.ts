import { isOptionChanged, isRuleChanged, type Config } from './config.ts';
import { optionId, type Catalog } from './model.ts';

export type Selection = { kind: 'rule' | 'option'; id: string };

/** Names the rule or the option with this ID or key, or nothing when the catalog has neither. */
export function selectionOf<S>(catalog: Catalog<S>, id: string): Selection | null {
	if (catalog.rules.some((rule) => rule.id === id)) {
		return { kind: 'rule', id };
	}

	return catalog.options.some((option) => optionId(option) === id) ? { kind: 'option', id } : null;
}

/** Names the first rule or option of the catalog, which a tab shows before the visitor picks one. */
export function firstSelection<S>(catalog: Catalog<S>, kind: Selection['kind']): Selection | null {
	const first = kind === 'rule' ? catalog.rules[0]?.id : catalog.options[0] === undefined ? undefined : optionId(catalog.options[0]);
	return first === undefined ? null : { kind, id: first };
}

/** Whether the selection names a rule or an option the configuration changes, which is what Changed only leaves in the list. */
export function isSelectionChanged<S>(catalog: Catalog<S>, config: Config, selection: Selection): boolean {
	if (selection.kind === 'rule') {
		const rule = catalog.rules.find((candidate) => candidate.id === selection.id);
		return rule !== undefined && isRuleChanged(rule, config);
	}

	const option = catalog.options.find((candidate) => optionId(candidate) === selection.id);
	return option !== undefined && isOptionChanged(option, config);
}

/** Names the first rule or option of the kind that the configuration changes, or nothing when it changes none. */
export function firstChangedSelection<S>(catalog: Catalog<S>, config: Config, kind: Selection['kind']): Selection | null {
	const ids = kind === 'rule' ? catalog.rules.map((rule) => rule.id) : catalog.options.map(optionId);
	const id = ids.find((candidate) => isSelectionChanged(catalog, config, { kind, id: candidate }));

	return id === undefined ? null : { kind, id };
}
