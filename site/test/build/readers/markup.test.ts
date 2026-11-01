import { describe, expect, it } from 'vitest';

import { extractFromSources } from '../../../build/readers/extract.ts';
import { isMarkupKey, isMarkupRule, markupRuleIds, splitMarkup, unwrapRawString } from '../../../build/readers/markup.ts';
import { ExtractionError, labelSegments } from '../../../build/readers/source.ts';
import { realCatalog, realCombined, realMarkup, realSources, withText } from '../../fixtures.ts';

const ids = (rules: Array<{ id: string }>): string[] => rules.map((rule) => rule.id);
const keys = (options: Array<{ key: string }>): string[] => options.map((option) => option.key);

describe('the split of the catalog into the C# page and the Markup page', () => {
	it('names the markup rules once, as ZA2001 to ZA2010', () => {
		expect(markupRuleIds).toHaveLength(10);
		expect(markupRuleIds[0]).toBe('ZA2001');
		expect(markupRuleIds.at(-1)).toBe('ZA2010');
		expect(isMarkupRule('ZA2005')).toBe(true);
		expect(isMarkupRule('ZA2901')).toBe(false);
		expect(isMarkupRule('ZA0903')).toBe(false);
	});

	it('gives each rule to one page, and loses none', () => {
		expect(ids(realMarkup.rules)).toEqual(markupRuleIds);
		expect(ids(realCatalog.rules).some(isMarkupRule)).toBe(false);
		expect(ids(realCatalog.rules).length + ids(realMarkup.rules).length).toBe(realCombined.rules.length);
	});

	it('gives each key to one page, and keeps the key that picks the C# literals on the C# page', () => {
		expect(keys(realMarkup.options)).toEqual([
			'stanza_markup_doc_reprint',
			'stanza_markup_enabled',
			'stanza_markup_max_blank_lines',
			'stanza_markup_profile',
			'stanza_markup_scope',
			'stanza_markup_separated_elements',
		]);

		expect(keys(realCatalog.options)).toContain('stanza_markup_literals');
		expect(keys(realCatalog.options).filter((key) => key.startsWith('stanza_markup_'))).toEqual(['stanza_markup_literals']);
		expect(keys(realCatalog.options).length + keys(realMarkup.options).length).toBe(realCombined.options.length);
	});

	it('takes a key by what stanza-markup reads, not by its name alone', () => {
		expect(isMarkupKey({ key: 'stanza_markup_scope', csharp: { audience: 'both' } })).toBe(true);
		expect(isMarkupKey({ key: 'stanza_markup_enabled', csharp: { audience: 'markup' } })).toBe(true);
		expect(isMarkupKey({ key: 'stanza_markup_literals', csharp: { audience: 'analyzer' } })).toBe(false);
		expect(isMarkupKey({ key: 'stanza_doc_max_line_length', csharp: { audience: 'both' } })).toBe(false);
	});

	it('leaves the rules that stanza-markup also reads on the C# page, where they have a level', () => {
		const reads = realCatalog.rules.filter((rule) => rule.csharp!.readByMarkup);

		expect(ids(reads)).toEqual(expect.arrayContaining(['ZA0903', 'ZA0909']));
		expect(reads.every((rule) => rule.level !== 'no-level')).toBe(true);
	});

	it('keeps each rule to the keys of its page, and each key to the rules of its page', () => {
		const markupKeys = new Set(keys(realMarkup.options));
		const csharpKeys = new Set(keys(realCatalog.options));

		for (const rule of realMarkup.rules) {
			expect(
				rule.options.every((key) => markupKeys.has(key)),
				rule.id,
			).toBe(true);
		}

		for (const rule of realCatalog.rules) {
			expect(
				rule.options.every((key) => csharpKeys.has(key)),
				rule.id,
			).toBe(true);
		}

		for (const option of realMarkup.options) {
			expect(
				option.rules.every((id) => isMarkupRule(id)),
				option.key,
			).toBe(true);
		}

		expect(realCatalog.options.find((option) => option.key === 'stanza_markup_literals')!.rules).toEqual([]);

		expect(realMarkup.options.find((option) => option.key === 'stanza_markup_scope')!.rules).toEqual(
			expect.arrayContaining(['ZA2001', 'ZA2002', 'ZA2003']),
		);
	});

	it('gives each page the pairs and the groups of its rules', () => {
		expect(realMarkup.pairs.map((pair) => [pair.first, pair.second])).toEqual([
			['ZA2001', 'ZA2002'],
			['ZA2008', 'ZA2009'],
		]);

		expect(realCatalog.pairs.length + realMarkup.pairs.length).toBe(realCombined.pairs.length);
		expect(realCatalog.pairs.some((pair) => isMarkupRule(pair.first) || isMarkupRule(pair.second))).toBe(false);
		expect(realMarkup.groups).toEqual(['Markup rules']);
		expect(realCatalog.groups).not.toContain('Markup rules');
		expect(realCatalog.rules.every((rule) => realCatalog.groups.includes(rule.group))).toBe(true);
	});

	it('shares the profiles and the extensions of the markup files, which both pages write', () => {
		expect(realMarkup.markupExtensions).toEqual(realCombined.markupExtensions);
		expect(realMarkup.markupProfiles).toEqual(realCombined.markupProfiles);
		expect(realCatalog.markupExtensions).toEqual(realCombined.markupExtensions);
	});

	it('tells each page what the other holds', () => {
		expect(realCatalog.elsewhere).toEqual({ page: 'Markup', rules: markupRuleIds, options: keys(realMarkup.options) });
		expect(realMarkup.elsewhere).toEqual({ page: 'C#', rules: [], options: ['stanza_markup_literals'] });
	});

	it('fails the build on a pair that joins a markup rule to another rule', () => {
		const mixed = {
			...realCombined,
			pairs: [...realCombined.pairs, { first: 'ZA2003', second: 'ZA0101', defaultHalf: 'ZA0101', ownerWhenBothEnabled: 'ZA0101' }],
		};

		expect(() => splitMarkup(mixed)).toThrowError(/pair of ZA2003 and ZA0101 joins a markup rule to another rule/);
	});
});

describe('the examples of the Markup page', () => {
	it('show the XML a markup file holds, without the C# raw string', () => {
		for (const rule of realMarkup.rules) {
			for (const example of rule.examples) {
				const snippets = [example.before, ...example.afters.map((after) => after.snippet)];

				for (const snippet of snippets) {
					expect(snippet.lang, rule.id).toBe('xml');
					expect(snippet.code, rule.id).not.toMatch(/"""|language=xml|const string/);
					expect(snippet.code.startsWith('<'), rule.id).toBe(true);
					expect(snippet.code.includes('\t'), rule.id).toBe(false);
				}
			}
		}

		const [first] = realMarkup.rules;

		expect(first!.examples[0]!.before.code).toBe(
			[
				'<Project>',
				'  <ItemGroup>',
				'    <PackageReference Include="a" />',
				'  </ItemGroup>',
				'  <ItemGroup>',
				'    <PackageReference Include="b" />',
				'  </ItemGroup>',
				'</Project>',
			].join('\n'),
		);
	});

	it('keep the unchanged example of ZA2010', () => {
		expect(realMarkup.rules.find((rule) => rule.id === 'ZA2010')!.examples.some((example) => example.unchanged)).toBe(true);
	});

	it('unwrap the C# example of a key, and leave the XML and INI examples as they are', () => {
		const blank = realMarkup.options.find((option) => option.key === 'stanza_markup_max_blank_lines')!;
		const scope = realMarkup.options.find((option) => option.key === 'stanza_markup_scope')!;
		const enabled = realMarkup.options.find((option) => option.key === 'stanza_markup_enabled')!;

		expect(blank.examples[0]!.segments.map((segment) => segment.snippet.lang)).toEqual(['xml', 'xml']);
		expect(blank.examples[0]!.segments[0]!.snippet.code.startsWith('<Project>')).toBe(true);
		expect(scope.examples[0]!.segments.every((segment) => segment.snippet.lang === 'xml')).toBe(true);
		expect(enabled.examples[0]!.segments[0]!.snippet.lang).toBe('ini');
	});

	it('keep the C# examples of the C# page', () => {
		expect(realCatalog.rules.flatMap((rule) => rule.examples.map((example) => example.before.lang))).not.toContain('xml');
		expect(realCatalog.options.find((option) => option.key === 'stanza_markup_literals')!.examples[0]!.segments[0]!.snippet.lang).toBe('csharp');
	});

	it('fail the build on a C# example that is not a raw string constant', () => {
		const sources = withText(realSources(), 'docs/rules/ZA2001-Separate-multi-line-markup-elements.md', (text) =>
			text.replace('const string Project = """', 'var project = """'),
		);

		expect(() => splitMarkup(extractFromSources(sources))).toThrowError(ExtractionError);

		expect(() => splitMarkup(extractFromSources(sources))).toThrowError(
			/ZA2001-Separate-multi-line-markup-elements\.md: .*not a raw string constant/,
		);
	});
});

describe('unwrapping a raw string example', () => {
	const wrapped = (lines: string[]) => ({ lang: 'csharp', code: lines.join('\n') });

	it('cuts the marker, the constant and the closing line, and the indent of the markup', () => {
		const code = ['// language=xml', 'const string Page = """', '\t<A>', '\t  <B />', '', '\t</A>', '\t""";'];
		expect(unwrapRawString('x.md', wrapped(code))).toEqual({ lang: 'xml', code: '<A>\n  <B />\n\n</A>' });
	});

	it('takes an example with no marker', () => {
		expect(unwrapRawString('x.md', wrapped(['const string Page = """', '\t<A />', '\t""";']))).toEqual({ lang: 'xml', code: '<A />' });
	});

	it('leaves a snippet that is not C# as it is', () => {
		const ini = { lang: 'ini', code: '[*.xml]' };
		expect(unwrapRawString('x.md', ini)).toBe(ini);
	});

	it('fails, with the file, on a snippet in another shape', () => {
		expect(() => unwrapRawString('docs/x.md', wrapped(['var page = """', '\t<A />', '\t""";']))).toThrowError(
			/docs\/x\.md: .*not a raw string constant/,
		);

		expect(() => unwrapRawString('docs/x.md', wrapped(['const string Page = """', '\t<A />']))).toThrowError(/docs\/x\.md/);

		expect(() => unwrapRawString('docs/x.md', wrapped(['const string Page = """', '<A />', '\t""";']))).toThrowError(
			/not indented as its closing line is/,
		);
	});
});

describe('the labels of an option example', () => {
	it('keeps the marker of a raw string in the segment under its label', () => {
		const lines = ['// language=xml', 'const string Page = """', '\t<A />', '\t""";'];
		expect(labelSegments('csharp', ['// Before - a label', ...lines].join('\n'))).toEqual([{ label: 'Before - a label', code: lines.join('\n') }]);
	});
});
