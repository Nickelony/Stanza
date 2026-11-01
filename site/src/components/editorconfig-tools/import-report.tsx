import { listedNotes, summarize } from '../../editorconfig/import.ts';
import { Button, Mono } from '../primitives.ts';

import { LiveStatus, Notes, Reason, Report, Summary } from './parts.ts';
import type { ImportedFile } from './types.ts';

/**
 * What an import applied and skipped, hidden until one runs.
 * The live region stays in the page and only its text changes, because one that appears with its first message is often not announced.
 */
export function ImportReport({ imported, onClose }: { imported: ImportedFile | null; onClose(): void }) {
	const summary = imported === null ? '' : summarize(imported.result, imported.name);
	const { shown, more } = listedNotes(imported?.result.skipped ?? []);

	return (
		<div>
			<LiveStatus role="status">{summary || null}</LiveStatus>

			{imported !== null && (
				<Report aria-label="Import report">
					<Summary aria-hidden="true">{summary}</Summary>

					<Notes>
						{shown.map((note, index) => (
							<li key={index}>
								<Mono>{`Line ${note.line}`}</Mono> <code>{note.text}</code> <Reason>{note.reason}</Reason>
							</li>
						))}

						{more > 0 && <li>{`and ${more} more`}</li>}
					</Notes>

					<Button type="button" title="Close this report" onClick={onClose}>
						Close
					</Button>
				</Report>
			)}
		</div>
	);
}
