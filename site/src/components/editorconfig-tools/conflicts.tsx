import { findConflicts } from '../../editorconfig/conflicts.ts';
import { useAppState } from '../../hooks/use-app-state.ts';
import { useCatalog } from '../../hooks/use-page.ts';

import { WarningList, Warnings, WarningsTitle } from './parts.ts';

/** Turns each rule ID in a message into a link that selects the rule, so a click reaches its page. */
function linked(message: string, pages: ReadonlyMap<string, string>) {
	return message.split(/(ZA\d{4})/).map((part, index) => {
		const url = pages.get(part);

		return url ? (
			<a key={index} href={url} target="_blank" rel="noopener" data-select={part}>
				{part}
			</a>
		) : (
			part
		);
	});
}

/** The list of rules that fight each other or the formatter, which is absent while there are none. */
export function Conflicts() {
	const catalog = useCatalog();
	const { config } = useAppState();
	const conflicts = findConflicts(catalog, config);
	const pages = new Map(catalog.rules.map((rule) => [rule.id, rule.pageUrl]));

	if (conflicts.length === 0) {
		return null;
	}

	return (
		<Warnings>
			<WarningsTitle>Conflicts</WarningsTitle>

			<WarningList>
				{conflicts.map((conflict) => (
					<li key={conflict.message}>{linked(conflict.message, pages)}</li>
				))}
			</WarningList>
		</Warnings>
	);
}
