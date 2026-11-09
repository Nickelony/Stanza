/** The four tiers a rule page names in its tier line. */
export type Tier = 'error' | 'warning' | 'suggestion' | 'silent';

/** The severities an `.editorconfig` can give a rule: the tiers and `none`, which disables it. */
export type Severity = Tier | 'none';

/** What the form sets for a rule; `default` stands for no line at all. */
export type RuleSetting = Severity | 'default';

/**
 * The first level that enables a rule, `opt-in` for a rule that no level enables, or `no-level` for a markup rule,
 * which no level holds and which only its own severity line enables.
 */
export type RuleLevel = 'minimal' | 'moderate' | 'strong' | 'opt-in' | 'no-level';

/** What the `csharpier` formatter does to a rule at the strong level: turns it off, or turns on the tight half of its pair. */
export type CSharpierMark = 'off' | 'on';

/** A fenced code block of a page, before highlighting. */
export interface SourceSnippet {
	lang: string;
	code: string;
}

/** One line of a highlighted snippet as markup, in the compact form of `compact.ts` once shipped; `lineText` reads the plain text the diff compares. */
export interface SnippetLine {
	html: string;
}

export interface Snippet {
	lines: SnippetLine[];
}

export interface Variant<S> {
	/** The text after `After - ` in the label, empty when the label has none. */
	label: string;
	snippet: S;
}

/**
 * A `// Before` segment with the `// After` segments that follow it in the same code block,
 * or an `// Unchanged` segment, which shows code the rule leaves as written and has no After.
 */
export interface RuleExample<S> {
	beforeLabel: string;
	before: S;
	afters: Variant<S>[];
	unchanged: boolean;
}

/** A paragraph, a bullet list or a table of a page, as HTML. */
export type Block = { kind: 'p'; html: string } | { kind: 'ul'; items: string[] } | { kind: 'table'; head: string[]; rows: string[][] };

/** Marks where an example stands among the text of a page, by its index in the `examples` of the rule or option. */
export interface ExampleRef {
	kind: 'example';
	index: number;
}

/** What a rule page holds after its description, in the order the page writes it. */
export type RulePart = Block | ExampleRef;

/** What an option page holds after its opening paragraph; `values` marks the value table. */
export type OptionPart = Block | ExampleRef | { kind: 'values' };

/** What a C# rule has that a rule of another language lacks: how CSharpier and `stanza-markup` treat it. */
export interface CSharpRule {
	/** What the `csharpier` formatter does to the rule at the strong level, from the level table, or `null` when it does nothing. */
	csharpier: CSharpierMark | null;

	/** Whether `stanza-markup` applies the rule with no `.editorconfig` line; the tool never reads the level. */
	markupOnByDefault: boolean;

	/** Whether `stanza-markup` reads the rule's severity, as `MarkupDiagnostics` declares it. */
	readByMarkup: boolean;

	/** Whether the formatter CSharpier undoes the layout the rule writes, as the formatters page lists it. */
	undoneByCSharpier: boolean;
}

export interface Rule<S> {
	id: string;
	title: string;

	/** The README `###` heading of the table that lists the rule. */
	group: string;

	/** The gap text of the README row, on one line, as plain text. */
	summary: string;

	/** The severity the rule ships at, as its tier line says. A level reports its rules at the severity the visitor picks instead. */
	tier: Tier;

	/** Whether the rule ships on, so the analyzer reports it with no property and no `.editorconfig` line; the level table agrees with the tier line. */
	onByDefault: boolean;

	/** The first level that enables the rule, from the level table; `no-level` for a markup rule, which the level table does not list. */
	level: RuleLevel;

	/** The facts only the C# and Markup pages hold; absent for a rule of another language. */
	csharp?: CSharpRule;

	/** The tier line of the page, as written. */
	tierText: string;
	descriptionHtml: string;
	examples: RuleExample<S>[];
	parts: RulePart[];

	/** The `optionId` of each option the page names. */
	options: string[];

	/** The other half of the pair the rule belongs to, where the tool takes a rule the level enables out of it once a file names this half; absent elsewhere. */
	opposite?: string;

	/** Where the full page of the rule lives: the wiki for C#, GitHub for the other languages. */
	pageUrl: string;
}

export type OptionKind = 'enum' | 'number' | 'list';

export interface OptionValue {
	/** The cell as written, as HTML. */
	valueHtml: string;
	effectHtml: string;

	/** The literals the cell names between backticks. */
	literals: string[];
}

export interface OptionSegment<S> {
	/** The comment label of the segment, empty for a block with no label. */
	label: string;

	/** The value the label opens with, when it names one. */
	value: string;
	isDefault: boolean;
	snippet: S;
}

export interface OptionExample<S> {
	segments: OptionSegment<S>[];
}

export type Audience = 'analyzer' | 'markup' | 'both';

/** What a C# key has that a key of another language lacks: how `stanza-markup` reads it, and the words and the table its page gives. */
export interface CSharpOption {
	/** The default for `stanza-markup`, which is `null` when the tool derives its default, as from the profile of the file. */
	markupDefaultValue: string | null;

	/** Whether the page says the tool's default is the profile's own. */
	markupDefaultByProfile: boolean;

	/** Who reads the key: the analyzer (it is in the README table), `stanza-markup`, or both. */
	audience: Audience;

	/** Whether the key picks a profile per file, so a line under a glob gives every file the glob matches that profile. */
	perFile: boolean;

	/** The words a `number` takes besides digits. */
	words: string[];

	/** The rows of the value table of the page. */
	values: OptionValue[];
}

export interface Option<S> {
	key: string;
	descriptionHtml: string;

	/** The `Default:` line of the page as plain text. */
	defaultText: string;

	/** The value the analyzer uses with no line, when the docs give one; otherwise `null`. */
	defaultValue: string | null;
	readByHtml: string;
	kind: OptionKind;

	/** The literals of an `enum`, or the words each item of a `list` takes; empty for a list of free text. */
	choices: string[];

	/** The bounds of a `number`. */
	min: number | null;
	max: number | null;
	examples: OptionExample<S>[];
	parts: OptionPart[];

	/** The IDs of the rules the page names or that name the key. */
	rules: string[];

	/** Where the full page of the key lives, or the page that documents it when the key has none of its own. */
	pageUrl: string;

	/** Whether the key belongs to no one rule, such as a key of `settings.stanza` or of a Python table; absent for a key a rule takes. */
	global?: boolean;

	/** The rule whose entry takes the key as its own option, so another rule may take the same key; absent for a key that no one rule owns. */
	scope?: string;

	/** The facts only the C# and Markup pages hold; absent for a key of another language, which the analyzer's reader alone reads. */
	csharp?: CSharpOption;
}

/** Two rules that judge one gap or one construct in opposite ways, as the pair table of the Which rule owns which gap page lists them. */
export interface RulePair {
	first: string;
	second: string;

	/** The half that owns the gap or construct while a file enables neither. */
	defaultHalf: string;

	/** The half that owns the gap or construct when a file enables both. */
	ownerWhenBothEnabled: string;
}

/** Where the rules and keys that a page lacks went, so an import can send the visitor to the page that holds a line. */
export interface Elsewhere {
	/** The name of the page, as the language switch shows it. */
	page: string;
	rules: string[];
	options: string[];
}

export interface Catalog<S> {
	rules: Rule<S>[];
	options: Option<S>[];
	pairs: RulePair[];

	/** The README `###` headings of the rule tables, in order. */
	groups: string[];

	/** The file extensions the markup profiles claim, without the dot. */
	markupExtensions: string[];

	/** The names of the markup profiles, from the table of the markup behavior page. */
	markupProfiles: string[];

	/** The rules and keys that moved to the other page of the C# split, or absent for a language that never splits. */
	elsewhere?: Elsewhere;
}

/** The identity of an option in a configuration, a link and a selection: its key, qualified by its rule when the key belongs to one rule's entry. */
export function optionId(option: { key: string; scope?: string }): string {
	return option.scope === undefined ? option.key : `${option.scope}.${option.key}`;
}

/** Who reads a key; a key without C# facts is the analyzer's alone. */
export function audienceOf(option: { csharp?: Pick<CSharpOption, 'audience'> }): Audience {
	return option.csharp?.audience ?? 'analyzer';
}

/** The words a number takes besides digits, which a key without C# facts never has. */
export function wordsOf(option: { csharp?: Pick<CSharpOption, 'words'> }): string[] {
	return option.csharp?.words ?? [];
}

export type SourceCatalog = Catalog<SourceSnippet>;
export type SiteCatalog = Catalog<Snippet>;
