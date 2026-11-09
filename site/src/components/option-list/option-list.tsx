import { Fragment } from 'preact';
import { useRef } from 'preact/hooks';

import { useAppState } from '../../hooks/use-app-state.ts';
import { useCatalog } from '../../hooks/use-page.ts';
import { useRowList } from '../../hooks/use-row-list.ts';
import { isOptionChanged } from '../../state/config.ts';
import { audienceOf, optionId } from '../../state/model.ts';
import { isSelected } from '../../state/store.ts';
import { ScreenReaderOnly } from '../primitives.ts';
import { Empty, Flag, OptionName, Panel, RowId, Rows, ScopeFlag } from '../row-list/parts.ts';
import { ListRow, RowButton } from '../row-list/row-list.tsx';
import { panelId, tabId } from '../tabs/ids.ts';

import { OptionControl } from './option-control.tsx';
import type { OptionRowProps } from './types.ts';

const audienceNotes = { analyzer: '', both: '', markup: 'markup only' } as const;

/** A long key wraps after an underscore rather than running under the control beside it. */
function breakableKey(key: string) {
	return key.split('_').map((part, index) =>
		index === 0 ? (
			part
		) : (
			<Fragment key={index}>
				<wbr />_{part}
			</Fragment>
		),
	);
}

/** The panel of the options. */
export function OptionList() {
	const catalog = useCatalog();
	const state = useAppState();
	const panel = useRef<HTMLDivElement>(null);
	const rows = catalog.options.map((option) => ({ option, changed: isOptionChanged(option, state.config) }));

	const list = useRowList(
		'option',
		rows.map(({ option, changed }) => ({ id: optionId(option), haystack: optionId(option), changed })),
		state,
		panel,
	);

	return (
		<Panel ref={panel} role="tabpanel" id={panelId('option')} aria-labelledby={tabId('option')} hidden={state.selection?.kind !== 'option'}>
			{list.inView.length === 0 && <Empty>No option matches the filter.</Empty>}

			<Rows>
				{rows.map(({ option, changed }) => (
					<OptionRow
						key={optionId(option)}
						option={option}
						changed={changed}
						selected={isSelected(state, { kind: 'option', id: optionId(option) })}
						list={list}
					/>
				))}
			</Rows>
		</Panel>
	);
}

function OptionRow({ option, changed, selected, list }: OptionRowProps) {
	const id = optionId(option);
	const selection = { kind: 'option', id } as const;
	const note = audienceNotes[audienceOf(option)];
	const anchored = list.anchor === id;

	return (
		<ListRow selection={selection} selected={selected} changed={changed} shown={list.isShown(id)}>
			<RowButton selection={selection} title={`Default: ${option.defaultText}`} selected={selected} anchored={anchored} list={list}>
				<OptionName>
					<RowId $key>{breakableKey(option.key)}</RowId>
					<ScreenReaderOnly>{changed ? 'changed' : ''}</ScreenReaderOnly>
					{option.scope !== undefined && <ScopeFlag>{option.scope}</ScopeFlag>}
				</OptionName>

				{note && <Flag>{note}</Flag>}
			</RowButton>

			<OptionControl option={option} changed={changed} anchored={anchored} />
		</ListRow>
	);
}
