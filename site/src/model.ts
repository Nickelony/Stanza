/** The severities an `.editorconfig` can give a rule; `default` stands for no line at all. */
export type Severity = 'error' | 'warning' | 'suggestion' | 'silent' | 'none';

export type RuleSetting = Severity | 'default';

/** The four tiers a rule page names in its tier line. */
export type Tier = 'error' | 'warning' | 'suggestion' | 'silent';

/** A fenced code block of a page, before highlighting. */
export interface SourceSnippet {
	lang: string;
	code: string;
}

/** One line of a highlighted snippet: its plain text, which the diff compares, and its markup. */
export interface SnippetLine {
	text: string;
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

export interface Rule<S> {
	id: string;
	title: string;

	/** The README `###` heading of the table that lists the rule. */
	group: string;

	/** The one-line gap text of the README row, as plain text. */
	summary: string;

	/** The severity the analyzer reports at while the rule is on. */
	tier: Tier;

	/** Whether the analyzer reports the rule with no `.editorconfig` line. */
	onByDefault: boolean;

	/** Whether `stanza-markup` applies the rule with no `.editorconfig` line. */
	markupOnByDefault: boolean;

	/** Whether `stanza-markup` reads the rule's severity, as `MarkupDiagnostics` declares it. */
	readByMarkup: boolean;

	/** The tier line of the page, as written. */
	tierText: string;
	descriptionHtml: string;
	examples: RuleExample<S>[];
	parts: RulePart[];

	/** The keys of the options the page names. */
	options: string[];
	wikiUrl: string;
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

export interface Option<S> {
	key: string;
	descriptionHtml: string;

	/** The `Default:` line of the page as plain text. */
	defaultText: string;

	/** The value the analyzer uses with no line, when the docs give one; otherwise `null`. */
	defaultValue: string | null;

	/** The same for `stanza-markup`, which is `null` when the tool derives its default, as from the profile of the file. */
	markupDefaultValue: string | null;
	readByHtml: string;

	/** Who reads the key: the analyzer (it is in the README table), `stanza-markup`, or both. */
	audience: Audience;
	kind: OptionKind;

	/** Whether the key picks a profile per file, so a line under a glob gives every file the glob matches that profile. */
	perFile: boolean;

	/** The literals of an `enum`. */
	choices: string[];

	/** The bounds of a `number`. */
	min: number | null;
	max: number | null;

	/** The words a `number` takes besides digits. */
	words: string[];
	values: OptionValue[];
	examples: OptionExample<S>[];
	parts: OptionPart[];

	/** The IDs of the rules the page names or that name the key. */
	rules: string[];
	wikiUrl: string;
}

export interface Catalog<S> {
	rules: Rule<S>[];
	options: Option<S>[];

	/** The README `###` headings of the rule tables, in order. */
	groups: string[];

	/** The file extensions the markup profiles claim, without the dot. */
	markupExtensions: string[];

	/** The names of the markup profiles, from the table of the markup behavior page. */
	markupProfiles: string[];
	repoUrl: string;
	wikiHomeUrl: string;
}

export type SourceCatalog = Catalog<SourceSnippet>;

export type SiteCatalog = Catalog<Snippet>;
