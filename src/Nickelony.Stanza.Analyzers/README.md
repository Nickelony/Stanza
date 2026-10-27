# Nickelony.Stanza.Analyzers

Roslyn analyzers and code fixes that lay out blank lines, braces, member bodies and comments in C#.

## Installation

```xml
<PackageReference Include="Nickelony.Stanza.Analyzers" Version="<version>" PrivateAssets="all" />
```

Replace `<version>` with the released version.
The package targets `netstandard2.0` and is compiled against `Microsoft.CodeAnalysis.CSharp` 4.11,
so the host compiler needs Roslyn 4.11 or later, as in the .NET 8.0.400 SDK and Visual Studio 2022 17.11.
The package ships the analyzers and their code fixes, no library, and it adds no dependency to the consuming project.

## Quick start

```text
dotnet format analyzers --severity info
dotnet format analyzers --verify-no-changes --severity info
```

The first command applies the fixes, suggestions included, and the second fails a run that would change a file.
With CSharpier, run the commands of the [run order](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#run-order) instead,
so each tool lays out what the one before it wrote.
[Adopting the analyzers](https://github.com/Nickelony/Stanza/wiki/Adopting-the-analyzers) lists the steps in order.

Next, read [Severity and enablement](#severity-and-enablement) and [Configuration](#configuration), which follow the rule tables.
[Disabling and silencing a rule](https://github.com/Nickelony/Stanza/wiki/Disabling-and-silencing-a-rule) covers a rule that does not fit,
and [Troubleshooting](https://github.com/Nickelony/Stanza/wiki/Troubleshooting) and [Limitations](https://github.com/Nickelony/Stanza/wiki/Limitations)
cover a rule that misbehaves.

## What it does

Most rules add or remove a blank line between two neighboring units of code:

```csharp
// Before
var options = BuildOptions();
logger.LogInformation("Bound {Count} options.", options.Count);
logger.LogInformation("Starting the host.");
return options;

// After
var options = BuildOptions();

logger.LogInformation("Bound {Count} options.", options.Count);
logger.LogInformation("Starting the host.");

return options;
```

The analyzers read C# source only, including the markup inside raw string literals.
Markup files take the separate tool [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md).

## Rules

Each rule has its own page, and the IDE's help link for a diagnostic opens that page.
The tables group the rules by what they lay out.
The **Gap** column says whether a rule adds or removes the blank line at the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) it owns,
or what the rule changes when it works on a keyword, a name, an order or a comment.
The **Ships as** column gives the tier, and [Severity and enablement](#severity-and-enablement) says what each tier does.

### Blank lines between units

| Rule | Gap | Ships as |
| ---- | --- | -------- |
| `ZA0001` | [Adds a blank line between a run of declarations or invocations and its neighbors](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-declaration-and-invocation-runs) | Warning |
| `ZA0002` | [Adds a blank line between a run of members of one kind and its neighbors](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-member-groups) | Warning |
| `ZA0004` | [Adds a blank line between a run of declarations of the exact same type and its neighbors](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-same-type-declaration-groups) | Warning |
| `ZA0005` | [Adds a blank line beside a multi-line construct](https://github.com/Nickelony/Stanza/wiki/ZA0005-Separate-multi-line-constructs) | Warning |
| `ZA0006` | [Removes the blank line between the only two single-line units of a container](https://github.com/Nickelony/Stanza/wiki/ZA0006-Keep-the-two-units-of-a-container-together) | Suggestion, off by default |
| `ZA0007` | [Adds a blank line between the import section and the first member](https://github.com/Nickelony/Stanza/wiki/ZA0007-Separate-the-import-section) | Warning |
| `ZA0008` | [Adds a blank line between import directives of different kinds](https://github.com/Nickelony/Stanza/wiki/ZA0008-Separate-the-import-directive-groups) | Suggestion, off by default |
| `ZA0009` | [Adds a blank line between a file-scoped namespace declaration and its first member](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-a-file-scoped-namespace-declaration-from-its-first-member) | Warning |

### Blank lines inside delimited lists

| Rule | Gap | Ships as |
| ---- | --- | -------- |
| `ZA0010` | [Adds a blank line between multi-line accessors (`get`, `set`, `init`, `add`, `remove`)](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-multi-line-accessors) | Suggestion, off by default |
| `ZA0011` | [Removes the blank line between multi-line accessors](https://github.com/Nickelony/Stanza/wiki/ZA0011-Keep-multi-line-accessors-together) | Suggestion, off by default |
| `ZA0012` | [Removes the blank line between single-line accessors](https://github.com/Nickelony/Stanza/wiki/ZA0012-Keep-single-line-accessors-together) | Suggestion, off by default |
| `ZA0013` | [Adds a blank line between multi-line `switch` sections](https://github.com/Nickelony/Stanza/wiki/ZA0013-Separate-multi-line-switch-sections) | Warning |
| `ZA0014` | [Removes the blank line between multi-line `switch` sections](https://github.com/Nickelony/Stanza/wiki/ZA0014-Keep-multi-line-switch-sections-together) | Suggestion, off by default |
| `ZA0015` | [Removes the blank line between single-line `switch` sections](https://github.com/Nickelony/Stanza/wiki/ZA0015-Keep-single-line-switch-sections-together) | Suggestion, off by default |
| `ZA0016` | [Adds a blank line between multi-line `switch` expression arms](https://github.com/Nickelony/Stanza/wiki/ZA0016-Separate-multi-line-switch-expression-arms) | Warning |
| `ZA0017` | [Removes the blank line between multi-line `switch` expression arms](https://github.com/Nickelony/Stanza/wiki/ZA0017-Keep-multi-line-switch-expression-arms-together) | Suggestion, off by default |
| `ZA0018` | [Removes the blank line between single-line `switch` expression arms](https://github.com/Nickelony/Stanza/wiki/ZA0018-Keep-single-line-switch-expression-arms-together) | Suggestion, off by default |
| `ZA0019` | [Adds a blank line between multi-line initializer members (object, `with`, anonymous object)](https://github.com/Nickelony/Stanza/wiki/ZA0019-Separate-multi-line-initializer-members) | Suggestion, off by default |
| `ZA0020` | [Removes the blank line between multi-line initializer members](https://github.com/Nickelony/Stanza/wiki/ZA0020-Keep-multi-line-initializer-members-together) | Suggestion, off by default |
| `ZA0021` | [Removes the blank line between single-line initializer members](https://github.com/Nickelony/Stanza/wiki/ZA0021-Keep-single-line-initializer-members-together) | Suggestion, off by default |
| `ZA0022` | [Adds a blank line between multi-line collection elements (arrays, collections, C# 12)](https://github.com/Nickelony/Stanza/wiki/ZA0022-Separate-multi-line-collection-elements) | Suggestion, off by default |
| `ZA0023` | [Removes the blank line between multi-line collection elements](https://github.com/Nickelony/Stanza/wiki/ZA0023-Keep-multi-line-collection-elements-together) | Suggestion, off by default |
| `ZA0024` | [Removes the blank line between single-line collection elements](https://github.com/Nickelony/Stanza/wiki/ZA0024-Keep-single-line-collection-elements-together) | Suggestion, off by default |
| `ZA0025` | [Adds a blank line between multi-line arguments (calls, `new`, constructor initializers, attributes, indexers)](https://github.com/Nickelony/Stanza/wiki/ZA0025-Separate-multi-line-arguments) | Suggestion, off by default |
| `ZA0026` | [Removes the blank line between multi-line arguments](https://github.com/Nickelony/Stanza/wiki/ZA0026-Keep-multi-line-arguments-together) | Suggestion, off by default |
| `ZA0027` | [Removes the blank line between single-line arguments](https://github.com/Nickelony/Stanza/wiki/ZA0027-Keep-single-line-arguments-together) | Suggestion, off by default |
| `ZA0028` | [Adds a blank line between multi-line parameters (methods, ctors, delegates, records, indexers, lambdas)](https://github.com/Nickelony/Stanza/wiki/ZA0028-Separate-multi-line-parameters) | Suggestion, off by default |
| `ZA0029` | [Removes the blank line between multi-line parameters](https://github.com/Nickelony/Stanza/wiki/ZA0029-Keep-multi-line-parameters-together) | Suggestion, off by default |
| `ZA0030` | [Removes the blank line between single-line parameters](https://github.com/Nickelony/Stanza/wiki/ZA0030-Keep-single-line-parameters-together) | Suggestion, off by default |
| `ZA0031` | [Adds a blank line between multi-line declarators (`int a = 1, b = 2;`, fields, `for`, `using`)](https://github.com/Nickelony/Stanza/wiki/ZA0031-Separate-multi-line-declarators) | Suggestion, off by default |
| `ZA0032` | [Removes the blank line between multi-line declarators](https://github.com/Nickelony/Stanza/wiki/ZA0032-Keep-multi-line-declarators-together) | Suggestion, off by default |
| `ZA0033` | [Removes the blank line between single-line declarators](https://github.com/Nickelony/Stanza/wiki/ZA0033-Keep-single-line-declarators-together) | Suggestion, off by default |

### Clauses and braces

| Rule | Gap | Ships as |
| ---- | --- | -------- |
| `ZA0034` | [Adds a blank line between a closing brace and the clause keyword after it](https://github.com/Nickelony/Stanza/wiki/ZA0034-Separate-a-clause-keyword-from-the-brace-before-it) | Suggestion, off by default |
| `ZA0035` | [Removes the blank line between a closing brace and the clause keyword after it](https://github.com/Nickelony/Stanza/wiki/ZA0035-Keep-clause-keywords-adjacent) | Suggestion |
| `ZA0036` | [Removes the blank padding beside a brace](https://github.com/Nickelony/Stanza/wiki/ZA0036-Remove-brace-padding) | Suggestion |
| `ZA0037` | [Joins an empty pair of braces that sit on separate lines](https://github.com/Nickelony/Stanza/wiki/ZA0037-Collapse-empty-blocks) | Suggestion |
| `ZA0038` | [Braces of a chain or a body](https://github.com/Nickelony/Stanza/wiki/ZA0038-Use-braces-consistently) | Suggestion |
| `ZA0039` | [Redundant braces](https://github.com/Nickelony/Stanza/wiki/ZA0039-Remove-redundant-braces) | Suggestion, off by default |

### Member bodies

| Rule | Gap | Ships as |
| ---- | --- | -------- |
| `ZA0040` | [A multi-line member from its arrow body](https://github.com/Nickelony/Stanza/wiki/ZA0040-Use-a-block-body-for-a-multi-line-member) | Suggestion, off by default |
| `ZA0041` | [A single-line member from its block body](https://github.com/Nickelony/Stanza/wiki/ZA0041-Use-an-expression-body-for-a-single-line-member) | Suggestion, off by default |
| `ZA0042` | [A multi-line value member from its arrow body](https://github.com/Nickelony/Stanza/wiki/ZA0042-Use-a-block-body-for-a-multi-line-value-member) | Suggestion, off by default |
| `ZA0043` | [A single-line value member from its block body](https://github.com/Nickelony/Stanza/wiki/ZA0043-Use-an-expression-body-for-a-single-line-value-member) | Suggestion, off by default |
| `ZA0044` | [The expression of an expression body from the arrow line](https://github.com/Nickelony/Stanza/wiki/ZA0044-Break-the-expression-off-the-signature-line) | Suggestion |
| `ZA0045` | [The arrow of an expression body from the line it opens](https://github.com/Nickelony/Stanza/wiki/ZA0045-Keep-the-arrow-on-the-signature-line) | Suggestion, off by default |
| `ZA0046` | [The arrow of an expression body from the signature line](https://github.com/Nickelony/Stanza/wiki/ZA0046-Put-the-arrow-on-the-line-after-the-signature) | Suggestion, off by default |

### Regions

| Rule | Gap | Ships as |
| ---- | --- | -------- |
| `ZA0047` | [Adds a blank line on each side of a region marker](https://github.com/Nickelony/Stanza/wiki/ZA0047-Separate-the-region-marker-from-its-neighbors) | Suggestion |
| `ZA0048` | [The closing region marker from the name of its region](https://github.com/Nickelony/Stanza/wiki/ZA0048-Name-the-closing-region-directive) | Suggestion, off by default |
| `ZA0049` | [An empty region from the two markers that bound it](https://github.com/Nickelony/Stanza/wiki/ZA0049-Remove-empty-regions) | Suggestion, off by default |

### Argument names

| Rule | Gap | Ships as |
| ---- | --- | -------- |
| `ZA0050` | [The unnamed arguments of a call that reaches the count threshold](https://github.com/Nickelony/Stanza/wiki/ZA0050-Name-the-arguments-of-a-call) | Suggestion, off by default |
| `ZA0051` | [The name of an argument that already carries its parameter's name](https://github.com/Nickelony/Stanza/wiki/ZA0051-Remove-a-redundant-argument-name) | Suggestion, off by default |

### Markup in raw strings

| Rule | Gap | Ships as |
| ---- | --- | -------- |
| `ZA0101` | [Adds a blank line between multi-line markup elements inside a raw string](https://github.com/Nickelony/Stanza/wiki/ZA0101-Separate-multi-line-markup-elements) | Warning, off by default |
| `ZA0102` | [Removes the blank line between multi-line markup elements](https://github.com/Nickelony/Stanza/wiki/ZA0102-Keep-multi-line-markup-elements-together) | Warning, off by default |
| `ZA0103` | [Removes the blank line between single-line markup elements](https://github.com/Nickelony/Stanza/wiki/ZA0103-Keep-single-line-markup-elements-together) | Warning, off by default |
| `ZA0104` | [Removes extra blank lines between markup elements](https://github.com/Nickelony/Stanza/wiki/ZA0104-Collapse-extra-blank-lines-between-markup-elements) | Warning, off by default |
| `ZA0105` | [A markup comment marker from its text](https://github.com/Nickelony/Stanza/wiki/ZA0105-Separate-a-markup-comment-marker-from-its-text) | Warning, off by default |

### Documentation comments

| Rule | Gap | Ships as |
| ---- | --- | -------- |
| `ZA0201` | [The line breaks and wrapping of a documentation comment](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment) | Suggestion, off by default |
| `ZA0202` | [Documentation paragraphs from the break between them](https://github.com/Nickelony/Stanza/wiki/ZA0202-Mark-documentation-paragraphs) | Suggestion, off by default |
| `ZA0203` | [Documentation padding from the lines around it](https://github.com/Nickelony/Stanza/wiki/ZA0203-Remove-documentation-padding) | Suggestion, off by default |
| `ZA0204` | [The items of a documentation list from its tag](https://github.com/Nickelony/Stanza/wiki/ZA0204-Lay-out-the-items-of-a-documentation-list) | Suggestion, off by default |
| `ZA0205` | [A documentation comment from its declaration](https://github.com/Nickelony/Stanza/wiki/ZA0205-Keep-a-documentation-comment-with-its-declaration) | Suggestion, off by default |
| `ZA0206` | [The whitespace inside a tag from the tokens around it](https://github.com/Nickelony/Stanza/wiki/ZA0206-Collapse-the-whitespace-inside-a-tag) | Suggestion, off by default |
| `ZA0207` | [The slash of a self-closing tag from the tag](https://github.com/Nickelony/Stanza/wiki/ZA0207-Separate-the-slash-of-a-self-closing-tag) | Suggestion, off by default |
| `ZA0208` | [The slash of a self-closing tag from the space before it](https://github.com/Nickelony/Stanza/wiki/ZA0208-Keep-the-slash-of-a-self-closing-tag-tight) | Suggestion, off by default |
| `ZA0209` | [An empty documentation element, written as a self-closing tag](https://github.com/Nickelony/Stanza/wiki/ZA0209-Write-an-empty-element-as-a-self-closing-tag) | Suggestion, off by default |
| `ZA0210` | [A missing period at the end of a documentation element's text](https://github.com/Nickelony/Stanza/wiki/ZA0210-End-the-documentation-text-with-a-period) | Suggestion, off by default |
| `ZA0211` | [The `null` keyword in documentation prose, written as an element](https://github.com/Nickelony/Stanza/wiki/ZA0211-Mark-the-null-keyword) | Suggestion, off by default |
| `ZA0212` | [The order of the elements of a documentation comment](https://github.com/Nickelony/Stanza/wiki/ZA0212-Order-the-elements-of-a-documentation-comment) | Suggestion, off by default |
| `ZA0213` | [The order of a documentation list's header, items, terms and descriptions](https://github.com/Nickelony/Stanza/wiki/ZA0213-Order-the-items-of-a-documentation-list) | Suggestion, off by default |

### Plain comments

| Rule | Gap | Ships as |
| ---- | --- | -------- |
| `ZA0214` | [A single-line comment marker from its text](https://github.com/Nickelony/Stanza/wiki/ZA0214-Separate-a-single-line-comment-marker-from-its-text) | Suggestion, off by default |
| `ZA0215` | [A delimited comment marker from its text](https://github.com/Nickelony/Stanza/wiki/ZA0215-Separate-a-delimited-comment-marker-from-its-text) | Suggestion, off by default |
| `ZA0216` | [A comment that trails code from the code before it](https://github.com/Nickelony/Stanza/wiki/ZA0216-Separate-a-trailing-comment-from-the-code) | Suggestion, off by default |

Each rule has a code fix that supports Fix All.

The markup rules share their IDs with [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md),
which also reports [ZA0100](https://github.com/Nickelony/Stanza/wiki/ZA0100-Report-a-file-that-could-not-be-read-or-parsed) and [ZA0106](https://github.com/Nickelony/Stanza/wiki/ZA0106-Reprint-the-body-of-a-documentation-member).
No analyzer reports those two.

## Severity and enablement

No rule ships as an error.
The **Ships as** column gives each rule's default severity, and *off by default* marks a rule that reports nothing
until a file [enables](https://github.com/Nickelony/Stanza/wiki/Terms) it.

- A warning appears in the IDE and in a build, and fails the build only under `TreatWarningsAsErrors` or `WarningsAsErrors`.
- A suggestion appears in the IDE, and `dotnet format --severity info` fixes it, but `dotnet build` never reports it.
  Raise a suggestion to `warning` to have a build report it.
- A build parses documentation comments only while it writes the documentation file,
  so a build reports the [documentation rules](#documentation-comments)
  only under `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. `dotnet format` reports them either way.

Some rules carry the IDE's unnecessary tag, so the editor fades the code they flag instead of underlining it.

When a file enables one half of a pair, that half owns the gap.
[Which rule owns which gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap) lists the owner of each gap.

## Configuration

Everything is configured in `.editorconfig`.
A rule's `dotnet_diagnostic.<id>.severity` line sets both whether the rule is enabled and how loudly it reports.
It takes `error`, `warning`, `suggestion`, `silent`, `none` or `default`,
and [Terms](https://github.com/Nickelony/Stanza/wiki/Terms) says which of them enable or disable a rule.
[Disabling and silencing a rule](https://github.com/Nickelony/Stanza/wiki/Disabling-and-silencing-a-rule) covers `none`, `silent` and the pragma.

The rules are in the `Formatting` category, which `dotnet_analyzer_diagnostic.category-Formatting.severity` sets.
A `dotnet_analyzer_diagnostic` category or bulk key reaches only the rules that are on by default,
so a rule that is off by default needs its own `dotnet_diagnostic` line.
A rule's own line, even one set to `default`, keeps a bulk key from reaching it.
The sample sets one rule per tier, and disables one:

```ini
[*.cs]
# Warning tier.
dotnet_diagnostic.ZA0001.severity = warning

# Suggestion tier, raised so a build reports it.
dotnet_diagnostic.ZA0036.severity = warning

# Disabled: the rule reports nothing, and a bulk key never reaches it.
dotnet_diagnostic.ZA0037.severity = none

# Off by default until a line like this enables it.
# dotnet_diagnostic.ZA0006.severity = warning

stanza_max_line_length = 100
```

Each key links to its own page, which says what the key controls and lists the values it takes.

| Key | Default |
| --- | --- |
| [`stanza_assertion_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_assertion_roots) | `Assert`, `Expect`, `Should` |
| [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length) | `100` |
| [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length) | `stanza_max_line_length`, then `100` |
| [`stanza_named_argument_count`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_count) | `4` |
| [`stanza_named_argument_match`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_match) | `allow` |
| [`stanza_named_argument_case`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_case) | `sensitive` |
| [`stanza_multiline_body_trigger`](https://github.com/Nickelony/Stanza/wiki/stanza_multiline_body_trigger) | `either` |
| [`stanza_tab_width`](https://github.com/Nickelony/Stanza/wiki/stanza_tab_width) | the file's `tab_width`, then its `indent_size`, then `4` |
| [`stanza_markup_scope`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_scope) | `all` |
| [`stanza_markup_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_max_blank_lines) | `1` |
| [`stanza_doc_expand`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_expand) | `none` |
| [`stanza_doc_min_tail_words`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_min_tail_words) | `2` |
| [`stanza_doc_min_line_words`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_min_line_words) | `2` |
| [`stanza_doc_max_overflow_columns`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_overflow_columns) | `4` |
| [`stanza_doc_weak_tail_words`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_weak_tail_words) | `none` |
| [`stanza_doc_indent_size`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_indent_size) | `0` |
| [`stanza_doc_indent_style`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_indent_style) | `nested` |

The `stanza_doc_*` keys also lay out the `<member>` bodies that stanza-markup rewrites.
The table omits the keys that only stanza-markup reads, such as `stanza_markup_enabled`.
The [markup configuration](https://github.com/Nickelony/Stanza/wiki/Markup-configuration) page lists them.

## See also

- [Documentation wiki](https://github.com/Nickelony/Stanza/wiki) holds the guides and the reference pages.
- [Troubleshooting](https://github.com/Nickelony/Stanza/wiki/Troubleshooting) maps a symptom to the setting or page that decides it.
- [Repository readme](https://github.com/Nickelony/Stanza/blob/main/README.md) describes the packages and how they fit together.
- [Security policy](https://github.com/Nickelony/Stanza/blob/main/SECURITY.md) explains how to report a vulnerability.
- [License](https://github.com/Nickelony/Stanza/blob/main/LICENSE) is the MIT license.
- [Changelog](https://github.com/Nickelony/Stanza/blob/main/CHANGELOG.md) records what changes between releases.
