import { useStoredToggle } from '../../hooks/use-stored-toggle.ts';

import { Check, MarksButton } from './parts.ts';

/** The header button that shows the preview code with or without the change marks; marked unless the visitor chose otherwise. */
export function MarksToggle() {
	const [marks, choose] = useStoredToggle<'on' | 'off'>({
		key: 'stanza-marks',
		attribute: 'marks',
		values: ['on', 'off'],
		initial: (stored) => stored ?? 'on',
	});

	const marked = marks === 'on';

	return (
		<MarksButton type="button" aria-pressed={marked} title="Off, the preview shows the code alone, blank lines as they are" onClick={choose}>
			{marked && (
				<Check viewBox="0 0 12 12" aria-hidden="true">
					<path d="M1.5 6.5 4.5 9.5 10.5 2.5" />
				</Check>
			)}
			Diff marks
		</MarksButton>
	);
}
