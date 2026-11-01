import { createHighlighter, type BundledLanguage } from 'shiki';

import type { LanguageId } from '../../src/languages/language.ts';
import type { Option, Rule, SiteCatalog, Snippet, SourceCatalog, SourceSnippet } from '../../src/state/model.ts';

import { escapeHtml } from './markdown.ts';

const themes = { light: 'github-light', dark: 'github-dark' } as const;

/** The grammars each page loads: the fences its rule and option pages hold. */
const grammars: Record<LanguageId, readonly string[]> = {
	csharp: ['csharp', 'xml', 'ini'],
	markup: ['xml', 'ini'],
	js: ['ts', 'js'],
	python: ['python'],
};

/**
 * The GitHub theme colors that fall under 4.5:1 on the code background of their theme or on the tint of an added or removed line,
 * each with the Primer color that takes its place.
 */
const readable = {
	light: { '#d73a49': '#a40e26', '#22863a': '#116329', '#6a737d': '#57606a' },
	dark: { '#6a737d': '#9198a1' },
} as const satisfies Record<keyof typeof themes, Record<string, string>>;

/** The color the page draws a token in, which is the theme's own unless it is one that `readable` replaces. */
const readableColor = (theme: keyof typeof readable, color: string): string =>
	(readable[theme] as Record<string, string>)[color.toLowerCase()] ?? color;

interface Highlighted {
	catalog: SiteCatalog;

	/** The rules that give a token class its light and dark color. */
	css: string;
}

/** Highlights every snippet of the catalog with Shiki, so the browser needs no highlighter. */
export async function highlightCatalog(source: SourceCatalog, id: LanguageId = 'csharp'): Promise<Highlighted> {
	const languages = grammars[id];
	const highlighter = await createHighlighter({ themes: [themes.light, themes.dark], langs: [...languages] });
	const foreground = [highlighter.getTheme(themes.light).fg.toLowerCase(), highlighter.getTheme(themes.dark).fg.toLowerCase()];
	const palette = new Map<string, number>();

	const classFor = (light: string, dark: string): string | null => {
		if (light.toLowerCase() === foreground[0] && dark.toLowerCase() === foreground[1]) {
			return null;
		}

		const key = `${light}|${dark}`;

		if (!palette.has(key)) {
			palette.set(key, palette.size);
		}

		return `t${palette.get(key)}`;
	};

	const snippet = (value: SourceSnippet): Snippet => {
		const language = (languages.includes(value.lang) ? value.lang : 'text') as BundledLanguage;
		const { tokens } = highlighter.codeToTokens(value.code, { lang: language, themes, defaultColor: false });

		return {
			lines: tokens.map((line) => ({
				html: line
					.map((token) => {
						const style = token.htmlStyle as Record<string, string> | undefined;
						const name = classFor(style?.['--shiki-light'] ?? foreground[0]!, style?.['--shiki-dark'] ?? foreground[1]!);

						return name ? `<span class="${name}">${escapeHtml(token.content)}</span>` : escapeHtml(token.content);
					})
					.join(''),
			})),
		};
	};

	const rules: Rule<Snippet>[] = source.rules.map((rule) => ({
		...rule,

		examples: rule.examples.map((example) => ({
			beforeLabel: example.beforeLabel,
			before: snippet(example.before),
			afters: example.afters.map((after) => ({ label: after.label, snippet: snippet(after.snippet) })),
			unchanged: example.unchanged,
		})),
	}));

	const options: Option<Snippet>[] = source.options.map((option) => ({
		...option,

		examples: option.examples.map((example) => ({
			segments: example.segments.map((segment) => ({ ...segment, snippet: snippet(segment.snippet) })),
		})),
	}));

	highlighter.dispose();

	const light: string[] = [];
	const dark: string[] = [];

	for (const [key, index] of palette) {
		const [lightColor, darkColor] = key.split('|');

		light.push(`.t${index}{color:${readableColor('light', lightColor!)}}`);
		dark.push(`.t${index}{color:${readableColor('dark', darkColor!)}}`);
	}

	const css = [
		light.join('\n'),
		`@media (prefers-color-scheme: dark){\n${dark.map((rule) => `:root:not([data-theme="light"]) ${rule}`).join('\n')}\n}`,
		dark.map((rule) => `:root[data-theme="dark"] ${rule}`).join('\n'),
	].join('\n');

	return { catalog: { ...source, rules, options }, css };
}
