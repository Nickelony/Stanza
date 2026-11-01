import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

import { MarkdownError } from '../render/markdown.ts';

/** The files the site reads, relative to the repository root. */
export const sourcePaths = {
	rules: 'docs/rules',
	options: 'docs/options',
	readme: 'src/Nickelony.Stanza.Analyzers/README.md',
	markupBehavior: 'docs/Markup-behavior.md',
	markupDiagnostics: 'src/Nickelony.Stanza.Markup.Core/MarkupDiagnostics.cs',
	gapOwners: 'docs/Which-rule-owns-which-gap.md',
	formatters: 'docs/Working-with-formatters.md',
	levels: 'src/Nickelony.Stanza.Analyzers.Package/levels.txt',
} as const;

/** The files the JS reader takes, relative to the repository root. */
export const jsSourcePaths = {
	rules: 'js/docs/rules',
	ruleCode: 'js/src/rules',
	readme: 'js/README.md',
	presets: 'js/src/presets.ts',
	ruleMap: 'js/docs/Rule-map.md',
	environment: 'js/src/analysis/environment.ts',
} as const;

/** The files the Python reader takes, relative to the repository root. */
export const pythonSourcePaths = {
	rules: 'py/docs/rules',
	ruleCode: 'py/src/nickelony_stanza/rules',
	readme: 'py/README.md',
	configuration: 'py/docs/Configuration.md',
	options: 'py/src/nickelony_stanza/options.py',
	config: 'py/src/nickelony_stanza/config.py',
	formatter: 'py/src/nickelony_stanza/formatter.py',
	ruleMap: 'py/docs/Rule-map.md',
} as const;

/** Raised when a page does not have the shape the site reads; the message starts with the file. */
export class ExtractionError extends Error {
	readonly file: string;

	constructor(file: string, message: string) {
		super(`${file}: ${message}`);

		this.name = 'ExtractionError';
		this.file = file;
	}
}

/** Names the file in a failure of a page: Markdown the renderer cannot print, or a shape nothing foresaw. */
export function inFile(file: string, error: unknown): ExtractionError {
	if (error instanceof ExtractionError) {
		return error;
	}

	return new ExtractionError(file, error instanceof MarkdownError ? error.message : `unexpected failure: ${String(error)}`);
}

/** Runs a read of one file, and names the file in a failure that does not already name it. */
export function withFile<T>(file: string, read: () => T): T {
	try {
		return read();
	} catch (error) {
		throw inFile(file, error);
	}
}

export interface SourceFile {
	/** The path relative to the repository root, with forward slashes. */
	file: string;
	text: string;
}

export interface ExtractionSources {
	rules: SourceFile[];
	options: SourceFile[];
	readme: SourceFile;
	markupBehavior: SourceFile;
	markupDiagnostics: SourceFile;
	gapOwners: SourceFile;
	formatters: SourceFile;
	levels: SourceFile;
}

/** Orders by key with a plain comparison of code units, so the output does not depend on the ICU data of the Node that builds it. */
export const byKey = (a: { key: string }, b: { key: string }): number => (a.key < b.key ? -1 : Number(a.key > b.key));
export const normalize = (text: string): string => text.replace(/\r\n?/g, '\n');
export const pageName = (file: string): string => file.slice(file.lastIndexOf('/') + 1).replace(/\.md$/, '');

/** Reads the files of a directory, except the names `ignored` matches, and fails on a name that `pattern` does not match. */
export function readFiles(root: string, directory: string, pattern: RegExp, what: string, ignored?: RegExp): SourceFile[] {
	const absolute = join(root, directory);
	let names: string[];

	try {
		names = readdirSync(absolute)
			.filter((name) => !ignored?.test(name))
			.sort();
	} catch {
		throw new ExtractionError(directory, `the ${what} directory cannot be read from ${absolute}`);
	}

	const unexpected = names.filter((name) => !pattern.test(name));

	if (unexpected.length > 0) {
		throw new ExtractionError(`${directory}/${unexpected[0]}`, `is not a ${what} page by its name`);
	}

	return names.map((name) => ({ file: `${directory}/${name}`, text: readFileSync(join(absolute, name), 'utf8') }));
}

/** Reads one file of the repository, and fails with its path when it cannot be read. */
export function readOne(root: string, file: string): SourceFile {
	try {
		return { file, text: readFileSync(join(root, file), 'utf8') };
	} catch {
		throw new ExtractionError(file, `cannot be read from ${join(root, file)}`);
	}
}

// --- Fenced code

export type CodeChunk = { kind: 'code'; lang: string; code: string };
export type Chunk = { kind: 'text'; text: string } | CodeChunk;

/** Splits a page into the text between code fences and the fenced blocks. */
export function splitFences(file: string, text: string): Chunk[] {
	const chunks: Chunk[] = [];
	const lines = normalize(text).split('\n');

	let prose: string[] = [];

	for (let index = 0; index < lines.length; index++) {
		const open = /^```(\S*)\s*$/.exec(lines[index]!);

		if (!open) {
			// A fence the splitter does not recognize would leave its example in the prose, where it vanishes without a word.
			if (/^\s*(?:~~~|```)/.test(lines[index]!)) {
				throw new ExtractionError(
					file,
					`line ${index + 1} opens a code fence this build does not read; use a bare "\`\`\`" in the first column, with at most a language after it`,
				);
			}

			prose.push(lines[index]!);
			continue;
		}

		const start = index + 1;
		let end = start;

		while (end < lines.length && !/^```\s*$/.test(lines[end]!)) {
			end++;
		}

		if (end >= lines.length) {
			throw new ExtractionError(file, `the code fence opened on line ${index + 1} is never closed`);
		}

		chunks.push({ kind: 'text', text: prose.join('\n') });
		chunks.push({ kind: 'code', lang: open[1]!, code: lines.slice(start, end).join('\n') });

		prose = [];
		index = end;
	}

	chunks.push({ kind: 'text', text: prose.join('\n') });

	return chunks;
}

/**
 * Splits a page at its code fences into the lines before the first one and the chunks after it.
 * The splitter opens with a text chunk, empty when the page starts at a fence, so the first line then fails the heading check of the caller.
 */
export function splitPage(file: string, text: string): { head: string[]; rest: Chunk[] } {
	const [first, ...rest] = splitFences(file, text);
	return { head: (first?.kind === 'text' ? first.text : '').split('\n'), rest };
}

/** The index of the first line from `from` on that is not blank. */
export function skipBlank(lines: readonly string[], from: number): number {
	let index = from;

	while (index < lines.length && lines[index]!.trim() === '') {
		index++;
	}

	return index;
}

/** The trimmed lines of the paragraph that starts at line `from`, which ends at the first blank line, and the index of the line that ends it. */
export function readParagraph(lines: readonly string[], from: number): { lines: string[]; end: number } {
	const paragraph: string[] = [];

	let end = from;

	for (; end < lines.length && lines[end]!.trim() !== ''; end++) {
		paragraph.push(lines[end]!.trim());
	}

	return { lines: paragraph, end };
}

interface Segment {
	label: string;
	code: string;
}

// The marker that tells an editor a raw string holds XML is part of the example, not the label of a segment.
const csharpLabel = /^\/\/ (?!language=xml$)(.+)$/;
const xmlLabel = /^<!-- (.+) -->$/;
const ruleLabelBody = '((?:Before|After|Unchanged)(?: - .+)?)$';

/** The comment that labels a segment of a rule page: `// Before` in C# and JS, and `# Before` in Python. */
const ruleLabel = (comment: string): RegExp => new RegExp(`^${comment.replace(/[/#]/g, '\\$&')} ${ruleLabelBody}`);

/**
 * Cuts a code block at each comment that opens in its first column, which labels the lines under it.
 * A rule page labels only `Before`, `After` and `Unchanged`, because any other comment there is part of the example.
 */
export function labelSegments(lang: string, code: string, rule = false, comment = '//'): Segment[] {
	const label = rule ? ruleLabel(comment) : lang === 'csharp' ? csharpLabel : lang === 'xml' ? xmlLabel : null;
	const segments: Segment[] = [];

	let current: { label: string; lines: string[] } = { label: '', lines: [] };

	const close = (): void => {
		const text = current.lines.join('\n').replace(/\s+$/, '').replace(/^\n+/, '');

		if (text !== '' || current.label !== '') {
			segments.push({ label: current.label, code: text });
		}
	};

	for (const line of code.split('\n')) {
		const match = label?.exec(line);

		if (match) {
			close();
			current = { label: match[1]!, lines: [] };
		} else {
			current.lines.push(line);
		}
	}

	close();

	return segments;
}
