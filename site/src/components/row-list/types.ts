import type { ComponentChildren } from 'preact';

import type { RowList } from '../../hooks/use-row-list.ts';
import type { Selection } from '../../state/selection.ts';

export interface ListRowProps {
	selection: Selection;
	selected: boolean;
	changed: boolean;
	shown: boolean;
	children: ComponentChildren;
}

export interface RowButtonProps {
	selection: Selection;
	title: string;
	selected: boolean;
	anchored: boolean;
	list: RowList;
	children: ComponentChildren;
}

export interface ControlProps {
	$changed: boolean;
}
