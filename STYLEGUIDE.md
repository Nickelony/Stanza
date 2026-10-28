# Documentation style guide

This page owns the documentation standard of the repository, and its last section shows the key rules applied.

A doc exists so a reader can use the code now.
Check each claim against the code or a test before writing it, because a wordy doc is cheap to fix and a wrong one is not.
Write plain, complete sentences: cut every word a sentence survives without,
but keep its subject, its verb, the word that carries the reasoning, such as `because`, `so` or `unless`,
and every qualifier, unit, bound and failure mode.

## What counts as a doc

A doc is any prose this repository owns: every Markdown file, an XML doc comment, an inline comment,
and a comment in a project, config, script or workflow file.

Not a doc: generated output (`bin/`, `obj/`, `artifacts/`),
a machine-owned manifest such as `AnalyzerReleases.*.md`, and a JSON value, since strict JSON has no comments.

## Rules

1. **Lead with one sentence.**
   A `<summary>` is one sentence, and a page's opening sentence says what its subject does.
   A comment's second sentence states an invariant the caller needs, and it goes in `<remarks>`.
2. **Say only the non-obvious, and say it concretely.**
   - If the name and signature already say it, delete the doc; delete beats reword.
     Never restate a type or parameter name, "gets or sets", or framework behavior.
   - Name the constraint, edge case, ordering rule or failure mode; a sentence that could describe any class is deleted.
   - Do not walk the reader through a neighbor type or another page's topic;
     mention it only where the text is unreadable without it.
3. **Leave out history, process and filler.**
   - State what the code does and what it guards against, never how it got there:
     no "previously", "no longer", "legacy", "migrated" or "used to", and no review, plan, phase or test id.
   - The [Vale style](.vale/styles/Stanza) bans the padding, history and census phrases it lists,
     such as "simply", "just", "note that" and "in order to".
   - A comment never addresses the reader; a page may say "you" where the imperative does not fit.
4. **Say it once.**
   - A fact has one home: the code, the option page, the rule page or the readme that owns it.
     Every other mention states the consequence where it applies and links the home.
   - Never count a set that grows ("the three rules", "both packages") or list its members outside the home a guard keeps in sync;
     name the family and link the home. A fixed pair, such as the two tags of an element, is not a census.
   - A version, count, default or ID range is stated once, on the option or rule page that owns it.
     Code comments and other pages link that page, and an illustrative key, ID or value is labeled as an example.
   - A source line number is never cited; name the symbol instead.
5. **Use one term per concept.** Use the plain word, and the same word everywhere:
   a rule is *enabled*, not *named* or *toggled*, and a place that holds files is a *directory*.
   A term this repository coins is defined once, in [docs/Terms.md](docs/Terms.md), and linked where a page first uses it.
6. **Examples of tool output are real.** A Before/After pair shows what the fix produces,
   a comparison labels which side to follow, and a measurement says what it counts.
   Prefer an example a test already pins.

## Code comments

- The Stanza rules `.editorconfig` enables lay out `///` comments,
  from the wrap width to the element order and the closing period.
  `./format.ps1` applies them and the build reports what is left,
  so write the words and let the tooling settle the layout.
- Mark a language keyword with `<see langword="..."/>`. `ZA0211` does it for `null`;
  the other keywords are marked by hand. A bare `null` stays only where it is a wire term.
- Documentation elements follow the order `DocElementOrder.ElementOrder` defines,
  which [ZA0212](https://github.com/Nickelony/Stanza/wiki/ZA0212-Order-the-elements-of-a-documentation-comment) enforces.
- An `<exception>` names the condition as a fragment, never with "Thrown when".
- A `cancellationToken` parameter reads "A token that can cancel the {operation}."
  An event reads "Raised when/after", and a protected hook the base invokes reads "Invoked when/after".
- A fallback clause after a semicolon takes a comma: `otherwise, <see .../>`.
- CS1591 is off, so a public member needs a doc only when it carries a fact its name and signature do not.
- A comment on what a member does or guarantees is a `///` doc on the member, and a `//` comment sits inside a body.
  `ConventionGuardTests` fails on a `//` run directly above a member in `src`.
- A `//` label over a group of members keeps a blank line below it, and a test keeps its `//` comments.
- A test needs no XML doc; its name states what it checks.
  A fixture or guard a reader cannot infer from its name earns one line.
- When a fact outgrows `<remarks>`, it moves to the page that owns it,
  and the member keeps a one-line consequence and a link.

## Pages

- A page under `docs/` owns one topic and reads in one sitting.
  It leads with what the subject does, then a command or an example, then the detail.
- A rule page, `docs/rules/<id>-<title>.md`,
  opens with the tier line its descriptor ships in and one sentence on what the rule does.
  A `// Before` and `// After` example follows, then bullets for scope, edge cases and the rule it pairs with.
- An option page, `docs/options/<key>.md`, opens with `# <key>` and one sentence on what the key controls,
  with its default. A table maps each value to its effect, an invalid value included;
  an example and bullets follow.
- `RuleDocumentationGuardTests` checks both openings,
  so a new rule or key fails the `Repository` guards until its page exists.

Pick the form by how the facts relate:

| The facts | The form |
| --- | --- |
| Stand alone and read in any order | A bulleted list |
| Happen in order | A numbered list |
| Share the same fields, such as a value and its effect | A table |
| Depend on each other through `because`, `so` or `unless` | A short paragraph |
| Describe behavior a reader could misread | A Before/After example |

- A bullet holds one fact as a complete sentence with a verb.
- The bullets of one list share a grammatical shape,
  and either every bullet opens with a bold lead-in or none does.
- A list nests one level at most and never holds a single item.
- Instructions use the imperative. Behavior uses the present tense and the active voice.
- A heading or a title uses sentence case, such as `Adding or changing a rule`: only its first word and its names take a capital.

The wiki copies `docs/` flat and verbatim, and a package readme is shown outside the repository,
so both link absolutely:

- A page links another page as `https://github.com/Nickelony/Stanza/wiki/<page>` and a file as `https://github.com/Nickelony/Stanza/blob/main/<path>`.
- A file only the repository shows, such as `CONTRIBUTING.md` or `STYLEGUIDE.md`, may link relatively; a file the wiki or NuGet shows must not,
  because a relative link breaks there.
- A link points at the branch, never a tag, so the reader lands on the current text.
- A page names another file only when the reader cannot follow the topic without it.

## READMEs

A package readme is a front page, not a manual:
a one-line summary, install, one or two runnable examples, then the few facts the signature does not carry.
Install and a first successful command come before any explanation.

- Show the call instead of describing it.
- Never inventory types, members or options; IntelliSense and the pages are the inventory.
  A table a guard keeps in sync with the code is the exception,
  as are the rule table and the default column of the key table in the analyzer readme.
- Keep the prose to about one screen, and link the page that owns any deeper fact.

## Living documentation

- Open work is tracked outside the repository, so no page carries a plan or a list of work to do.
- `CHANGELOG.md` records each user-visible change under `## [Unreleased]` until its release; nothing else keeps a log of completed work.
- A design note that outlives its change is rewritten as durable rationale beside the code it describes.
  The model and the rejected alternatives stay; the process identifiers and estimates go.
- Superseded material is deleted, not kept in the tree.

## Documentation shape

Length is judgment, not a quota: write as short as the facts allow,
and split a page that stops being a one-sitting read rather than trim a fact.
A comment passage that needs a heading, a list or a second paragraph belongs on a page. The ceilings bound a single run:

- An XML doc block is at most 4 `///` lines, so a one-line `<summary>` can carry a `<remarks>` sentence;
  an inline comment run at most 4 `//` lines.
- A paragraph outside fences, tables and lists, a list item and a sentence each have a word ceiling.
  The Vale rules `ParagraphLength`, `ListItemLength` and `SentenceLength` of `.vale/styles/Stanza` hold the numbers.
- A sentence aims for 25 words.

A longer run is split, or its fact moves to the page that owns it; it is never cut to fit.

- Markdown is wrapped by hand at 110 columns.
  `///` comments wrap at `stanza_doc_max_line_length`, and the tooling does the wrapping.
- A hand-wrapped run flows clause to clause:
  a line carries the clause that fills it even when that overshoots the width,
  and the mark closing the clause stays on the line.
  A wrapped line opens on a word, never on a mark or a list bullet.
- A line never runs past 150 columns, link targets not counted, so a longer clause breaks at a comma or a conjunction.
- US English, with "ID" and "IDs" in capitals. Write " - ", never a typographic dash such as an em or en dash.

Vale fails on a longer run, a banned phrase, a British spelling, a lowercase ID or a typographic dash,
and `DocumentationGuardTests` fails on a longer comment block.

## Examples

Each snippet illustrates a rule and is not repository text.

### [Rule 1](#rules) - one sentence, the rest in `<remarks>`

```csharp
// Avoid
/// <summary>Writes the blank line between two units. It is never written above a unit with a directive.</summary>

// Prefer
/// <summary>Writes the blank line between two units.</summary>
/// <remarks>
/// It is never written above a unit with a directive.
/// </remarks>
```

### [Rule 4](#rules) - a second home links the first

```markdown
// Avoid
## Named arguments

The named-argument rules ask for the parameter name once a call passes several arguments, allow the
shorthand when the identifier already carries the name, and compare the names case-sensitively.
```

```markdown
// Prefer
- The named-argument rules ask for the parameter name once a call passes several arguments.
- The [option pages](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_case) own the keys and their values.
```

### [Shape](#documentation-shape) - break at a clause end

```csharp
// Avoid
// Only the half a file enables hands its gap back through a pragma: a pragma that silences the half
// a file gets anyway says the file wants nothing there, not the other shape.
```

```csharp
// Prefer
// Only the half a file enables hands its gap back through a pragma:
// a pragma that silences the half a file gets anyway says the file wants nothing there,
// not the other shape.
```
