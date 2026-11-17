import { encodeHash } from '../../address/hash.ts';
import { useAppState } from '../../hooks/use-app-state.ts';
import { usePage } from '../../hooks/use-page.ts';
import { hasLevels } from '../../languages/language.ts';
import { pages } from '../../languages/pages.ts';
import { defaultLevel } from '../../state/levels.ts';

import { LanguageLinks } from './parts.ts';

/**
 * The links between the pages of the languages; each carries the level picked here, so the next page opens on it. A page with no levels carries none.
 * The link to the page the visitor is on carries the whole address, so a click on it changes nothing.
 */
export function LanguageSwitch() {
	const { catalog, language } = usePage();
	const state = useAppState();
	const levelHash = !hasLevels(language) || state.config.level === defaultLevel ? '' : `#level=${state.config.level}`;
	const ownHash = encodeHash(catalog, state, language);
	const hashOf = (page: { id: string }): string => (page.id !== language.id ? levelHash : ownHash === '' ? '' : `#${ownHash}`);

	return (
		<LanguageLinks aria-label="Language">
			{pages.map((page) => (
				<a key={page.id} href={`${import.meta.env.BASE_URL}${page.path}${hashOf(page)}`} aria-current={page.id === language.id ? 'page' : undefined}>
					{page.name}
				</a>
			))}
		</LanguageLinks>
	);
}
