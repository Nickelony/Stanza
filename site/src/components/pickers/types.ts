export interface PickerGroupProps<V extends string> {
	/** Names the group in the page: the ids of its heading and hint, and the name of its radio buttons. */
	name: string;
	title: string;
	values: readonly V[];
	labels: Record<V, string>;
	value: V;
	hint: string;
	disabled?: boolean;

	/** Whether the segments take the smaller padding of a narrow pane, so the group with the most of them fits. */
	tight?: boolean;

	/** Applies the pick, and returns `false` to refuse it, which puts the check and the focus back on the value that stays. */
	onPick(value: V): boolean | void;
}
