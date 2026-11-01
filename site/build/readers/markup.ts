import { pages } from '../../src/languages/pages.ts';
import {
	audienceOf,
	type CSharpOption,
	type Option,
	type Rule,
	type RulePair,
	type SourceCatalog,
	type SourceSnippet,
} from '../../src/state/model.ts';

import { ExtractionError, pageName } from './source.ts';

/**
 * The rules of the Markup page, ZA2001 to ZA2010, which the readme marks `No level`: no level holds one, so only its own severity line enables it.
 * This is the one place that names them. The extraction fails when the readme, the level table and the rule pages do not agree with it.
 */
export const markupRuleIds: readonly string[] = Array.from({ length: 10 }, (_, index) => `ZA${2001 + index}`);
export const isMarkupRule = (id: string): boolean => markupRuleIds.includes(id);

/** Whether the key belongs to the Markup page: a `stanza_markup_` key that `stanza-markup` reads. The key that picks the C# literals stays on the C# page. */
export const isMarkupKey = (option: { key: string; csharp?: Pick<CSharpOption, 'audience'> }): boolean =>
	option.key.startsWith('stanza_markup_') && audienceOf(option) !== 'analyzer';

const pageOf = (id: 'csharp' | 'markup'): string => pages.find((page) => page.id === id)!.name;

/**
 * Cuts the C# raw string away from an example of the Markup page, so the page shows the XML a markup file holds.
 * The rule pages write each example as an optional `// language=xml` line, `const string Name = """`, the indented markup, and `""";`.
 * An example in another shape fails the build, because it would otherwise show the wrapper.
 */
export function unwrapRawString(file: string, snippet: SourceSnippet): SourceSnippet {
	if (snippet.lang !== 'csharp') {
		return snippet;
	}

	const lines = snippet.code.split('\n');

	if (lines[0] === '// language=xml') {
		lines.shift();
	}

	const open = lines.shift() ?? '';
	const close = lines.pop() ?? '';

	if (!/^const string \w+ = """$/.test(open) || !/^\t""";$/.test(close)) {
		throw new ExtractionError(file, `a C# example of a markup page is not a raw string constant with indented markup: ${open}`);
	}

	const body = lines.map((line) => {
		if (line === '') {
			return line;
		}

		if (!line.startsWith('\t')) {
			throw new ExtractionError(file, `a line of a raw string example is not indented as its closing line is: ${line}`);
		}

		return line.slice(1);
	});

	return { lang: 'xml', code: body.join('\n') };
}

function unwrapRule(rule: Rule<SourceSnippet>): Rule<SourceSnippet> {
	const file = `docs/rules/${pageName(rule.pageUrl)}.md`;

	return {
		...rule,

		examples: rule.examples.map((example) => ({
			...example,
			before: unwrapRawString(file, example.before),
			afters: example.afters.map((after) => ({ ...after, snippet: unwrapRawString(file, after.snippet) })),
		})),
	};
}

function unwrapOption(option: Option<SourceSnippet>): Option<SourceSnippet> {
	const file = `docs/options/${option.key}.md`;

	return {
		...option,

		examples: option.examples.map((example) => ({
			segments: example.segments.map((segment) => ({ ...segment, snippet: unwrapRawString(file, segment.snippet) })),
		})),
	};
}

/** Keeps what a half of the split names: a rule keeps the keys of the half, and a key the rules of it. */
function restrict(
	rules: Rule<SourceSnippet>[],
	options: Option<SourceSnippet>[],
): { rules: Rule<SourceSnippet>[]; options: Option<SourceSnippet>[] } {
	const ids = new Set(rules.map((rule) => rule.id));
	const keys = new Set(options.map((option) => option.key));

	return {
		rules: rules.map((rule) => ({ ...rule, options: rule.options.filter((key) => keys.has(key)) })),
		options: options.map((option) => ({ ...option, rules: option.rules.filter((id) => ids.has(id)) })),
	};
}

function splitPairs(pairs: RulePair[]): { markup: RulePair[]; csharp: RulePair[] } {
	const markup = pairs.filter((pair) => isMarkupRule(pair.first) && isMarkupRule(pair.second));
	const csharp = pairs.filter((pair) => !isMarkupRule(pair.first) && !isMarkupRule(pair.second));

	if (markup.length + csharp.length !== pairs.length) {
		const mixed = pairs.find((pair) => isMarkupRule(pair.first) !== isMarkupRule(pair.second))!;

		throw new ExtractionError(
			'docs/Which-rule-owns-which-gap.md',
			`the pair of ${mixed.first} and ${mixed.second} joins a markup rule to another rule`,
		);
	}

	return { markup, csharp };
}

/**
 * Splits the catalog the pages describe into the catalog of the C# page and the catalog of the Markup page.
 * The Markup page owns the markup rules and the keys that `stanza-markup` reads, shows their examples as the XML a markup file holds,
 * and has no level. Each catalog names what it lacks, so an import can tell a visitor where a line belongs.
 */
export function splitMarkup(catalog: SourceCatalog): { csharp: SourceCatalog; markup: SourceCatalog } {
	const pairs = splitPairs(catalog.pairs);
	const markupRules = catalog.rules.filter((rule) => isMarkupRule(rule.id));
	const csharpRules = catalog.rules.filter((rule) => !isMarkupRule(rule.id));
	const markupOptions = catalog.options.filter(isMarkupKey);
	const csharpOptions = catalog.options.filter((option) => !isMarkupKey(option));
	const markup = restrict(markupRules.map(unwrapRule), markupOptions.map(unwrapOption));
	const csharp = restrict(csharpRules, csharpOptions);
	const groups = (rules: Rule<SourceSnippet>[]): string[] => catalog.groups.filter((group) => rules.some((rule) => rule.group === group));

	return {
		csharp: {
			...catalog,
			...csharp,
			pairs: pairs.csharp,
			groups: groups(csharp.rules),

			elsewhere: {
				page: pageOf('markup'),
				rules: markup.rules.map((rule) => rule.id),
				options: markup.options.map((option) => option.key),
			},
		},

		markup: {
			...catalog,
			...markup,
			pairs: pairs.markup,
			groups: groups(markup.rules),

			elsewhere: {
				page: pageOf('csharp'),
				rules: [],
				options: csharp.options.filter((option) => option.key.startsWith('stanza_markup_')).map((option) => option.key),
			},
		},
	};
}
