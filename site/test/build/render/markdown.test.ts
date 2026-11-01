import { describe, expect, it } from 'vitest';

import { escapeHtml, findTables, MarkdownError, parseBlocks, plainText, renderInline, splitRow } from '../../../build/render/markdown.ts';

describe('the Markdown the pages use', () => {
	it('escapes HTML in text and in code', () => {
		expect(renderInline('a <b> & `<c>`')).toBe('a &lt;b&gt; &amp; <code>&lt;c&gt;</code>');
		expect(escapeHtml('"')).toBe('&quot;');
	});

	it('renders a link around code, with the attributes the caller picks', () => {
		expect(renderInline('[`ZA0002`](https://example.test/x)', () => ' data-select="ZA0002"')).toBe(
			'<a href="https://example.test/x" data-select="ZA0002"><code>ZA0002</code></a>',
		);
	});

	it('renders bold and italic and leaves a lone star alone', () => {
		expect(renderInline('**a** and *b* and 2 * 3')).toBe('<strong>a</strong> and <em>b</em> and 2 * 3');
	});

	it('links only to an https address', () => {
		expect(renderInline('[b](https://example.test/)')).toContain('<a href="https://example.test/"');

		for (const target of ['javascript:run', 'HTTP://example.test/', '\\\\evil.example/x', '//evil.example/x', 'Terms.md', '#notes']) {
			expect(() => renderInline(`[a](${target})`), target).toThrowError(/not an https/);
		}
	});

	it('refuses an image and bold with underscores, which would print as literal text', () => {
		expect(() => renderInline('see ![logo](https://example.test/a.png)')).toThrowError(/image/);
		expect(() => renderInline('a __bold__ word')).toThrowError(/underscores/);
		expect(renderInline('a snake_case_name and __init')).toBe('a snake_case_name and __init');
		expect(renderInline('an exclamation! [a](https://example.test/)')).toContain('<a href');
	});

	it('refuses a link with a title or a parenthesis in its address, which would print half as text', () => {
		expect(() => renderInline('see [a](https://example.test/ "title")')).toThrowError(/title or a parenthesis/);
		expect(() => renderInline('see [a](https://example.test/a_(b))')).toThrowError(/parenthesis/);
		expect(renderInline('a [bracket] and (a paren)')).toBe('a [bracket] and (a paren)');
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

	it('finds the tables among other lines, each with the index of its header', () => {
		const lines = ['Text', '| A |', '| --- |', '| 1 |', '', '- item', '| B | C |', '| --- | --- |', '| 2 | 3 |', '| 4 | 5 |'];

		expect(findTables(lines)).toEqual([
			{ head: ['A'], rows: [['1']], at: 1 },

			{
				head: ['B', 'C'],

				rows: [
					['2', '3'],
					['4', '5'],
				],

				at: 6,
			},
		]);

		expect(findTables(lines, 4, 6)).toEqual([]);
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
