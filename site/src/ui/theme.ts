type Theme = 'light' | 'dark';

const storageKey = 'stanza-theme';

function stored(): Theme | null {
	try {
		const value = window.localStorage.getItem(storageKey);
		return value === 'light' || value === 'dark' ? value : null;
	} catch {
		return null;
	}
}

function currentTheme(): Theme {
	return stored() ?? (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
}

function applyTheme(theme: Theme): void {
	document.documentElement.dataset.theme = theme;
}

/** Remembers the choice where storage allows, and applies it either way. */
function chooseTheme(theme: Theme): void {
	try {
		window.localStorage.setItem(storageKey, theme);
	} catch {
		// Storage can be blocked, and the choice then lasts for the visit only.
	}

	applyTheme(theme);
}

/** Builds the toggle in the header; its label names the theme a click switches to. */
export function createThemeToggle(): HTMLButtonElement {
	const button = document.createElement('button');

	button.type = 'button';
	button.className = 'btn theme-toggle';

	const sync = (): void => {
		const theme = (document.documentElement.dataset.theme as Theme | undefined) ?? currentTheme();
		button.textContent = theme === 'dark' ? 'Use light theme' : 'Use dark theme';
	};

	button.addEventListener('click', () => {
		chooseTheme((document.documentElement.dataset.theme as Theme | undefined) === 'dark' ? 'light' : 'dark');
		sync();
	});

	applyTheme(currentTheme());
	sync();

	return button;
}
