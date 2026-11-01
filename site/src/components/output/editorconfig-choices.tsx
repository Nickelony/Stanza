import { useAppState } from '../../hooks/use-app-state.ts';
import { usePage } from '../../hooks/use-page.ts';
import { setConfig } from '../../state/store.ts';
import { CheckLabel } from '../primitives.ts';

/** The checkboxes that decide what the `.editorconfig` holds. */
export function EditorConfigChoices() {
	const { catalog, language, store } = usePage();
	const { config } = useAppState();

	const extensions = catalog.markupExtensions
		.slice(0, 3)
		.map((extension) => `.${extension}`)
		.join(', ');

	return (
		<>
			<CheckLabel htmlFor="opt-every-setting" title={language.everySettingHint}>
				<input
					type="checkbox"
					id="opt-every-setting"
					checked={config.everySetting}
					onChange={(event) => setConfig(store, { everySetting: event.currentTarget.checked })}
				/>
				Every setting
			</CheckLabel>

			<CheckLabel htmlFor="opt-markup" title={`A section for ${extensions} and the other files stanza-markup lays out`}>
				<input
					type="checkbox"
					id="opt-markup"
					checked={config.markupSection}
					onChange={(event) => setConfig(store, { markupSection: event.currentTarget.checked })}
				/>
				Markup section
			</CheckLabel>
		</>
	);
}
