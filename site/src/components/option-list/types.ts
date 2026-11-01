import type { RowList } from '../../hooks/use-row-list.ts';
import type { Option, Snippet } from '../../state/model.ts';

export interface OptionRowProps {
	option: Option<Snippet>;
	changed: boolean;
	selected: boolean;
	list: RowList;
}

export interface OptionControlProps {
	option: Option<Snippet>;
	changed: boolean;
	anchored: boolean;
}
