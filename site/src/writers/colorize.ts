import type { Syntax } from '../languages/language.ts';

/** What a stretch of generated text is, which the page colors. */
export type TokenKind = 'comment' | 'section' | 'value';

export interface Token {
	text: string;
	kind: TokenKind | null;
}

/** A line of generated text: its stretches, and the kind of the whole line when one color covers it. */
export interface ColoredLine {
	kind: TokenKind | null;
	tokens: Token[];
}

const plain = (text: string): Token => ({ text, kind: null });

/** Colors a line of an `.editorconfig` or a TOML table: a comment, a section header, or a key with its value. */
function editorconfigLine(line: string): ColoredLine {
	if (line.startsWith('#')) {
		return { kind: 'comment', tokens: [plain(line)] };
	}

	if (line.startsWith('[')) {
		return { kind: 'section', tokens: [plain(line)] };
	}

	const at = line.indexOf(' = ');

	return {
		kind: null,
		tokens: at < 0 ? [plain(line)] : [plain(line.slice(0, at)), plain(' = '), { text: line.slice(at + 3), kind: 'value' }],
	};
}

const jsToken = /([/][/].*$)|('(?:[\\].|[^'\\])*')|\b(import|from|export|default)\b/g;

/** Colors a line of an `eslint.config.mjs`: its comments, its strings and the keywords of its module syntax. */
function jsLine(line: string): ColoredLine {
	const tokens: Token[] = [];
	let at = 0;

	for (const match of line.matchAll(jsToken)) {
		const kind: TokenKind = match[1] ? 'comment' : match[2] ? 'value' : 'section';

		if (match.index > at) {
			tokens.push(plain(line.slice(at, match.index)));
		}

		tokens.push({ text: match[0], kind });
		at = match.index + match[0].length;
	}

	if (at < line.length || tokens.length === 0) {
		tokens.push(plain(line.slice(at)));
	}

	return { kind: null, tokens };
}

const colorers: Partial<Record<Syntax, (line: string) => ColoredLine>> = {
	editorconfig: editorconfigLine,
	toml: editorconfigLine,
	js: jsLine,
};

/** Splits generated text into its lines and the stretches of each the page colors; the browser ships no highlighter, so this reads the text alone. */
export function colorize(syntax: Syntax, text: string): ColoredLine[] {
	const colorLine = colorers[syntax];

	// A block with no colors is one text, as the browser would show a `pre` of it.
	if (colorLine === undefined) {
		return [{ kind: null, tokens: [plain(text)] }];
	}

	return text.replace(/\n$/, '').split('\n').map(colorLine);
}
