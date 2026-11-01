import type { Option, Rule, Snippet } from '../../state/model.ts';

export interface VariantButtonsProps {
	labels: readonly string[];
	shown: number;

	onPick(index: number): void;
}

export interface ExampleProps {
	rule: Rule<Snippet>;
	number: number;
	variant: number;
	off: boolean;
}

export interface BodyProps {
	option: Option<Snippet>;

	/** The value in force: the one the form holds, else the default. */
	effective: string | null;

	/** Whether the form holds a value of its own. */
	explicit: boolean;
}
