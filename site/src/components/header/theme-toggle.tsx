import { useStoredToggle } from '../../hooks/use-stored-toggle.ts';
import { Button } from '../primitives.ts';

type Theme = 'light' | 'dark';

const prefersDark = (): boolean => window.matchMedia('(prefers-color-scheme: dark)').matches;

/** The toggle in the header; its label names the theme a click switches to. */
export function ThemeToggle() {
	const [theme, choose] = useStoredToggle<Theme>({
		key: 'stanza-theme',
		attribute: 'theme',
		values: ['light', 'dark'],
		initial: (stored) => stored ?? (prefersDark() ? 'dark' : 'light'),
	});

	const next = theme === 'dark' ? 'light' : 'dark';

	// The label is short, and the accessible name says that a click switches to that theme.
	return (
		<Button type="button" aria-label={`Switch to the ${next} theme`} onClick={choose}>
			{next === 'light' ? 'Light theme' : 'Dark theme'}
		</Button>
	);
}
