import { describe, expect, it } from 'vitest';
import { changedCount, checkOption, decodeHash, effectiveState, emptyConfig, encodeHash, isRuleChanged } from '../src/config.ts';
import { diffLines } from '../src/diff.ts';
import { MarkdownError, escapeHtml, parseBlocks, plainText, renderInline, splitRow } from '../build/markdown.ts';
import { catalog, option, rule } from './fixtures.ts';

const sample = catalog(
	[rule('ZA0001'), rule('ZA0003', { tier: 'suggestion', onByDefault: false })],
	[
		option('stanza_max_line_length'),
		option('stanza_multiline_body_trigger', { kind: 'enum', choices: ['body', 'either'], defaultValue: 'either', min: null, max: null }),
		option('stanza_doc_indent_size', { min: 0, max: 64, words: ['tab'], defaultValue: '0' }),
		option('stanza_assertion_roots', { kind: 'list', min: null, max: null, defaultValue: null }),
	],
);

describe('a rule setting', () => {
	it('changes nothing when a rule that is on by default is set to its own severity', () => {
		const zero = sample.rules[0]!;

		expect(isRuleChanged(zero, 'default')).toBe(false);
		expect(isRuleChanged(zero, 'warning')).toBe(false);
		expect(isRuleChanged(zero, 'error')).toBe(true);
		expect(isRuleChanged(zero, 'none')).toBe(true);
	});

	it('changes the rule that is off by default whatever severity enables it', () => {
		const three = sample.rules[1]!;

		expect(isRuleChanged(three, 'suggestion')).toBe(true);
		expect(effectiveState(three, 'default')).toBe('off');
		expect(effectiveState(three, 'silent')).toBe('silent');
		expect(effectiveState(sample.rules[0]!, 'default')).toBe('warning');
	});

	it('counts what differs from the defaults', () => {
		const config = {
			...emptyConfig(),
			rules: { ZA0001: 'warning' as const, ZA0003: 'warning' as const },
			options: { stanza_max_line_length: '100', stanza_doc_indent_size: '2' },
		};

		expect(changedCount(sample, config)).toBe(2);
	});
});

describe('an option value', () => {
	it('leaves an empty input at the default', () => {
		expect(checkOption(sample.options[0]!, '  ')).toEqual({ ok: true, value: null });
	});

	it('accepts a whole number inside the bounds and normalizes it', () => {
		expect(checkOption(sample.options[0]!, '120')).toEqual({ ok: true, value: '120' });
		expect(checkOption(sample.options[0]!, '007')).toEqual({ ok: true, value: '7' });
		expect(checkOption(sample.options[0]!, '1000')).toEqual({ ok: true, value: '1000' });
	});

	it('refuses a number outside the bounds, a decimal, and text', () => {
		for (const raw of ['1001', '-1', '1.5', 'wide']) {
			expect(checkOption(sample.options[0]!, raw).ok, raw).toBe(false);
		}
	});

	it('accepts a word the page allows beside the numbers', () => {
		expect(checkOption(sample.options[2]!, 'tab')).toEqual({ ok: true, value: 'tab' });
		expect(checkOption(sample.options[2]!, 'space').ok).toBe(false);
	});

	it('accepts only the values of an enum', () => {
		expect(checkOption(sample.options[1]!, 'body')).toEqual({ ok: true, value: 'body' });
		expect(checkOption(sample.options[1]!, 'Body').ok).toBe(false);
	});

	it('trims a list, drops empty entries, and refuses a character that ends an .editorconfig value', () => {
		expect(checkOption(sample.options[3]!, ' Verify ,, Assert ')).toEqual({ ok: true, value: 'Verify, Assert' });
		expect(checkOption(sample.options[3]!, 'Verify # note').ok).toBe(false);
		expect(checkOption(sample.options[3]!, 'a;b').ok).toBe(false);
	});

	it('refuses a tab, a line separator and any other control character inside a value', () => {
		const controls = [0x09, 0x0b, 0x1b, 0x7f, 0x85, 0x2028, 0x2029].map((code) => `a${String.fromCharCode(code)}b`);

		for (const raw of controls) {
			for (const target of [sample.options[1]!, sample.options[3]!]) {
				expect(checkOption(target, raw).ok, `${target.key} ${raw.charCodeAt(1)}`).toBe(false);
			}
		}

		expect(checkOption(sample.options[3]!, 'a b').ok).toBe(true);
	});
});

describe('the address hash', () => {
	it('round-trips a configuration and a selection', () => {
		const config = {
			...emptyConfig(),
			everySetting: true,
			markupSection: false,
			rules: { ZA0003: 'warning' as const, ZA0001: 'none' as const },
			options: { stanza_assertion_roots: 'Verify, Assert' },
		};

		const hash = encodeHash(sample, { config, selection: { kind: 'rule', id: 'ZA0003' } });

		expect(hash).toBe('ZA0001=n&ZA0003=w&stanza_assertion_roots=Verify%2C+Assert&all=1&markup=0&sel=ZA0003');
		expect(decodeHash(sample, `#${hash}`)).toEqual({ config, selection: { kind: 'rule', id: 'ZA0003' } });
	});

	it('writes nothing for a configuration at its defaults', () => {
		expect(encodeHash(sample, { config: emptyConfig(), selection: null })).toBe('');
	});

	it('drops a key the catalog does not know, a letter that is no severity, and a value the page does not allow', () => {
		const state = decodeHash(sample, '#ZA9999=w&ZA0001=x&ZA0003=s&stanza_max_line_length=wide&stanza_multiline_body_trigger=body&sel=ZA9999');

		expect(state.config.rules).toEqual({ ZA0003: 'suggestion' });
		expect(state.config.options).toEqual({ stanza_multiline_body_trigger: 'body' });
		expect(state.selection).toBeNull();
	});

	it('selects an option by its key', () => {
		expect(decodeHash(sample, '#sel=stanza_max_line_length').selection).toEqual({ kind: 'option', id: 'stanza_max_line_length' });
	});
});

describe('the line diff', () => {
	it('marks an added blank line on the After side only', () => {
		const diff = diffLines(['a', 'b', 'c'], ['a', '', 'b', 'c']);

		expect([...diff.added]).toEqual([1]);
		expect([...diff.removed]).toEqual([]);
	});

	it('marks a removed blank line on the Before side only', () => {
		const diff = diffLines(['a', '', 'b'], ['a', 'b']);

		expect([...diff.removed]).toEqual([1]);
		expect([...diff.added]).toEqual([]);
	});

	it('marks a rewritten line on both sides', () => {
		const diff = diffLines(['a', 'b => c;', 'd'], ['a', 'b =>', 'c;', 'd']);

		expect([...diff.removed]).toEqual([1]);
		expect([...diff.added]).toEqual([1, 2]);
	});

	it('puts the added blank line where it was inserted when blank lines already stand nearby', () => {
		const diff = diffLines(['a', 'b', '', 'c'], ['a', '', 'b', '', 'c']);

		expect([...diff.added]).toEqual([1]);
	});

	it('ignores trailing whitespace', () => {
		expect(diffLines(['a  '], ['a']).added.size).toBe(0);
	});
});

describe('the Markdown the pages use', () => {
	it('escapes HTML in text and in code', () => {
		expect(renderInline('a <b> & `<c>`')).toBe('a &lt;b&gt; &amp; <code>&lt;c&gt;</code>');
		expect(escapeHtml('"')).toBe('&quot;');
	});

	it('renders a link around code, with the attributes the caller picks', () => {
		expect(renderInline('[`ZA0001`](https://example.test/x)', () => ' data-select="ZA0001"')).toBe(
			'<a href="https://example.test/x" data-select="ZA0001"><code>ZA0001</code></a>',
		);
	});

	it('renders bold and italic and leaves a lone star alone', () => {
		expect(renderInline('**a** and *b* and 2 * 3')).toBe('<strong>a</strong> and <em>b</em> and 2 * 3');
	});

	it('links only to an https address or a relative path', () => {
		expect(renderInline('[a](Terms.md) and [b](https://example.test/)')).toContain('<a href="Terms.md"');
		expect(() => renderInline('[a](javascript:run)')).toThrowError(MarkdownError);
		expect(() => renderInline('[a](HTTP://example.test/)')).toThrowError(/neither an https/);
	});

	it('refuses Markdown the renderer would print as plain text', () => {
		for (const line of ['# Title', '1. one', '> quoted', '  - nested', '* star']) {
			expect(() => parseBlocks(line), line).toThrowError(MarkdownError);
		}

		expect(parseBlocks('A line with a - dash\nand 1. inside it.')).toEqual([{ kind: 'p', text: 'A line with a - dash and 1. inside it.' }]);
	});

	it('refuses a table row with another number of cells than the header', () => {
		expect(() => parseBlocks(['| A | B |', '| --- | --- |', '| 1 |'].join('\n'))).toThrowError(MarkdownError);
	});

	it('reduces a line to its words', () => {
		expect(plainText('Adds a [blank `line`](https://example.test) and **more**')).toBe('Adds a blank line and more');
	});

	it('splits a table row without cutting a pipe inside code', () => {
		expect(splitRow('| `a|b` | c |')).toEqual(['`a|b`', 'c']);
	});

	it('reads paragraphs, lists with wrapped items, and tables', () => {
		const blocks = parseBlocks(
			['A sentence', 'that wraps.', '', '- one', '  wrapped', '- two', '', '| A | B |', '| --- | --- |', '| 1 | 2 |'].join('\n'),
		);

		expect(blocks).toEqual([
			{ kind: 'p', text: 'A sentence that wraps.' },
			{ kind: 'ul', items: ['one wrapped', 'two'] },
			{ kind: 'table', head: ['A', 'B'], rows: [['1', '2']] },
		]);
	});
});
