import type { Block } from '../src/model.ts';

/** Maps a link target to the attributes of its anchor, so a page link can select a rule in place. */
export type LinkAttributes = (url: string) => string;

export function escapeHtml(text: string): string {
	return text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
}

/** Raised when a page holds Markdown the renderer does not support; the extraction adds the name of the file. */
export class MarkdownError extends Error {
	constructor(message: string) {
		super(message);
		this.name = 'MarkdownError';
	}
}

const externalLink: LinkAttributes = () => ' target="_blank" rel="noopener"';

const linkPattern = /^\[((?:[^[\]`]|`[^`]*`)*)\]\(([^)\s]+)\)/;

/** A link goes to an https address or to a path beside the page, so a `javascript:` or `data:` target never reaches the site. */
const allowedTarget = (url: string): boolean => url.startsWith('https://') || !/^(?:[a-z][a-z0-9+.-]*:|\/\/)/i.test(url);

/** Renders the inline Markdown the pages use: code, links, bold and italic. Everything else is text. */
export function renderInline(markdown: string, linkAttributes: LinkAttributes = externalLink): string {
	let html = '';
	let index = 0;

	while (index < markdown.length) {
		const rest = markdown.slice(index);
		const char = markdown[index]!;

		if (char === '`') {
			const end = markdown.indexOf('`', index + 1);

			if (end > index) {
				html += `<code>${escapeHtml(markdown.slice(index + 1, end))}</code>`;
				index = end + 1;
				continue;
			}
		} else if (char === '[') {
			const match = linkPattern.exec(rest);

			if (match) {
				const url = match[2]!;

				if (!allowedTarget(url)) {
					throw new MarkdownError(`the link target "${url}" is neither an https:// address nor a relative path`);
				}

				html += `<a href="${escapeHtml(url)}"${linkAttributes(url)}>${renderInline(match[1]!, linkAttributes)}</a>`;
				index += match[0].length;
				continue;
			}
		} else if (rest.startsWith('**')) {
			const end = markdown.indexOf('**', index + 2);

			if (end > index + 2) {
				html += `<strong>${renderInline(markdown.slice(index + 2, end), linkAttributes)}</strong>`;
				index = end + 2;
				continue;
			}
		} else if (char === '*' && rest.length > 1 && rest[1] !== ' ') {
			const end = markdown.indexOf('*', index + 1);

			if (end > index + 1 && markdown[end - 1] !== ' ') {
				html += `<em>${renderInline(markdown.slice(index + 1, end), linkAttributes)}</em>`;
				index = end + 1;
				continue;
			}
		}

		html += escapeHtml(char);
		index += 1;
	}

	return html;
}

/** Returns the text of inline Markdown without its marks. */
export function plainText(markdown: string): string {
	return markdown
		.replace(/\[((?:[^[\]`]|`[^`]*`)*)\]\([^)\s]+\)/g, '$1')
		.replace(/`([^`]*)`/g, '$1')
		.replace(/\*\*([^*]+)\*\*/g, '$1')
		.replace(/\*([^*\s][^*]*)\*/g, '$1')
		.replace(/\s+/g, ' ')
		.trim();
}

/** Splits the cells of a table row, leaving the pipes inside backticks alone. */
export function splitRow(line: string): string[] {
	const cells: string[] = [];

	let current = '';
	let inCode = false;

	const trimmed = line.trim().replace(/^\|/, '').replace(/\|$/, '');

	for (const char of trimmed) {
		if (char === '`') {
			inCode = !inCode;
		}

		if (char === '|' && !inCode) {
			cells.push(current.trim());
			current = '';
		} else {
			current += char;
		}
	}

	cells.push(current.trim());

	return cells;
}

const separatorRow = /^\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?$/;

/** A table of a page as Markdown cells, before it is rendered. */
interface RawTable {
	head: string[];
	rows: string[][];
}

export type RawBlock = { kind: 'p'; text: string } | { kind: 'ul'; items: string[] } | ({ kind: 'table' } & RawTable);

/** The Markdown the renderer would print as plain text: a heading, a numbered item, a quote, or a bullet nested or marked with another character. */
const unsupportedLine = /^(?:#|\d+[.)] |>|\s+[-*+] |[*+] )/;

/** Splits Markdown without code fences into paragraphs, bullet lists and tables, and fails on a construct it cannot render. */
export function parseBlocks(text: string): RawBlock[] {
	const blocks: RawBlock[] = [];
	const lines = text.split('\n');

	let paragraph: string[] = [];
	let list: string[] | null = null;

	const flush = (): void => {
		if (paragraph.length > 0) {
			blocks.push({ kind: 'p', text: paragraph.join(' ') });
		}

		if (list) {
			blocks.push({ kind: 'ul', items: list });
		}

		paragraph = [];
		list = null;
	};

	for (let index = 0; index < lines.length; index++) {
		const line = lines[index]!;

		if (line.trim() === '') {
			flush();
		} else if (unsupportedLine.test(line)) {
			throw new MarkdownError(`the line "${line.trim()}" is Markdown the page renderer does not support`);
		} else if (line.startsWith('|') && separatorRow.test((lines[index + 1] ?? '').trim())) {
			flush();

			const head = splitRow(line);
			const rows: string[][] = [];

			index += 2;

			while (index < lines.length && lines[index]!.startsWith('|')) {
				rows.push(splitRow(lines[index]!));
				index += 1;
			}

			index -= 1;

			if (rows.some((row) => row.length !== head.length)) {
				throw new MarkdownError(`the table headed "${head.join(' | ')}" has a row with another number of cells than its ${head.length} columns`);
			}

			blocks.push({ kind: 'table', head, rows });
		} else if (line.startsWith('- ')) {
			if (paragraph.length > 0) {
				flush();
			}

			list ??= [];
			list.push(line.slice(2).trim());
		} else if (list) {
			list[list.length - 1] += ` ${line.trim()}`;
		} else {
			paragraph.push(line.trim());
		}
	}

	flush();

	return blocks;
}

export function renderBlocks(blocks: RawBlock[], linkAttributes?: LinkAttributes): Block[] {
	return blocks.map((block): Block => {
		switch (block.kind) {
			case 'p':
				return { kind: 'p', html: renderInline(block.text, linkAttributes) };

			case 'ul':
				return { kind: 'ul', items: block.items.map((item) => renderInline(item, linkAttributes)) };

			case 'table':
				return {
					kind: 'table',
					head: block.head.map((cell) => renderInline(cell, linkAttributes)),
					rows: block.rows.map((row) => row.map((cell) => renderInline(cell, linkAttributes))),
				};
		}
	});
}
