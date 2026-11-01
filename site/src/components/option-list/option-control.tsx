import { useState } from 'preact/hooks';

import { useAppState } from '../../hooks/use-app-state.ts';
import { useStore } from '../../hooks/use-page.ts';
import { checkOption, withOption } from '../../state/config.ts';
import { optionDefaultLabel } from '../../state/labels.ts';
import { optionId, wordsOf, type Option, type Snippet } from '../../state/model.ts';
import { FieldError, RowInput, RowSelect } from '../row-list/parts.ts';

import type { OptionControlProps } from './types.ts';

/** Names the default as the dropdowns do; a default too long for the box is left to the tooltip and the preview. */
function placeholder(option: Option<Snippet>): string {
	const accepted = wordsOf(option);
	const words = accepted.length > 0 ? `, or ${accepted.join(', ')}` : '';
	const value = option.defaultValue;

	return value !== null && value.length <= 12 ? `Default (${value})${words}` : 'Default';
}

/** The dropdown or the text box that sets an option, and the message that says why a typed value is refused. */
export function OptionControl({ option, changed, anchored }: OptionControlProps) {
	const store = useStore();
	const { config } = useAppState();
	const id = optionId(option);
	const value = config.options[id];

	// What the visitor typed while the box holds the focus; a typo is shown but never stored, and the blur drops it.
	const [draft, setDraft] = useState<string | null>(null);
	const checked = draft === null ? null : checkOption(option, draft);
	const reason = checked !== null && !checked.ok ? checked.reason : '';
	const errorId = `error-${id}`;

	const write = (next: string | null): void => {
		store.set({ config: withOption(store.get().config, id, next), selection: { kind: 'option', id } });
	};

	return (
		<>
			{option.kind === 'enum' ? (
				// The label stays short enough for the box, so the tooltip carries the whole default.
				<RowSelect
					title={`Default: ${option.defaultText}`}
					aria-label={`Value of ${id}`}
					aria-describedby={errorId}
					tabIndex={anchored ? 0 : -1}
					value={value ?? ''}
					$changed={changed}
					onChange={(event) => write(event.currentTarget.value === '' ? null : event.currentTarget.value)}
				>
					<option value="">{optionDefaultLabel(option)}</option>

					{option.choices.map((choice) => (
						<option key={choice} value={choice}>
							{choice}
						</option>
					))}
				</RowSelect>
			) : (
				// A number that also takes a word names the word in the placeholder; a suggestion list would draw a dropdown arrow on a text box.
				<RowInput
					type="text"
					placeholder={placeholder(option)}
					title={`Default: ${option.defaultText}`}
					aria-label={`Value of ${id}`}
					aria-describedby={errorId}
					aria-invalid={reason !== ''}
					autoComplete="off"
					spellcheck={false}
					inputMode={option.kind === 'number' && wordsOf(option).length === 0 ? 'numeric' : 'text'}
					tabIndex={anchored ? 0 : -1}
					value={draft ?? value ?? ''}
					$changed={changed}
					onInput={(event) => {
						const typed = event.currentTarget.value;
						const result = checkOption(option, typed);

						setDraft(typed);

						// A typo leaves the last valid value in the configuration, and the blur puts it back in the box.
						if (result.ok) {
							write(result.value);
						}
					}}
					onBlur={() => setDraft(null)}
				/>
			)}

			{/* The message stays in the page and only its text changes, which is what a polite live region announces. No text is no child, because an empty string would be written again on every render. */}
			<FieldError id={errorId} aria-live="polite">
				{reason || null}
			</FieldError>
		</>
	);
}
