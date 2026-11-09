import { useAppState } from '../../hooks/use-app-state.ts';
import { useLanguage } from '../../hooks/use-page.ts';
import { hasLevels } from '../../languages/language.ts';
import { pages } from '../../languages/pages.ts';
import { defaultLevel } from '../../state/levels.ts';

import { LanguageLinks } from './parts.ts';

/** The links between the pages of the languages; each carries the level picked here, so the next page opens on it. A page with no levels carries none. */
export function LanguageSwitch() {
	const language = useLanguage();
	const { config } = useAppState();
	const hash = !hasLevels(language) || config.level === defaultLevel ? '' : `#level=${config.level}`;

	return (
		<LanguageLinks aria-label="Language">
			{pages.map((page) => (
				<a key={page.id} href={`${import.meta.env.BASE_URL}${page.path}${hash}`} aria-current={page.id === language.id ? 'page' : undefined}>
					{page.name}
				</a>
			))}
		</LanguageLinks>
	);
}
