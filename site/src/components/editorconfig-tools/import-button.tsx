import { useRef } from 'preact/hooks';

import { importEditorConfig, refusal, sizeRefusal } from '../../editorconfig/import.ts';
import { usePage } from '../../hooks/use-page.ts';
import { changedCount } from '../../state/config.ts';
import { counted } from '../../util/counted.ts';
import { Button } from '../primitives.ts';

import type { ImportedFile } from './types.ts';

/** The button that opens the file picker, and applies the file it picks to the form after the visitor confirms. */
export function ImportButton({ onImported }: { onImported(imported: ImportedFile): void }) {
	const { catalog, store } = usePage();
	const input = useRef<HTMLInputElement>(null);

	const apply = async (file: File): Promise<void> => {
		const config = store.get().config;
		const tooLarge = sizeRefusal(file.size);

		if (tooLarge !== null) {
			onImported({ name: file.name, result: refusal(config, tooLarge) });
			return;
		}

		// A file of bytes that are no text still reads as a string; the import refuses it by what it holds.
		const result = importEditorConfig(catalog, await file.text(), config);

		if (result.refused !== null) {
			onImported({ name: file.name, result });
			return;
		}

		const total = changedCount(catalog, config);

		if (total > 0 && !window.confirm(`Replace the ${counted(total, 'changed setting')} with the ones in ${file.name}?`)) {
			return;
		}

		store.set({ config: result.config });
		onImported({ name: file.name, result });
	};

	const take = (): void => {
		const file = input.current?.files?.[0];

		// The same file can be chosen again after it is edited, so the choice is cleared once it is taken.
		if (input.current) {
			input.current.value = '';
		}

		if (file) {
			apply(file).catch(() => onImported({ name: file.name, result: refusal(store.get().config, 'The file could not be read.') }));
		}
	};

	return (
		<>
			<Button type="button" title="Fill in the form from an existing .editorconfig" onClick={() => input.current?.click()}>
				Import
			</Button>

			<input ref={input} type="file" hidden onChange={take} />
		</>
	);
}
