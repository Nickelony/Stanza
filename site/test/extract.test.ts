import { readdirSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import {
	ExtractionError,
	classifyValues,
	extractFromSources,
	literalDefault,
	parseOptionPage,
	parseRulePage,
	parseTier,
	splitFences,
} from '../build/extract.ts';
import { realCatalog, realSources, repositoryRoot, withText } from './fixtures.ts';

const rulePages = readdirSync(join(repositoryRoot, 'docs', 'rules')).filter((name) => name.endsWith('.md'));
const optionPages = readdirSync(join(repositoryRoot, 'docs', 'options')).filter((name) => name.endsWith('.md'));

const ruleOf = (id: string) => realCatalog.rules.find((rule) => rule.id === id)!;
const optionOf = (key: string) => realCatalog.options.find((option) => option.key === key)!;

describe('the real pages', () => {
	it('parse every rule page, with no list of rules in the site', () => {
		expect(rulePages.length).toBeGreaterThanOrEqual(72);
		expect(realCatalog.rules).toHaveLength(rulePages.length);

		for (const page of rulePages) {
			const id = page.slice(0, 6);

			expect(
				realCatalog.rules.some((rule) => rule.id === id),
				page,
			).toBe(true);
		}
	});

	it('give every rule a title, a group, an example with a changed After, and a wiki link', () => {
		for (const rule of realCatalog.rules) {
			expect(rule.title, rule.id).not.toBe('');
			expect(realCatalog.groups, rule.id).toContain(rule.group);
			expect(rule.summary, rule.id).not.toBe('');
			expect(rule.examples.length, rule.id).toBeGreaterThan(0);

			for (const example of rule.examples) {
				expect(example.before.code, rule.id).not.toBe('');
				expect(example.afters.length > 0, rule.id).toBe(!example.unchanged);

				for (const after of example.afters) {
					expect(after.snippet.code, rule.id).not.toBe(example.before.code);
				}
			}

			expect(rule.wikiUrl, rule.id).toMatch(/^https:\/\/github\.com\/Nickelony\/Stanza\/wiki\/ZA\d{4}-/);
		}
	});

	it('list the rules group by group as the readme does, each group in the order of its IDs', () => {
		const groups = realCatalog.rules.map((rule) => rule.group).filter((group, index, all) => group !== all[index - 1]);

		expect(groups).toEqual(realCatalog.groups);

		for (const group of realCatalog.groups) {
			const ids = realCatalog.rules.filter((rule) => rule.group === group).map((rule) => rule.id);

			expect(ids, group).toEqual([...ids].sort());
		}
	});

	it('parse every option page, and every key the readme lists has one', () => {
		expect(realCatalog.options).toHaveLength(optionPages.length);

		for (const option of realCatalog.options) {
			expect(option.defaultText, option.key).not.toBe('');
			expect(option.values.length, option.key).toBeGreaterThan(0);
			expect(['enum', 'number', 'list', 'text']).toContain(option.kind);
		}
	});

	it('read the tier of a rule from its page and check it against the readme', () => {
		expect(ruleOf('ZA0001')).toMatchObject({ tier: 'warning', onByDefault: true, markupOnByDefault: false });
		expect(ruleOf('ZA0003')).toMatchObject({ tier: 'suggestion', onByDefault: false });
		expect(ruleOf('ZA0035')).toMatchObject({ tier: 'suggestion', onByDefault: true });
		expect(ruleOf('ZA0101')).toMatchObject({ tier: 'warning', onByDefault: false, markupOnByDefault: true });
		expect(ruleOf('ZA0102')).toMatchObject({ onByDefault: false, markupOnByDefault: false });
	});

	it('mark the rules stanza-markup reads from its own list of diagnostics', () => {
		const reads = realCatalog.rules.filter((rule) => rule.readByMarkup).map((rule) => rule.id);

		expect(reads).toEqual(expect.arrayContaining(['ZA0101', 'ZA0102', 'ZA0103', 'ZA0104', 'ZA0105', 'ZA0203']));
		expect(reads).not.toContain('ZA0001');
		expect(reads).not.toContain('ZA0100');
	});

	it('split a page with several After segments and several examples', () => {
		expect(ruleOf('ZA0044').examples[0]!.afters.map((after) => after.label)).toEqual([
			'the trailing arrow stays where it was',
			'with ZA0046 enabled, the arrow opens its own line',
		]);

		expect(ruleOf('ZA0002').examples).toHaveLength(2);
		expect(ruleOf('ZA0203').examples).toHaveLength(2);
	});

	it('keep a comment of the example that is not a label as code', () => {
		expect(ruleOf('ZA0214').examples[0]!.before.code).toContain('//');
	});

	it('read an Unchanged example as code the rule leaves as written, with no After', () => {
		const unchanged = ruleOf('ZA0052').examples.find((example) => example.unchanged);

		expect(unchanged?.afters).toEqual([]);
		expect(unchanged?.before.code).toBe('total = 0;\n\ncount = 1;\n\nsize = 2;');
	});

	it('take the groups from the readme headings', () => {
		expect(realCatalog.groups[0]).toBe('Blank lines between units');
		expect(realCatalog.groups).toContain('Markup in raw strings');
	});

	it('take the markup extensions from the profile table', () => {
		expect(realCatalog.markupExtensions).toEqual(expect.arrayContaining(['xml', 'csproj', 'slnx', 'axaml']));
		expect(realCatalog.markupExtensions).not.toContain('resx');
	});

	it('type each option by the values its page allows', () => {
		expect(optionOf('stanza_max_line_length')).toMatchObject({ kind: 'number', min: 0, max: 1000, defaultValue: '100', audience: 'both' });
		expect(optionOf('stanza_tab_width')).toMatchObject({ kind: 'number', min: 1, max: 64, defaultValue: null });
		expect(optionOf('stanza_doc_indent_size')).toMatchObject({ kind: 'number', min: 0, max: 64, words: ['tab'], defaultValue: '0' });

		expect(optionOf('stanza_multiline_body_trigger')).toMatchObject({
			kind: 'enum',
			choices: ['body', 'signature', 'either', 'both'],
			defaultValue: 'either',
		});

		expect(optionOf('stanza_markup_enabled')).toMatchObject({ kind: 'enum', choices: ['true', 'false'], audience: 'markup', defaultValue: 'true' });
		expect(optionOf('stanza_assertion_roots')).toMatchObject({ kind: 'list', audience: 'analyzer', defaultValue: 'Assert, Expect, Should' });
		expect(optionOf('stanza_doc_weak_tail_words')).toMatchObject({ kind: 'list', defaultValue: 'none' });
		expect(optionOf('stanza_markup_profile')).toMatchObject({ kind: 'enum', perFile: true, defaultValue: null, markupDefaultValue: null });
		expect(optionOf('stanza_markup_profile').choices).toEqual(realCatalog.markupProfiles);
		expect(optionOf('stanza_doc_max_line_length')).toMatchObject({ defaultValue: null });
	});

	it('take the profiles from the profile table', () => {
		expect(realCatalog.markupProfiles).toEqual(['xml', 'msbuild', 'slnx', 'xaml']);
	});

	it('give the tool its own default only where the page says it differs', () => {
		const differing = realCatalog.options.filter((option) => option.defaultValue !== option.markupDefaultValue).map((option) => option.key);

		expect(differing).toEqual(['stanza_markup_scope']);
		expect(optionOf('stanza_markup_scope')).toMatchObject({ audience: 'both', defaultValue: 'all', markupDefaultValue: null });
		expect(optionOf('stanza_max_line_length')).toMatchObject({ defaultValue: '100', markupDefaultValue: '100' });
		expect(optionOf('stanza_markup_enabled')).toMatchObject({ defaultValue: 'true', markupDefaultValue: 'true' });
	});

	it('keep the text and the examples of a rule page in the order the page writes them', () => {
		const parts = ruleOf('ZA0203').parts;

		expect(parts.map((part) => part.kind)).toEqual(['example', 'p', 'example', 'ul']);
		expect(parts[1]).toMatchObject({ html: expect.stringContaining('The blank line after a start tag goes too') });
		expect(parts.filter((part) => part.kind === 'example')).toEqual([
			{ kind: 'example', index: 0 },
			{ kind: 'example', index: 1 },
		]);
	});

	it('keep the value table, the text and the examples of an option page in the order the page writes them', () => {
		const parts = optionOf('stanza_markup_scope').parts;

		expect(parts.map((part) => part.kind)).toEqual(['values', 'p', 'example', 'ul']);
		expect(parts[1]).toMatchObject({ html: expect.stringContaining('is the narrower reading') });
	});

	it('say who reads a key from the readme table and the page', () => {
		expect(optionOf('stanza_named_argument_count').audience).toBe('analyzer');
		expect(optionOf('stanza_markup_doc_reprint').audience).toBe('markup');
		expect(optionOf('stanza_markup_scope').audience).toBe('both');
		expect(optionOf('stanza_doc_expand').audience).toBe('both');
	});

	it('split an option example into labeled segments', () => {
		const segments = optionOf('stanza_named_argument_case').examples[0]!.segments;

		expect(segments.map((segment) => segment.value)).toEqual(['sensitive', 'insensitive']);
		expect(segments[0]!.isDefault).toBe(true);
		expect(segments[1]!.isDefault).toBe(false);
	});

	it('tie rules and options together from both sides', () => {
		expect(ruleOf('ZA0044').options).toEqual(expect.arrayContaining(['stanza_max_line_length', 'stanza_multiline_body_trigger']));
		expect(optionOf('stanza_assertion_roots').rules).toContain('ZA0001');
		expect(optionOf('stanza_max_line_length').rules).toContain('ZA0044');
	});
});

describe('a new rule page', () => {
	const page = [
		'# ZA0900 - A new rule',
		'',
		'**Suggestion, off by default.**',
		'Adds a blank line above a comment.',
		'',
		'```csharp',
		'// Before',
		'a();',
		'// keep',
		'b();',
		'',
		'// After',
		'a();',
		'',
		'// keep',
		'b();',
		'```',
		'',
		'- A note about `stanza_max_line_length`.',
		'',
	].join('\n');

	it('appears with a readme row and no change to the site', () => {
		const sources = realSources();

		const withPage = withText(
			{ ...sources, rules: [...sources.rules, { file: 'docs/rules/ZA0900-A-new-rule.md', text: page }] },
			'src/Nickelony.Stanza.Analyzers/README.md',
			(text) =>
				text.replace(
					/^(\| `ZA0216` .*\n)/m,
					'$1| `ZA0900` | [Adds a blank line above a comment](https://github.com/Nickelony/Stanza/wiki/ZA0900-A-new-rule) | Suggestion, off by default |\n',
				),
		);

		const added = extractFromSources(withPage).rules.find((rule) => rule.id === 'ZA0900')!;

		expect(added).toMatchObject({ title: 'A new rule', group: 'Plain comments', tier: 'suggestion', onByDefault: false });
		expect(added.examples[0]!.before.code).toBe('a();\n// keep\nb();');
		expect(added.examples[0]!.afters[0]!.snippet.code).toBe('a();\n\n// keep\nb();');
		expect(added.options).toEqual(['stanza_max_line_length']);
	});
});

describe('a tier line', () => {
	it('reads the severity and whether the rule is on by default', () => {
		expect(parseTier('x.md', 'Warning, on by default')).toEqual({ tier: 'warning', onByDefault: true, markupOn: false });
		expect(parseTier('x.md', 'Suggestion, off by default')).toEqual({ tier: 'suggestion', onByDefault: false, markupOn: false });
	});

	it('reads the stanza-markup clause', () => {
		expect(parseTier('x.md', 'Warning, off by default in the analyzer and on in `stanza-markup`')).toEqual({
			tier: 'warning',
			onByDefault: false,
			markupOn: true,
		});
	});

	it('fails on a shape it does not know, and names the file', () => {
		expect(() => parseTier('docs/rules/x.md', 'Loud, sometimes')).toThrowError(/docs\/rules\/x\.md/);
	});
});

describe('a default cell of the readme', () => {
	it('is a literal only when it holds nothing but backticked values', () => {
		expect(literalDefault('`100`')).toBe('100');
		expect(literalDefault('`Assert`, `Expect`, `Should`')).toBe('Assert, Expect, Should');
		expect(literalDefault('`stanza_max_line_length`, then `100`')).toBeNull();
		expect(literalDefault("the file's `tab_width`, then its `indent_size`, then `4`")).toBeNull();
	});
});

describe('the value column of an option page', () => {
	it('reads literals as an enum', () => {
		expect(classifyValues('x.md', ['`allow`', '`require`', 'anything else'])).toMatchObject({ kind: 'enum', choices: ['allow', 'require'] });
	});

	it('reads a range, with the literals that widen it or stand beside it', () => {
		expect(classifyValues('x.md', ['`0`', 'a number from 1 to 1000', 'below zero, above 1000, absent, or not a number'])).toMatchObject({
			kind: 'number',
			min: 0,
			max: 1000,
			words: [],
		});

		expect(classifyValues('x.md', ['`0`', 'a number from 1 to 64', '`tab`'])).toMatchObject({ kind: 'number', min: 0, max: 64, words: ['tab'] });
	});

	it('does not count a row that describes an invalid value', () => {
		expect(classifyValues('x.md', ['a number from 1 to 64', '`0`, below zero, above 64, empty, absent, or not a number'])).toMatchObject({
			min: 1,
			max: 64,
		});
	});

	it('fails on a cell it cannot read, and names the file', () => {
		expect(() => classifyValues('docs/options/k.md', ['whatever you like'])).toThrowError(/docs\/options\/k\.md/);
	});
});

describe('a page that does not parse', () => {
	const rulePath = 'docs/rules/ZA0001-Separate-declaration-and-invocation-runs.md';
	const optionPath = 'docs/options/stanza_tab_width.md';
	const readmePath = 'src/Nickelony.Stanza.Analyzers/README.md';

	const failsOn = (file: string, change: (text: string) => string, message: RegExp, reportedFile = file): void => {
		let error: unknown;

		try {
			extractFromSources(withText(realSources(), file, change));
		} catch (caught) {
			error = caught;
		}

		expect(error, `${file} should fail the extraction`).toBeInstanceOf(ExtractionError);
		expect((error as ExtractionError).message).toContain(reportedFile);
		expect((error as ExtractionError).message).toMatch(message);
	};

	it('fails the build when the heading names another rule', () => {
		failsOn(rulePath, (text) => text.replace('# ZA0001 - ', '# ZA0002 - '), /heading names ZA0002/);
	});

	it('fails the build when the tier line is missing', () => {
		failsOn(rulePath, (text) => text.replace(/\*\*Warning, on by default\.\*\*\n/, ''), /bold tier line/);
	});

	it('fails the build when the tier line disagrees with the readme', () => {
		failsOn(rulePath, (text) => text.replace('**Warning, on by default.**', '**Suggestion, on by default.**'), /disagrees/);
	});

	it('fails the build when the example has no Before', () => {
		failsOn(rulePath, (text) => text.replace('// Before', '// Earlier'), /expected|Before/);
	});

	it('fails the build when the example has no After', () => {
		failsOn(rulePath, (text) => text.replace(/\/\/ After[\s\S]*?```/, '```'), /no "\/\/ After"/);
	});

	it('fails the build when a code fence is never closed', () => {
		failsOn(rulePath, (text) => text.replace(/```\n(?![\s\S]*```\n)/, ''), /never closed/);
	});

	it('fails the build when the page has no example at all', () => {
		failsOn(rulePath, (text) => text.replace(/```csharp[\s\S]*?```\n/g, ''), /no "\/\/ Before"/);
	});

	it('fails the build when the readme has no row for the page', () => {
		failsOn(readmePath, (text) => text.replace(/^\| `ZA0002` .*\n/m, ''), /ZA0002 has no row/, 'docs/rules/ZA0002-Separate-member-groups.md');
	});

	it('fails the build when the readme lists a rule with no page', () => {
		failsOn(
			readmePath,
			(text) =>
				text.replace(
					/^(\| `ZA0002` .*\n)/m,
					'$1| `ZA0999` | [Adds a blank line](https://github.com/Nickelony/Stanza/wiki/ZA0999-Nothing) | Warning |\n',
				),
			/ZA0999/,
		);
	});

	it('fails the build when an option page has no Default line', () => {
		failsOn(optionPath, (text) => text.replace(/^Default: .*\n/m, ''), /Default:/);
	});

	it('fails the build when an option page has no value table', () => {
		failsOn(optionPath, (text) => text.replace(/\| Value[\s\S]*?\n\n/, ''), /"Value"/);
	});

	it('fails the build when the readme lists a key with no page', () => {
		failsOn(
			readmePath,
			(text) =>
				text.replace(
					/^(\| \[`stanza_tab_width`\].*\n)/m,
					'$1| [`stanza_nothing`](https://github.com/Nickelony/Stanza/wiki/stanza_nothing) | `1` |\n',
				),
			/stanza_nothing/,
		);
	});

	it('fails the build when an option row of the readme does not match the shape of a key row', () => {
		failsOn(readmePath, (text) => text.replace('| [`stanza_tab_width`](', '| [`Stanza_tab_width`]('), /looks like an option row/);
	});

	it('fails the build when an option row of the readme has another number of cells than its header', () => {
		failsOn(readmePath, (text) => text.replace(/^(\| \[`stanza_tab_width`\].*) \|$/m, '$1 | extra |'), /cells under a header/);
	});

	it('fails the build when a Default line names a value of the table without giving a literal default', () => {
		failsOn(
			'docs/options/stanza_markup_enabled.md',
			(text) => text.replace('Default: `true`.', 'Default: `true` unless a file says otherwise.'),
			/names `true`/,
		);
	});

	it('fails the build when a Default line gives the tool another default without naming the analyzer one', () => {
		failsOn(
			'docs/options/stanza_markup_scope.md',
			(text) => text.replace('Default: `all` in the analyzer', 'Default: everything in the analyzer'),
			/does not open with/,
		);
	});

	it('fails the build when a rule ID of the markup diagnostics is not a const string declaration', () => {
		failsOn(
			'src/Nickelony.Stanza.Markup.Core/MarkupDiagnostics.cs',
			(text) => text.replace('const string SeparateMultiLineMarkupElementsId', 'static readonly string SeparateMultiLineMarkupElementsId'),
			/rule ID strings/,
		);
	});

	it.each([
		['a heading', '## Notes'],
		['a numbered item', '1. First'],
		['a quote', '> Quoted'],
		['a nested bullet', '- one\n  - nested'],
		['a star bullet', '* one'],
	])('fails the build on %s, which the renderer would print as plain text', (_what, markdown) => {
		failsOn(rulePath, (text) => `${text.trimEnd()}\n\n${markdown}\n`, /does not support/);
	});

	it.each([
		['a script address', '[x](javascript:run)'],
		['a data address', '[x](data:text/html,abc)'],
		['a plain http address', '[x](http://example.test/)'],
		['a protocol relative address', '[x](//example.test/)'],
	])('fails the build on a link to %s', (_what, markdown) => {
		failsOn(rulePath, (text) => `${text.trimEnd()}\n\n${markdown}\n`, /neither an https/);
	});

	it('fails the build when a table row has another number of cells than its header', () => {
		failsOn(rulePath, (text) => `${text.trimEnd()}\n\n| A | B |\n| --- | --- |\n| 1 | 2 | 3 |\n`, /another number of cells/);
	});

	it('names the file when a rule page is parsed alone', () => {
		expect(() => parseRulePage('docs/rules/ZA0001-Separate.md', 'no heading here', undefined)).toThrowError(/docs\/rules\/ZA0001-Separate\.md/);
		expect(() => parseOptionPage('docs/options/stanza_x.md', '# stanza_y\n')).toThrowError(/docs\/options\/stanza_x\.md/);
	});

	it('splits fences without losing the text between them', () => {
		const chunks = splitFences('x.md', 'a\n```csharp\ncode\n```\nb');
		expect(chunks.map((chunk) => chunk.kind)).toEqual(['text', 'code', 'text']);
	});
});
