import { repoUrl } from '../../address/links.ts';
import { useLanguage } from '../../hooks/use-page.ts';

import { GitHubMark } from './github-mark.tsx';
import { LanguageSwitch } from './language-switch.tsx';
import { logos } from './logos.ts';
import { MarksToggle } from './marks-toggle.tsx';
import { Actions, Bar, Controls, Logo, RepositoryLink, Title } from './parts.ts';
import { ThemeToggle } from './theme-toggle.tsx';

/** The heading names the tool alone, because the language switch beside it says which language the page is for. */
export function Header() {
	const language = useLanguage();

	return (
		<Bar>
			<Title>
				<Logo src={logos[language.id]} alt="" width={22} height={22} />
				Stanza configurator
			</Title>

			<LanguageSwitch />

			<Actions>
				<RepositoryLink href={repoUrl} target="_blank" rel="noopener" aria-label="Stanza on GitHub" title="Stanza on GitHub">
					<GitHubMark />
				</RepositoryLink>

				<Controls>
					<ThemeToggle />
					<MarksToggle />
				</Controls>
			</Actions>
		</Bar>
	);
}
