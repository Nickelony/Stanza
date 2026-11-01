import { describe, expect, it } from 'vitest';

import { catalogReaders } from '../../build/catalogs.ts';
import { asParsed, mapStrings } from '../../build/plugin.ts';
import { highlightCatalog } from '../../build/render/highlight.ts';
import { languages } from '../../src/languages/index.ts';
import { hasEditorConfigTools, hasLevels, type LanguageId } from '../../src/languages/language.ts';
import { pages } from '../../src/languages/pages.ts';
import { emptyConfig } from '../../src/state/config.ts';
import type { SourceCatalog } from '../../src/state/model.ts';
import { compactHtml } from '../../src/util/compact.ts';
import { realCatalog, realJavascript, realMarkup, realPython, repositoryRoot } from '../fixtures.ts';

const catalogs: Record<LanguageId, SourceCatalog> = { csharp: realCatalog, markup: realMarkup, js: realJavascript, python: realPython };

describe('the descriptor of a language', () => {
	it.each(languages.map((language) => [language.id, language] as const))('is consistent for %s', (_id, language) => {
		expect(language.ruleSettings).toContain('none');
		expect(language.ruleSettings.every((setting) => language.severityLabels[setting] !== undefined)).toBe(true);
		expect(language.severities.length === 0 || language.severities.includes(language.defaultSeverity)).toBe(true);
		expect(language.outputs.filter((block) => block.tools).length).toBeLessThanOrEqual(1);
	});

	it('offers the import, the conflict list and the file choices on the C# page and the Markup page alone', () => {
		expect(languages.filter(hasEditorConfigTools).map((language) => language.id)).toEqual(['csharp', 'markup']);
	});

	it('names each language once, and each page by its own path', () => {
		expect(new Set(languages.map((language) => language.id)).size).toBe(languages.length);
		expect(pages.map((page) => page.id)).toEqual(languages.map((language) => language.id));
		expect(new Set(pages.map((page) => page.path)).size).toBe(pages.length);
		expect(pages.every((page) => page.path === '' || /^[a-z]+[/]$/.test(page.path))).toBe(true);
		expect(Object.keys(catalogReaders).toSorted()).toEqual(languages.map((language) => language.id).toSorted());
	});

	it.each(languages.map((language) => [language.id, language] as const))('writes text for every output of %s', async (id, language) => {
		const { catalog } = await highlightCatalog(catalogs[id], id);
		const config = emptyConfig(language.defaultSeverity);

		for (const block of language.outputs) {
			const text = block.write(catalog, config);
			expect(text.length > 0 || block.empty !== null, block.title).toBe(true);
		}
	});

	it.each(languages.map((language) => [language.id] as const))('reads the same catalog for %s as the tests do', (id) => {
		expect(catalogReaders[id].read(repositoryRoot)).toEqual(catalogs[id]);
	});
});

describe('the descriptor of the Markup page', () => {
	const byId = (id: LanguageId) => languages.find((language) => language.id === id)!;

	it('has no level, no severity picker and no formatter, and writes the .editorconfig alone', () => {
		expect(languages.filter(hasLevels).map((language) => language.id)).toEqual(['csharp', 'js', 'python']);

		const markup = byId('markup');

		expect(markup.severities).toEqual([]);
		expect(markup.formatterPicker).toBe(false);
		expect(markup.outputs.map((block) => block.title)).toEqual(['.editorconfig']);
		expect(markup.ruleSettings).toEqual(byId('csharp').ruleSettings);
		expect(markup.everySettingHint).toBeTruthy();
	});

	it('shares the .editorconfig block of the C# page, so both write and import the same file', () => {
		expect(byId('markup').outputs[0]).toBe(byId('csharp').outputs[1]);
	});

	it('sits second in the header, after C#, and answers its own address', () => {
		expect(pages.map((page) => page.id)).toEqual(['csharp', 'markup', 'js', 'python']);
		expect(pages[1]).toMatchObject({ name: 'Markup', path: 'markup/' });
	});

	it('points each of the two pages at the other, and nowhere else', () => {
		expect(byId('csharp').elsewhere?.id).toBe('markup');
		expect(byId('markup').elsewhere?.id).toBe('csharp');
		expect(byId('js').elsewhere).toBeNull();
		expect(byId('python').elsewhere).toBeNull();
	});

	it('names in its note the rules and the key that its catalog sends to the other page', () => {
		const { rules, options } = realCatalog.elsewhere!;

		expect(byId('csharp').elsewhere!.lead).toContain(`${rules[0]} to ${rules.at(-1)}`);
		expect(options.length).toBeGreaterThan(0);
		expect(byId('markup').elsewhere!.lead).toContain(realMarkup.elsewhere!.options[0]);
	});

	it('is the only page whose catalog holds the rules without a level', () => {
		expect(realMarkup.rules.every((rule) => rule.level === 'no-level')).toBe(true);
		expect(realCatalog.rules.some((rule) => rule.level === 'no-level')).toBe(false);
		expect(realJavascript.rules.some((rule) => rule.level === 'no-level')).toBe(false);
		expect(realPython.rules.some((rule) => rule.level === 'no-level')).toBe(false);
	});
});

describe('the highlighted catalog of JS and Python', () => {
	it.each([['js'], ['python']] as const)('colors the examples of %s and ships as one JSON string', async (id) => {
		const { catalog, css } = await highlightCatalog(catalogs[id], id);
		const html = catalog.rules.flatMap((rule) => rule.examples.flatMap((example) => example.before.lines.map((line) => line.html))).join('\n');

		expect(html).toContain('<span class="t');
		expect(css).toContain('.t0{color:');
		expect(() => asParsed(mapStrings(catalog, compactHtml))).not.toThrow();
	});
});
