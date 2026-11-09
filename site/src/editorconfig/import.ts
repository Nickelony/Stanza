import { checkOption, differsFromDefaultIn, isRuleChanged, isRuleChangedIn, readsOption, severities, type Config } from '../state/config.ts';
import type { Catalog, Option, Severity } from '../state/model.ts';
import { counted } from '../util/counted.ts';

/** The longest file the page reads, in characters; an `.editorconfig` that matters is far shorter. */
export const maxImportLength = 1 << 20;

/** How many skipped notes the report lists; a file of bogus keys would otherwise draw a list item for each. */
export const maxListedNotes = 200;

/** The longest stretch of a line a note quotes. */
const maxQuoted = 120;

/** A line, or a section, the import did not apply. */
export interface ImportNote {
	/** The line of the file, counted from 1. */
	line: number;

	/** The line as written, cut to a length a list can show. */
	text: string;
	reason: string;
}

export interface ImportResult {
	/** The configuration with the file applied; the configuration passed in, untouched, when the file was refused. */
	config: Config;

	/** The rules the file sets to something other than the default, and the options it sets to something other than the default. */
	rules: number;
	options: number;

	/** The Stanza lines the import left out. */
	skipped: ImportNote[];

	/** The lines of other settings, such as `indent_style`, which the page has no use for and does not list. */
	ignored: number;

	/** Why the file was not read at all, or `null`. */
	refused: string | null;
}

type Scope = 'analyzer' | 'markup' | 'other';

/** The scopes whose settings the page reads. */
type ReadScope = Exclude<Scope, 'other'>;

interface Assignment {
	value: string;
	note: ImportNote;
}

/** What each scope assigned, by rule ID or option key. */
type ByScope = Record<ReadScope, Map<string, Assignment>>;

interface Collected {
	rules: ByScope;
	options: ByScope;
}

/** What a severity word of a rule line is stored as: Roslyn reads `refactoring` as another name for `silent`, which is how the page shows it. */
const severityAliases: ReadonlyMap<string, string> = new Map([['refactoring', 'silent']]);
const severityWords: ReadonlySet<string> = new Set([...severities, ...severityAliases.keys(), 'default']);

/** Cuts the text at its first `#` or `;`, which a section header may escape with a backslash, as the C# reader does. */
function stripComment(text: string, honorsEscapes: boolean): string {
	for (let offset = 0; offset < text.length; offset++) {
		if (honorsEscapes && text[offset] === '\\') {
			offset++;
		} else if (text[offset] === '#' || text[offset] === ';') {
			return text.slice(0, offset).trim();
		}
	}

	return text.trim();
}

/** The key ends at the first `=` or `:`, whichever comes first, as it does in `stanza-markup`; the result is -1 when there is neither. */
function findSeparator(line: string): number {
	const equals = line.indexOf('=');
	const colon = line.indexOf(':');

	if (equals < 0) {
		return colon;
	}

	return colon < 0 ? equals : Math.min(equals, colon);
}

/** The value of a line cut of its trailing comment and, on a `stanza_` or `csharp_` key, of its `:severity` suffix. */
function stripCommentAndSeverity(key: string, raw: string): string {
	const value = stripComment(raw, false);

	if (key.startsWith('stanza_') || key.startsWith('csharp_')) {
		const colon = value.indexOf(':');
		return colon < 0 ? value : value.slice(0, colon).trimEnd();
	}

	return value;
}

/** Splits the first `{a,b}` of a glob into its alternatives, and does the same for the braces left in each. */
function expandBraces(glob: string, budget = { left: 64 }): string[] {
	const open = glob.indexOf('{');

	if (open < 0 || budget.left <= 0) {
		return [glob];
	}

	let depth = 0;
	let close = -1;

	const commas: number[] = [];

	for (let index = open; index < glob.length; index++) {
		const character = glob[index];

		if (character === '{') {
			depth++;
		} else if (character === '}') {
			depth--;

			if (depth === 0) {
				close = index;
				break;
			}
		} else if (character === ',' && depth === 1) {
			commas.push(index);
		}
	}

	// A brace with no comma, or none that closes, is a literal as far as this reader goes.
	if (close < 0 || commas.length === 0) {
		return [glob];
	}

	const bounds = [open, ...commas, close];
	const results: string[] = [];

	for (let part = 0; part < bounds.length - 1; part++) {
		budget.left--;
		results.push(...expandBraces(glob.slice(0, open) + glob.slice(bounds[part]! + 1, bounds[part + 1]).trim() + glob.slice(close + 1), budget));
	}

	return results;
}

/** Whether the glob of a section matches every C# file, the markup files of the profiles, or neither. */
function scopeOf(header: string, markupExtensions: readonly string[]): Scope {
	let markup = false;

	for (const alternative of expandBraces(header.trim().toLowerCase())) {
		const glob = alternative.trim();
		const match = /^(?:\*\*\/)?\*{1,2}(?:\.([a-z0-9]+))?$/.exec(glob);

		if (!match) {
			continue;
		}

		if (match[1] === undefined || match[1] === 'cs') {
			return 'analyzer';
		}

		markup ||= markupExtensions.includes(match[1]);
	}

	return markup ? 'markup' : 'other';
}

function quoted(line: string): string {
	return line.length > maxQuoted ? `${line.slice(0, maxQuoted)}...` : line;
}

/** Why a file of that many characters or bytes is not read, or `null` when its size is fine. */
export function sizeRefusal(size: number): string | null {
	return size > maxImportLength ? `The file is larger than ${maxImportLength / 1024} KB, which is more than an .editorconfig holds.` : null;
}

/** The result for a file that was not read at all: the configuration stays as it was. */
export function refusal(config: Config, refused: string): ImportResult {
	return { config, rules: 0, options: 0, skipped: [], ignored: 0, refused };
}

/** Reads the value of an option line the way its page allows; an enum is matched without regard to case, as an `.editorconfig` reads values. */
function checkImported<S>(option: Option<S>, value: string): ReturnType<typeof checkOption> {
	const checked = checkOption(option, value);

	if (checked.ok || option.kind !== 'enum') {
		return checked;
	}

	const lower = value.toLowerCase();
	const match = option.choices.find((choice) => choice.toLowerCase() === lower);

	return match === undefined ? checked : { ok: true, value: match };
}

/** What the lines of a file give before the values are resolved: the assignments by scope, the lines left out and the sections seen. */
interface Reading {
	collected: Collected;
	skipped: ImportNote[];
	ignored: number;
	analyzerSeen: boolean;
	markupSeen: boolean;
}

/** Why a text is not read at all, or `null` when it is. */
function refusalOf(text: unknown): string | null {
	if (typeof text !== 'string') {
		return 'The file could not be read as text.';
	}

	return sizeRefusal(text.length) ?? (text.includes('\0') ? 'The file is not text: it holds a NUL character.' : null);
}

const byteOrderMark = String.fromCharCode(0xfeff);

/** Splits the text into its lines at a CRLF, a CR or an LF, as the readers do, after the byte order mark a file may open with. */
function readLines(text: string): string[] {
	return (text.startsWith(byteOrderMark) ? text.slice(1) : text).split(/\r\n|\r|\n/);
}

/** The reason a line is left out when the page has no such rule or key: the page that holds it, or that this one does not. */
function missingReason<S>(catalog: Catalog<S>, kind: 'rule' | 'option', name: string): string {
	const { elsewhere } = catalog;
	const moved = kind === 'rule' ? elsewhere?.rules : elsewhere?.options;

	if (elsewhere && moved?.includes(name)) {
		return `${name} belongs to the ${elsewhere.page} page; import the file there.`;
	}

	return kind === 'rule' ? `The page has no rule ${name}.` : `The page has no option ${quoted(name)}.`;
}

/** Notes the rule line, or collects its severity under the scope that read it. */
function collectRule<S>(
	catalog: Catalog<S>,
	reading: Reading,
	scope: ReadScope,
	ruleId: string,
	value: string,
	note: (reason: string) => ImportNote,
): void {
	if (!catalog.rules.some((rule) => rule.id === ruleId)) {
		reading.skipped.push(note(missingReason(catalog, 'rule', ruleId)));
	} else if (!severityWords.has(value.toLowerCase())) {
		reading.skipped.push(note(`"${quoted(value)}" is not a severity; use ${[...severityWords].join(', ')}.`));
	} else {
		const severity = value.toLowerCase();
		reading.collected.rules[scope].set(ruleId, { value: severityAliases.get(severity) ?? severity, note: note('') });
	}
}

/** Notes the option line, or collects its value under the scope that read it. */
function collectOption<S>(
	catalog: Catalog<S>,
	reading: Reading,
	scope: ReadScope,
	key: string,
	value: string,
	note: (reason: string) => ImportNote,
): void {
	const option = catalog.options.find((candidate) => candidate.key === key);

	if (!option) {
		reading.skipped.push(note(missingReason(catalog, 'option', key)));
		return;
	}

	const checked = checkImported(option, value);

	if (!checked.ok) {
		reading.skipped.push(note(checked.reason));
	} else if (checked.value === null) {
		reading.skipped.push(note('The value is empty.'));
	} else {
		reading.collected.options[scope].set(option.key, { value: checked.value, note: note('') });
	}
}

/** Walks the lines once, keeping the scope of the section it is in, and collects every Stanza setting a section reads. */
function collectLines<S>(catalog: Catalog<S>, lines: readonly string[]): Reading {
	const reading: Reading = {
		collected: {
			rules: { analyzer: new Map(), markup: new Map() },
			options: { analyzer: new Map(), markup: new Map() },
		},

		skipped: [],
		ignored: 0,
		analyzerSeen: false,
		markupSeen: false,
	};

	let scope: Scope = 'analyzer';
	let header: ImportNote | null = null;
	let unread = 0;

	const closeSection = (): void => {
		if (header !== null && unread > 0) {
			reading.skipped.push({
				...header,
				reason: `Not a section for all C# files or the markup files, so its ${counted(unread, 'Stanza line')} ${unread === 1 ? 'is' : 'are'} not read.`,
			});
		}

		header = null;
		unread = 0;
	};

	lines.forEach((raw, index) => {
		const line = raw.trim();
		const note = (reason: string): ImportNote => ({ line: index + 1, text: quoted(line), reason });

		if (line === '' || line.startsWith('#') || line.startsWith(';')) {
			return;
		}

		if (line.startsWith('[')) {
			const bracketed = stripComment(line, true);

			if (!bracketed.endsWith(']') || bracketed.length < 3) {
				reading.skipped.push(note('Not a section header: it needs a name between [ and ].'));
				return;
			}

			closeSection();
			scope = scopeOf(bracketed.slice(1, -1), catalog.markupExtensions);

			if (scope === 'other') {
				header = note('');
			}

			return;
		}

		const at = findSeparator(line);
		const key = (at < 0 ? line : line.slice(0, at)).trim().toLowerCase();
		const value = at < 0 ? '' : stripCommentAndSeverity(key, line.slice(at + 1));
		const ruleId = /^dotnet_diagnostic\.(za\d+)\.severity$/.exec(key)?.[1]?.toUpperCase();
		const stanza = ruleId !== undefined || key.startsWith('stanza_');

		if (at < 0 || key === '') {
			if (scope === 'other') {
				reading.ignored++;
			} else {
				reading.skipped.push(note('Not a setting: it needs a key, an equals sign or a colon, and a value.'));
			}

			return;
		}

		if (!stanza) {
			reading.ignored++;
			return;
		}

		if (scope === 'other') {
			unread++;
			return;
		}

		if (scope === 'analyzer') {
			reading.analyzerSeen = true;
		} else {
			reading.markupSeen = true;
		}

		if (ruleId !== undefined) {
			collectRule(catalog, reading, scope, ruleId, value, note);
		} else {
			collectOption(catalog, reading, scope, key, value, note);
		}
	});

	closeSection();

	return reading;
}

/** Picks the value of a key from the C# section, else from the markup section, and notes a markup value that disagrees. */
function resolve(by: ByScope, key: string, skipped: ImportNote[]): { value: string; from: ReadScope } | undefined {
	const analyzer = by.analyzer.get(key);
	const markup = by.markup.get(key);

	if (analyzer && markup && analyzer.value !== markup.value) {
		skipped.push({
			...markup.note,
			reason: `The value of ${key} in the markup section (${markup.value}) differs from the one in the C# section (${analyzer.value}); the page keeps one value for both, the C# one.`,
		});
	}

	if (analyzer) {
		return { value: analyzer.value, from: 'analyzer' };
	}

	return markup ? { value: markup.value, from: 'markup' } : undefined;
}

/**
 * Whether a value differs from the default of the tool whose section gave it.
 * The page writes the analyzer default in the C# section where the tool has none, as `stanza_markup_scope = all`, so that line is no change.
 * A key only `stanza-markup` reads is held against that tool wherever it stands.
 */
function isImportedOptionChanged<S>(option: Option<S>, value: string, from: ReadScope): boolean {
	return differsFromDefaultIn(option, value, from === 'markup' || !readsOption(option, 'analyzer') ? 'markup' : 'analyzer');
}

/** Settles each rule and option from the scopes that gave it, and builds the result the page shows. */
function resolveReading<S>(catalog: Catalog<S>, reading: Reading, current: Config): ImportResult {
	const { collected, skipped } = reading;
	const rules: Record<string, Severity> = {};
	const options: Record<string, string> = {};

	for (const rule of catalog.rules) {
		const value = resolve(collected.rules, rule.id, skipped);

		if (value === undefined || value.value === 'default') {
			continue;
		}

		// A line that says what the level gives is no change, so a file the page wrote comes back as none.
		// A markup line alone says nothing about the analyzer, so it is held against the markup state of the level only.
		const trial: Config = { ...current, rules: { [rule.id]: value.value as Severity } };
		const changed = value.from === 'markup' ? isRuleChangedIn(rule, trial, 'markup') : isRuleChanged(rule, trial);

		if (changed) {
			rules[rule.id] = value.value as Severity;
		}
	}

	for (const option of catalog.options) {
		const value = resolve(collected.options, option.key, skipped);

		if (value !== undefined && isImportedOptionChanged(option, value.value, value.from)) {
			options[option.key] = value.value;
		}
	}

	skipped.sort((a, b) => a.line - b.line);

	const markupSection = reading.markupSeen ? true : reading.analyzerSeen ? false : current.markupSection;

	return {
		config: { ...current, rules, options, markupSection },
		rules: Object.keys(rules).length,
		options: Object.keys(options).length,
		skipped,
		ignored: reading.ignored,
		refused: null,
	};
}

/**
 * Reads the text of an `.editorconfig` and applies the Stanza settings in it to a copy of the configuration.
 * It never throws: text it cannot read as an `.editorconfig` is refused whole, and a line it cannot apply is listed in `skipped`.
 * A section that matches C# files gives the analyzer values, and one that matches the markup files the `stanza-markup` values;
 * the page keeps one value for both, so the C# one wins where they differ.
 */
export function importEditorConfig<S>(catalog: Catalog<S>, text: string, current: Config): ImportResult {
	const refused = refusalOf(text);
	return refused === null ? resolveReading(catalog, collectLines(catalog, readLines(text)), current) : refusal(current, refused);
}

/** The notes the report lists, and how many more the cap left out. */
export function listedNotes(skipped: readonly ImportNote[]): { shown: ImportNote[]; more: number } {
	return { shown: skipped.slice(0, maxListedNotes), more: Math.max(0, skipped.length - maxListedNotes) };
}

/** The sentence that tells the visitor what an import did, for the report beside the output. */
export function summarize(result: ImportResult, name: string): string {
	if (result.refused !== null) {
		return `Nothing was imported from ${name}. ${result.refused}`;
	}

	const read = `Imported ${counted(result.rules, 'rule setting')} and ${counted(result.options, 'option')} from ${name}.`;
	const listed = result.skipped.length > maxListedNotes ? `the first ${maxListedNotes} listed below` : 'listed below';
	const skipped = result.skipped.length > 0 ? ` ${counted(result.skipped.length, 'line')} skipped, ${listed}.` : ' No Stanza line was skipped.';

	const ignored =
		result.ignored > 0
			? ` ${counted(result.ignored, 'line')} of other settings ${result.ignored === 1 ? 'was' : 'were'} not Stanza's and left alone.`
			: '';

	return read + skipped + ignored;
}
