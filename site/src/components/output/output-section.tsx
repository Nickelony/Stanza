import { lazy, Suspense } from 'preact/compat';
import { useMemo, useState } from 'preact/hooks';

import { useAppState } from '../../hooks/use-app-state.ts';
import { useCatalog } from '../../hooks/use-page.ts';
import { saveFile } from '../../util/clipboard.ts';
import type { ImportedFile } from '../editorconfig-tools/types.ts';
import { Button } from '../primitives.ts';

import { ColoredText } from './colored-text.tsx';
import { EditorConfigChoices } from './editorconfig-choices.tsx';
import { Actions, Bar, Code, CopyStatus, Note, Title } from './parts.ts';
import type { BlockProps } from './types.ts';

import { useCopy } from './hooks/use-copy.ts';

type Tools = typeof import('../editorconfig-tools/editorconfig-tools.ts');

/** The component of the tools module, loaded on first use; every one comes from the one module, so the build writes one chunk. */
const lazyTool = <Name extends keyof Tools>(name: Name) =>
	lazy<Tools[Name]>(() => import('../editorconfig-tools/editorconfig-tools.ts').then((module) => ({ default: module[name] })));

const ImportButton = lazyTool('ImportButton');
const ImportReport = lazyTool('ImportReport');
const Conflicts = lazyTool('Conflicts');

/** One block of output: its bar with the title and the buttons, and the generated text under it. */
export function OutputSection({ block, index }: BlockProps) {
	const catalog = useCatalog();
	const { config } = useAppState();
	const [imported, setImported] = useState<ImportedFile | null>(null);

	// A rebuilt block loses the visitor's text selection, so the text is written again only when the configuration changes it.
	const text = useMemo(() => block.write(catalog, config), [block, catalog, config]);
	const writesNothing = text === '' && block.empty !== null;
	const copy = useCopy(text);
	const { file } = block;

	return (
		<>
			<Bar $separated={index > 0}>
				<Title>{block.title}</Title>
				{block.note !== null && <Note>{block.note}</Note>}
				{block.tools && <EditorConfigChoices />}

				<Actions>
					<CopyStatus role="status">{copy.message || null}</CopyStatus>

					{block.tools && (
						<Suspense
							fallback={
								<Button type="button" disabled>
									Import
								</Button>
							}
						>
							<ImportButton onImported={setImported} />
						</Suspense>
					)}

					<Button type="button" disabled={writesNothing} onClick={copy.copy}>
						Copy
					</Button>

					{file !== null && (
						<Button type="button" title={file.hint ?? undefined} onClick={() => saveFile(file.name, text)}>
							Download
						</Button>
					)}
				</Actions>
			</Bar>

			{block.tools && (
				<Suspense fallback={null}>
					<ImportReport imported={imported} onClose={() => setImported(null)} />
					<Conflicts />
				</Suspense>
			)}

			<Code tabIndex={0} role="region" aria-label={block.label} $compact={block.compact}>
				<code>{writesNothing ? block.empty : <ColoredText syntax={block.syntax} text={text} />}</code>
			</Code>
		</>
	);
}
