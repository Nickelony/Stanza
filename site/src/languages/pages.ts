import type { LanguageId } from './language.ts';

/** A page of the site: the language it shows, its name in the language switch, and its directory under the site base. */
export interface PageEntry {
	id: LanguageId;
	name: string;

	/** Empty for the home page, which is the C# one. */
	path: string;
}

/** Every page of the site, in the order the language switch lists them. The descriptor of a language is loaded only by the page that shows it. */
export const pages: readonly PageEntry[] = [
	{ id: 'csharp', name: 'C#', path: '' },
	{ id: 'markup', name: 'Markup', path: 'markup/' },
	{ id: 'js', name: 'JS/TS', path: 'js/' },
	{ id: 'python', name: 'Python', path: 'py/' },
];
