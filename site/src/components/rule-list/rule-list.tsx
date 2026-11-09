import { useRef } from 'preact/hooks';

import { useAppState } from '../../hooks/use-app-state.ts';
import { usePage } from '../../hooks/use-page.ts';
import { useRowList } from '../../hooks/use-row-list.ts';
import { hasLevels } from '../../languages/language.ts';
import {
	effectiveState,
	hiddenByCSharpier,
	isReporting,
	isRuleChanged,
	levelState,
	replacedBy,
	settingOf,
	withRule,
	type EffectiveState,
} from '../../state/config.ts';
import { ruleDefaultLabel } from '../../state/labels.ts';
import type { RuleSetting } from '../../state/model.ts';
import { isSelected } from '../../state/store.ts';
import { ScreenReaderOnly } from '../primitives.ts';
import { Dot, Empty, Flag, GroupTitle, Panel, RowId, RowName, Rows, RowSelect, RowTitle } from '../row-list/parts.ts';
import { ListRow, RowButton } from '../row-list/row-list.tsx';
import { panelId, tabId } from '../tabs/ids.ts';

/** The panel of the rules, listed under the group each belongs to. */
export function RuleList() {
	const { catalog, language, store } = usePage();
	const state = useAppState();
	const panel = useRef<HTMLDivElement>(null);

	// A language that offers one way to enable a rule has no severity to color, so its enabled rules show alike.
	const coloredBySeverity = language.ruleSettings.filter((setting) => setting !== 'none').length > 1;
	const dotState = (effective: EffectiveState) => (!coloredBySeverity && isReporting(effective) ? 'enabled' : effective);
	const offTitle = hasLevels(language) ? 'Off at this level' : 'Off until a line enables it';

	const rows = catalog.rules.map((rule) => {
		const changed = isRuleChanged(rule, state.config);
		const haystack = `${rule.id} ${rule.title}`.toLowerCase();

		return { rule, changed, facts: { id: rule.id, haystack, changed, allowed: !hiddenByCSharpier(rule, state.config) } };
	});

	const list = useRowList(
		'rule',
		rows.map((row) => row.facts),
		state,
		panel,
	);

	const setSeverity = (id: string, value: string): void => {
		store.set({ config: withRule(store.get().config, id, value as RuleSetting), selection: { kind: 'rule', id } });
	};

	return (
		<Panel ref={panel} role="tabpanel" id={panelId('rule')} aria-labelledby={tabId('rule')} hidden={state.selection?.kind === 'option'}>
			{list.inView.length === 0 && <Empty>No rule matches the filter.</Empty>}

			{catalog.groups.map((group) => {
				const inGroup = rows.filter(({ rule }) => rule.group === group);

				return (
					<section key={group} hidden={!inGroup.some(({ rule }) => list.isShown(rule.id))}>
						<GroupTitle>{group}</GroupTitle>

						<Rows>
							{inGroup.map(({ rule, changed }) => {
								const selection = { kind: 'rule', id: rule.id } as const;
								const selected = isSelected(state, selection);
								const effective = effectiveState(rule, state.config);
								const label = ruleDefaultLabel(rule, state.config, language);
								const opposite = replacedBy(rule, state.config);
								const anchored = list.anchor === rule.id;

								return (
									<ListRow key={rule.id} selection={selection} selected={selected} changed={changed} shown={list.isShown(rule.id)}>
										<RowButton selection={selection} title={rule.summary} selected={selected} anchored={anchored} list={list}>
											<Dot aria-hidden="true" title={language.stateLabels[effective]} $state={dotState(effective)} />

											<RowName>
												<RowId>{rule.id}</RowId>
												<RowTitle>{rule.title}</RowTitle>
											</RowName>

											<ScreenReaderOnly>{changed ? 'changed' : ''}</ScreenReaderOnly>

											{levelState(rule, state.config) === 'off' && (
												<Flag title={opposite === null ? offTitle : `Off: ${opposite} is enabled`}>off</Flag>
											)}
										</RowButton>

										<RowSelect
											aria-label={`Severity of ${rule.id}`}
											title={label}
											tabIndex={anchored ? 0 : -1}
											value={settingOf(state.config, rule)}
											$changed={changed}
											onChange={(event) => setSeverity(rule.id, event.currentTarget.value)}
										>
											<option value="default">{label}</option>

											{language.ruleSettings.map((setting) => (
												<option key={setting} value={setting}>
													{language.severityLabels[setting]}
												</option>
											))}
										</RowSelect>
									</ListRow>
								);
							})}
						</Rows>
					</section>
				);
			})}
		</Panel>
	);
}
