import { useRef } from 'preact/hooks';

import { Face, Group, Hint, Name, Radio, Segment } from './parts.ts';
import type { PickerGroupProps } from './types.ts';

/** One group of joined buttons, which are radio buttons the page styles, whose choice is a property of the package. */
export function PickerGroup<V extends string>(props: PickerGroupProps<V>) {
	const { name, title, values, labels, value, hint, disabled = false, tight = false, onPick } = props;
	const group = useRef<HTMLDivElement>(null);

	const pick = (choice: V): void => {
		if (onPick(choice) !== false) {
			return;
		}

		// An arrow key moved the focus with the check, so both go back to the radio that stays checked, and the next arrow starts there.
		const kept = [...(group.current?.querySelectorAll('input') ?? [])].find((input) => input.value === value);

		if (kept) {
			kept.checked = true;
			kept.focus();
		}
	};

	return (
		<div>
			<Name id={`picker-${name}-name`}>{title}</Name>

			<Group ref={group} role="radiogroup" aria-labelledby={`picker-${name}-name`} aria-describedby={`picker-${name}-hint`}>
				{values.map((choice) => (
					<Segment key={choice}>
						<Radio
							type="radio"
							name={`picker-${name}`}
							value={choice}
							checked={choice === value}
							disabled={disabled}
							onChange={(event) => event.currentTarget.checked && pick(choice)}
						/>

						<Face $tight={tight}>{labels[choice]}</Face>
					</Segment>
				))}
			</Group>

			<Hint id={`picker-${name}-hint`}>{hint}</Hint>
		</div>
	);
}
