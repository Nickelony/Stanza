import { createHighlighter } from 'shiki';
import type { Option, Rule, SiteCatalog, Snippet, SourceCatalog, SourceSnippet } from '../src/model.ts';
import { escapeHtml } from './markdown.ts';

const themes = { light: 'github-light', dark: 'github-dark' } as const;

const languages = ['csharp', 'xml', 'ini'] as const;

type Language = (typeof languages)[number];

const isLanguage = (lang: string): lang is Language => (languages as readonly string[]).includes(lang);

interface Highlighted {
	catalog: SiteCatalog;

	/** The rules that give a token class its light and dark color. */
	css: string;
}

/** Highlights every snippet of the catalog with Shiki, so the browser needs no highlighter. */
export async function highlightCatalog(source: SourceCatalog): Promise<Highlighted> {
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
		const language = isLanguage(value.lang) ? value.lang : 'text';
		const { tokens } = highlighter.codeToTokens(value.code, { lang: language, themes, defaultColor: false });

		return {
			lines: tokens.map((line, index) => ({
				text: value.code.split('\n')[index] ?? '',

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

		light.push(`.t${index}{color:${lightColor}}`);
		dark.push(`.t${index}{color:${darkColor}}`);
	}

	const css = [
		light.join('\n'),
		`@media (prefers-color-scheme: dark){\n${dark.map((rule) => `:root:not([data-theme="light"]) ${rule}`).join('\n')}\n}`,
		dark.map((rule) => `:root[data-theme="dark"] ${rule}`).join('\n'),
	].join('\n');

	return { catalog: { ...source, rules, options }, css };
}
