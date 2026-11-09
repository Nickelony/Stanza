import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import { extractFromSources } from '../../../build/readers/extract.ts';
import { markupRuleIds } from '../../../build/readers/markup.ts';
import { classifyValues, parseOptionPage } from '../../../build/readers/option-page.ts';
import { literalDefault, parseTier } from '../../../build/readers/readme.ts';
import { parseRulePage } from '../../../build/readers/rule-page.ts';
import { ExtractionError, splitFences } from '../../../build/readers/source.ts';
import { failsOn, realCombined, realSources, repositoryRoot, withText } from '../../fixtures.ts';

const rulePages = readdirSync(join(repositoryRoot, 'docs', 'rules')).filter((name) => name.endsWith('.md'));
const optionPages = readdirSync(join(repositoryRoot, 'docs', 'options')).filter((name) => name.endsWith('.md'));
const ruleOf = (id: string) => realCombined.rules.find((rule) => rule.id === id)!;
const optionOf = (key: string) => realCombined.options.find((option) => option.key === key)!;

describe('the real pages', () => {
	it('parse every rule page, with no list of rules in the site', () => {
		// The folder is read, so only an empty one could make the comparison below pass without reading a page.
		expect(rulePages.length).toBeGreaterThan(0);
		expect(realCombined.rules).toHaveLength(rulePages.length);

		for (const page of rulePages) {
			const id = page.slice(0, 6);

			expect(
				realCombined.rules.some((rule) => rule.id === id),
				page,
			).toBe(true);
		}
	});

	it('give every rule a title, a group, an example with a changed After, and a wiki link', () => {
		for (const rule of realCombined.rules) {
			expect(rule.title, rule.id).not.toBe('');
			expect(realCombined.groups, rule.id).toContain(rule.group);
			expect(rule.summary, rule.id).not.toBe('');
			expect(rule.examples.length, rule.id).toBeGreaterThan(0);

			for (const example of rule.examples) {
				expect(example.before.code, rule.id).not.toBe('');
				expect(example.afters.length > 0, rule.id).toBe(!example.unchanged);

				for (const after of example.afters) {
					expect(after.snippet.code, rule.id).not.toBe(example.before.code);
				}
			}

			expect(rule.pageUrl, rule.id).toMatch(/^https:\/\/github\.com\/Nickelony\/Stanza\/wiki\/ZA\d{4}-/);
		}
	});

	it('list the rules group by group as the readme does, each group in the order of its IDs', () => {
		const groups = realCombined.rules.map((rule) => rule.group).filter((group, index, all) => group !== all[index - 1]);

		expect(groups).toEqual(realCombined.groups);

		for (const group of realCombined.groups) {
			const ids = realCombined.rules.filter((rule) => rule.group === group).map((rule) => rule.id);
			expect(ids, group).toEqual([...ids].sort());
		}
	});

	it('parse every option page, each with a default, a value table and a kind', () => {
		expect(realCombined.options).toHaveLength(optionPages.length);

		for (const option of realCombined.options) {
			expect(option.defaultText, option.key).not.toBe('');
			expect(option.csharp!.values.length, option.key).toBeGreaterThan(0);
			expect(['enum', 'number', 'list', 'text']).toContain(option.kind);
		}
	});

	it('read the tier and the markup default of a rule from its page', () => {
		expect(ruleOf('ZA0002')).toMatchObject({ tier: 'suggestion', onByDefault: true, csharp: { markupOnByDefault: false } });
		expect(ruleOf('ZA0014')).toMatchObject({ tier: 'suggestion', onByDefault: false });
		expect(ruleOf('ZA0202')).toMatchObject({ tier: 'suggestion', onByDefault: true });
		expect(ruleOf('ZA2001')).toMatchObject({ tier: 'suggestion', onByDefault: false, csharp: { markupOnByDefault: true } });
		expect(ruleOf('ZA2002')).toMatchObject({ onByDefault: false, csharp: { markupOnByDefault: false } });
	});

	it('mark the rules stanza-markup reads from its own list of diagnostics', () => {
		const reads = realCombined.rules.filter((rule) => rule.csharp!.readByMarkup).map((rule) => rule.id);

		expect(reads).toEqual(expect.arrayContaining(['ZA2001', 'ZA2002', 'ZA2003', 'ZA2006', 'ZA2007', 'ZA2010', 'ZA0903']));
		expect(reads).not.toContain('ZA0002');
		expect(reads).not.toContain('ZA2901');
	});

	it('split a page with several After segments and several examples', () => {
		expect(ruleOf('ZA0305').examples[0]!.afters.map((after) => after.label)).toEqual([
			'the trailing arrow stays where it was',
			'with ZA0307 enabled, the arrow opens its own line',
		]);

		expect(ruleOf('ZA0013').examples).toHaveLength(2);
		expect(ruleOf('ZA0903').examples).toHaveLength(2);
	});

	it('keep a comment of the example that is not a label as code', () => {
		expect(ruleOf('ZA0951').examples[0]!.before.code).toContain('//');
	});

	it('read an Unchanged example as code the rule leaves as written, with no After', () => {
		// No rule page holds one today, so the test adds one to a real page.
		const sources = withText(realSources(), 'docs/rules/ZA0052-Gather-a-run-of-loose-statements.md', (text) =>
			text.replace(/\n- /, '\n```csharp\n// Unchanged\ntotal = 0;\n\ncount = 1;\n```\n\n- '),
		);

		const unchanged = extractFromSources(sources)
			.rules.find((rule) => rule.id === 'ZA0052')!
			.examples.find((example) => example.unchanged);

		expect(unchanged?.afters).toEqual([]);
		expect(unchanged?.before.code).toBe('total = 0;\n\ncount = 1;');
	});

	it('take the markup extensions from the profile table', () => {
		expect(realCombined.markupExtensions).toEqual(expect.arrayContaining(['xml', 'csproj', 'slnx', 'axaml']));
		expect(realCombined.markupExtensions).not.toContain('resx');
	});

	it('type each option by the values its page allows', () => {
		expect(optionOf('stanza_max_line_length')).toMatchObject({ kind: 'number', min: 0, max: 1000, defaultValue: null, csharp: { audience: 'both' } });
		expect(optionOf('stanza_doc_max_overflow_columns')).toMatchObject({ kind: 'number', min: 0, max: 1000, defaultValue: '4' });
		expect(optionOf('stanza_doc_indent_size')).toMatchObject({ kind: 'number', min: 0, max: 64, defaultValue: '0', csharp: { words: ['tab'] } });

		expect(optionOf('stanza_multi_line_member_part')).toMatchObject({
			kind: 'enum',
			choices: ['body', 'signature', 'either', 'both'],
			defaultValue: 'either',
		});

		expect(optionOf('stanza_markup_enabled')).toMatchObject({
			kind: 'enum',
			choices: ['true', 'false'],
			defaultValue: 'true',
			csharp: { audience: 'markup' },
		});

		expect(optionOf('stanza_assertion_roots')).toMatchObject({
			kind: 'list',
			defaultValue: 'Assert, Expect, Should',
			csharp: { audience: 'analyzer' },
		});

		expect(optionOf('stanza_guard_roots')).toMatchObject({ kind: 'list', defaultValue: 'none', csharp: { audience: 'analyzer' } });

		expect(optionOf('stanza_named_argument_literals')).toMatchObject({
			kind: 'enum',
			choices: ['bool', 'keywords'],
			csharp: { audience: 'analyzer' },
			defaultValue: 'keywords',
		});

		expect(optionOf('stanza_assignment_targets')).toMatchObject({
			kind: 'enum',
			choices: ['mixed', 'split'],
			csharp: { audience: 'analyzer' },
			defaultValue: 'mixed',
		});

		expect(optionOf('stanza_doc_weak_line_end_words')).toMatchObject({ kind: 'list', defaultValue: 'none' });
		expect(optionOf('stanza_markup_separated_elements')).toMatchObject({ kind: 'list', defaultValue: 'none', csharp: { audience: 'both' } });

		expect(optionOf('stanza_markup_profile')).toMatchObject({
			kind: 'enum',
			defaultValue: null,
			csharp: { perFile: true, markupDefaultValue: null },
		});

		expect(optionOf('stanza_markup_profile').choices).toEqual(realCombined.markupProfiles);
		expect(optionOf('stanza_doc_max_line_length')).toMatchObject({ defaultValue: null });
	});

	it('take the profiles from the profile table', () => {
		expect(realCombined.markupProfiles).toEqual(['xml', 'msbuild', 'slnx', 'xaml']);
	});

	it('give the tool its own default only where the page says it differs', () => {
		const differing = realCombined.options.filter((option) => option.defaultValue !== option.csharp!.markupDefaultValue).map((option) => option.key);

		expect(differing).toEqual(['stanza_markup_scope']);

		expect(optionOf('stanza_markup_scope')).toMatchObject({
			defaultValue: 'all',
			csharp: { audience: 'both', markupDefaultValue: null, markupDefaultByProfile: true },
		});

		expect(realCombined.options.filter((option) => option.csharp!.markupDefaultByProfile).map((option) => option.key)).toEqual([
			'stanza_markup_scope',
		]);

		expect(optionOf('stanza_doc_max_overflow_columns')).toMatchObject({ defaultValue: '4', csharp: { markupDefaultValue: '4' } });
		expect(optionOf('stanza_markup_enabled')).toMatchObject({ defaultValue: 'true', csharp: { markupDefaultValue: 'true' } });
	});

	it('keep the text and the examples of a rule page in the order the page writes them', () => {
		const parts = ruleOf('ZA0903').parts;

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
		expect(optionOf('stanza_named_argument_min_arguments').csharp!.audience).toBe('analyzer');
		expect(optionOf('stanza_markup_doc_reprint').csharp!.audience).toBe('markup');
		expect(optionOf('stanza_markup_scope').csharp!.audience).toBe('both');
		expect(optionOf('stanza_doc_block_elements').csharp!.audience).toBe('both');
	});

	it('split an option example into labeled segments', () => {
		const segments = optionOf('stanza_named_argument_case').examples[0]!.segments;

		expect(segments.map((segment) => segment.value)).toEqual(['sensitive', 'insensitive']);
		expect(segments[0]!.isDefault).toBe(true);
		expect(segments[1]!.isDefault).toBe(false);
	});

	it('tie rules and options together from both sides', () => {
		expect(ruleOf('ZA0305').options).toEqual(expect.arrayContaining(['stanza_max_line_length', 'stanza_multi_line_member_part']));
		expect(optionOf('stanza_assertion_roots').rules).toContain('ZA0005');
		expect(optionOf('stanza_guard_roots').rules).toContain('ZA0011');
		expect(optionOf('stanza_named_argument_literals').rules).toContain('ZA0502');
		expect(optionOf('stanza_assignment_targets').rules).toContain('ZA0008');
		expect(optionOf('stanza_max_line_length').rules).toContain('ZA0305');
	});
});

const levelsPath = 'src/Nickelony.Stanza.Analyzers.Package/levels.txt';

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
					/^(\| `ZA0953` .*\n)/m,
					'$1| `ZA0900` | [Adds a blank line above a comment](https://github.com/Nickelony/Stanza/wiki/ZA0900-A-new-rule) | Suggestion, off by default | Strong |\n',
				),
		);

		const withLevel = withText(withPage, levelsPath, (text) => `${text}ZA0900 strong csharpier-off\n`);
		const added = extractFromSources(withLevel).rules.find((rule) => rule.id === 'ZA0900')!;

		expect(added).toMatchObject({
			title: 'A new rule',
			group: 'Plain comments',
			tier: 'suggestion',
			onByDefault: false,
			level: 'strong',
			csharp: { csharpier: 'off' },
		});

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
		expect(literalDefault('`max_line_length`, then `100`')).toBeNull();
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
	const rulePath = 'docs/rules/ZA0002-Separate-declaration-runs.md';
	const optionPath = 'docs/options/stanza_doc_max_overflow_columns.md';
	const readmePath = 'src/Nickelony.Stanza.Analyzers/README.md';

	it('fails the build when the heading names another rule', () => {
		failsOn(rulePath, (text) => text.replace('# ZA0002 - ', '# ZA0013 - '), /heading names ZA0013/);
	});

	it('fails the build when the tier line is missing', () => {
		failsOn(rulePath, (text) => text.replace(/\*\*Suggestion, on by default\.\*\*\n/, ''), /bold tier line/);
	});

	it('fails the build when the tier line disagrees with the readme', () => {
		failsOn(rulePath, (text) => text.replace('**Suggestion, on by default.**', '**Warning, on by default.**'), /disagrees/);
	});

	it('fails the build when the level table has no row for a rule of the readme', () => {
		failsOn(levelsPath, (text) => text.replace(/^ZA0013 .*\n/m, ''), /ZA0013 has a row in the rule table of the readme but none in the level table/);
	});

	it('fails the build when the level table has a row for a rule with no page', () => {
		failsOn(levelsPath, (text) => `${text}ZA0999 strong\n`, /ZA0999 has a row in the level table but no page/);
	});

	it('fails the build when the Level column of the readme disagrees with the level table', () => {
		failsOn(
			readmePath,
			(text) => text.replace(/^(\| `ZA0013` .*\| )Moderate( \|)$/m, '$1Strong$2'),
			/the Level column gives ZA0013 strong, but the level table gives it moderate/,
		);
	});

	it('fails the build when the readme row has no Level column', () => {
		failsOn(readmePath, (text) => text.replace(/^(\| `ZA0013` .*) \| Moderate \|$/m, '$1 |'), /another number of cells/);
	});

	it('fails the build when the Level cell of a readme row is empty', () => {
		failsOn(readmePath, (text) => text.replace(/^(\| `ZA0013` .*\| )Moderate( \|)$/m, '$1 $2'), /the level "" of ZA0013/);
	});

	it('fails the build when a rule ships on by its tier but its level does not', () => {
		const sources = withText(
			withText(realSources(), levelsPath, (text) => text.replace(/^ZA0013 moderate$/m, 'ZA0013 strong')),
			readmePath,
			(text) => text.replace(/^(\| `ZA0013` .*\| )Moderate( \|)$/m, '$1Strong$2'),
		);

		expect(() => extractFromSources(sources)).toThrowError(/levels\.txt: ZA0013 is strong there, so it ships off, but its readme row says on/);
	});

	it('reads No level in the Level column of a markup rule, which has no row in the level table', () => {
		expect(ruleOf('ZA2001')).toMatchObject({ level: 'no-level', onByDefault: false, csharp: { csharpier: null } });
		expect(ruleOf('ZA2009')).toMatchObject({ level: 'no-level', csharp: { csharpier: null, undoneByCSharpier: true } });
		expect(realCombined.rules.filter((rule) => rule.level === 'no-level').map((rule) => rule.id)).toEqual(markupRuleIds);
		expect(realSources().levels.text).not.toMatch(/^ZA20\d\d /m);
	});

	it('fails the build when the readme gives a markup rule a level', () => {
		failsOn(
			readmePath,
			(text) => text.replace(/^(\| `ZA2002` .*\| )No level( \|)$/m, '$1Opt-in$2'),
			/ZA2002 is a markup rule, which no level holds, but the Level column gives it opt-in/,
		);
	});

	it('fails the build when the level table gives a markup rule a level', () => {
		failsOn(levelsPath, (text) => `${text}ZA2002 opt-in\n`, /ZA2002 is a markup rule, which no level holds, but the level table gives it opt-in/);
	});

	it('fails the build when the readme drops the row of a markup rule', () => {
		failsOn(
			readmePath,
			(text) => text.replace(/^\| `ZA2010` .*\n/m, ''),
			/ZA2010 has no row in the rule table of the analyzer readme/,
			'docs/rules/ZA2010-Write-an-empty-markup-element-as-a-self-closing-tag.md',
		);
	});

	it('fails the build when a rule that is no markup rule says No level', () => {
		failsOn(
			readmePath,
			(text) => text.replace(/^(\| `ZA0013` .*\| )Moderate( \|)$/m, '$1No level$2'),
			/the Level column gives ZA0013 No level, which only a markup rule \(ZA2001 to ZA2010\) has/,
		);
	});

	it('fails the build when a markup rule ships on', () => {
		const sources = withText(
			withText(realSources(), 'docs/rules/ZA2002-Keep-multi-line-markup-elements-together.md', (text) =>
				text.replace('**Suggestion, off by default.**', '**Suggestion, on by default.**'),
			),

			readmePath,
			(text) => text.replace(/^(\| `ZA2002` .*\| Suggestion), off by default( \| No level \|)$/m, '$1$2'),
		);

		expect(() => extractFromSources(sources)).toThrowError(
			/ZA2002 is a markup rule, which no level holds, so it ships off, but its row says on by default/,
		);
	});

	it('reads the words of the Level column, and fails on one it does not know', () => {
		failsOn(readmePath, (text) => text.replace(/^(\| `ZA2002` .*\| )No level( \|)$/m, '$1No levels$2'), /the level "No levels" of ZA2002 is none of/);
	});

	it("reads who reads a key from its page, so the key that picks the C# literals is the analyzer's alone", () => {
		expect(optionOf('stanza_markup_literals')).toMatchObject({ defaultValue: 'marked', kind: 'enum', csharp: { audience: 'analyzer' } });
		expect(optionOf('stanza_markup_literals').choices).toEqual(['marked', 'all', 'off']);
	});

	it('fails the build when a key in the readme table says the analyzer ignores it', () => {
		failsOn(
			'docs/options/stanza_markup_literals.md',
			(text) => text.replace('stanza-markup ignores it', 'the analyzer ignores it'),
			/in the option table of the readme but its "Read by" text says the analyzer ignores it/,
		);
	});

	it('fails the build when a key outside the readme table says stanza-markup ignores it', () => {
		failsOn(
			'docs/options/stanza_markup_enabled.md',
			(text) => text.replace('Read by stanza-markup alone; the analyzer ignores it.', 'Read by nobody; stanza-markup ignores it.'),
			/its "Read by" text does not say stanza-markup reads it/,
		);
	});

	it('fails the build when the example has no Before', () => {
		failsOn(rulePath, (text) => text.replace('// Before', '// Earlier'), /holds "\/\/ Earlier" where "\/\/ Before"/);
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
		failsOn(readmePath, (text) => text.replace(/^\| `ZA0013` .*\n/m, ''), /ZA0013 has no row/, 'docs/rules/ZA0013-Separate-member-groups.md');
	});

	it('fails the build when the readme lists a rule with no page', () => {
		failsOn(
			readmePath,

			(text) =>
				text.replace(
					/^(\| `ZA0013` .*\n)/m,
					'$1| `ZA0999` | [Adds a blank line](https://github.com/Nickelony/Stanza/wiki/ZA0999-Nothing) | Suggestion | Moderate |\n',
				),

			/ZA0999/,
		);
	});

	it('fails the build when a rule page names a key that has no option page', () => {
		failsOn(
			rulePath,
			(text) => `${text.trimEnd()}\n\n- A note about \`stanza_assertion_rootz\`.\n`,
			/names stanza_assertion_rootz, which has no page/,
		);
	});

	it('fails the build when a Read by line names a rule that has no page', () => {
		failsOn(optionPath, (text) => text.replace('`ZA0901`', '`ZA0999`'), /names ZA0999, which has no page/);
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
					/^(\| \[`stanza_doc_max_overflow_columns`\].*\n)/m,
					'$1| [`stanza_nothing`](https://github.com/Nickelony/Stanza/wiki/stanza_nothing) | `1` |\n',
				),

			/stanza_nothing/,
		);
	});

	it('fails the build when an option row of the readme does not match the shape of a key row', () => {
		failsOn(
			readmePath,
			(text) => text.replace('| [`stanza_doc_max_overflow_columns`](', '| [`Stanza_doc_max_overflow_columns`]('),
			/looks like an option row/,
		);
	});

	it('fails the build when an option row of the readme has another number of cells than its header', () => {
		failsOn(readmePath, (text) => text.replace(/^(\| \[`stanza_doc_max_overflow_columns`\].*) \|$/m, '$1 | extra |'), /another number of cells/);
	});

	it('fails the build, with the line, when the first row of the pair table links one rule twice', () => {
		const page = 'docs/Which-rule-owns-which-gap.md';
		const lines = readFileSync(join(repositoryRoot, page), 'utf8').split('\n');
		const row = lines.findIndex((line) => line.startsWith('| Multi-line accessors | [ZA0101]')) + 1;

		expect(row, 'the first row of the pair table').toBeGreaterThan(0);

		failsOn(
			page,
			(text) => text.replace(/^(\| Multi-line accessors \| \[ZA0101\].*? and )\[ZA0102\]/m, '$1[ZA0101]'),
			new RegExp(`line ${row}: the "Halves" cell`),
		);
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

	it('fails the build, with the name of the file, on Markdown the renderer would print as plain text', () => {
		failsOn(rulePath, (text) => `${text.trimEnd()}\n\n## Notes\n`, /does not support/);
	});

	it.each([
		['a data address', '[x](data:text/html,abc)'],
		['a plain http address', '[x](http://example.test/)'],
	])('fails the build on a link to %s', (_what, markdown) => {
		failsOn(rulePath, (text) => `${text.trimEnd()}\n\n${markdown}\n`, /not an https/);
	});

	it.each([
		['an image', '![logo](https://example.test/logo.png)'],
		['bold with underscores', 'A __bold__ word.'],
	])('fails the build on %s, which the renderer would print as literal text', (_what, markdown) => {
		failsOn(rulePath, (text) => `${text.trimEnd()}\n\n${markdown}\n`, /does not support/);
	});

	it.each([
		['a tilde fence', '~~~csharp\n// Before\na();\n~~~'],
		['an indented fence', '  ```csharp\n  // Before\n  a();\n  ```'],
		['a fence with more than a language after the backticks', '```csharp title="x"\na();\n```'],
	])('fails the build on %s, which the splitter would not read as a code fence', (_what, markdown) => {
		failsOn(rulePath, (text) => `${text.trimEnd()}\n\n${markdown}\n`, /opens a code fence this build does not read/);
	});

	it('reads a profile name with a hyphen', () => {
		const sources = withText(realSources(), 'docs/Markup-behavior.md', (text) => text.replace('| `xml` |', '| `xml-doc` |'));
		expect(extractFromSources(sources).markupProfiles).toContain('xml-doc');
	});

	it('fails the build when a profile row claims extensions without a readable name', () => {
		failsOn('docs/Markup-behavior.md', (text) => text.replace('| `xml` |', '| xml |'), /claims extensions/);
	});

	it('fails the build when a table row has another number of cells than its header', () => {
		failsOn(rulePath, (text) => `${text.trimEnd()}\n\n| A | B |\n| --- | --- |\n| 1 | 2 | 3 |\n`, /another number of cells/);
	});

	it('names the file when a rule page is parsed alone', () => {
		expect(() => parseRulePage('docs/rules/ZA0002-Separate.md', 'no heading here', undefined)).toThrowError(/docs\/rules\/ZA0002-Separate\.md/);
		expect(() => parseOptionPage('docs/options/stanza_x.md', '# stanza_y\n')).toThrowError(/docs\/options\/stanza_x\.md/);
	});

	it('splits fences without losing the text between them', () => {
		const chunks = splitFences('x.md', 'a\n```csharp\ncode\n```\nb');
		expect(chunks.map((chunk) => chunk.kind)).toEqual(['text', 'code', 'text']);
	});

	it('names the file and the line of a fence it does not read', () => {
		expect(() => splitFences('docs/x.md', 'a\n~~~\ncode\n~~~')).toThrowError(/docs\/x\.md: line 2/);
		expect(() => splitFences('docs/x.md', 'a\n ```\ncode')).toThrowError(ExtractionError);
		expect(() => splitFences('docs/x.md', '```csharp title="x"\ncode\n```')).toThrowError(/line 1/);
	});
});
