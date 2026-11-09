# Nickelony.Stanza.Analyzers

Roslyn analyzers and code fixes for the layout and readability of C#,
from blank lines and braces to argument names, limits, documentation markup and regions.

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

The rules of the Moderate level ship on as suggestions, which a build never reports, so no first build fails on them.
The `StanzaLevel`, `StanzaFormatter` and `StanzaSeverity` MSBuild properties pick another level, the formatter and the severity, as [Presets](#presets) shows.
Once the fixes have run, `<StanzaSeverity>warning</StanzaSeverity>` makes a build enforce the level.

Next, read [Severity and enablement](#severity-and-enablement), [Presets](#presets) and [Configuration](#configuration), which follow the rule tables.
[Disabling and silencing a rule](https://github.com/Nickelony/Stanza/wiki/Disabling-and-silencing-a-rule) covers a rule that does not fit,
and [Troubleshooting](https://github.com/Nickelony/Stanza/wiki/Troubleshooting) and [Limitations](https://github.com/Nickelony/Stanza/wiki/Limitations)
cover a rule that misbehaves.

## What it does

Most rules add or remove a blank line between two neighboring units of code, as [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs) does here:

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

The analyzers read C# source only, including the markup inside the raw string literals that [`stanza_markup_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_literals) picks.
Markup files take the separate tool [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md).

## Rules

Each rule has its own page, and the IDE's help link for a diagnostic opens that page.
The tables group the rules by what they lay out.
The **Gap** column says whether a rule adds or removes the blank line at the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) it owns,
or what the rule changes when it works on a keyword, a name, an order or a comment.
The **Ships as** column gives the tier, and [Severity and enablement](#severity-and-enablement) says what each tier does.

The **Level** column names the first [level](#presets) that enables the rule, and *Opt-in* marks a rule that no level enables.
*No level* marks a markup rule, which no level holds: only its own severity line enables it, the line `stanza-markup` reads too.

### Blank lines between units

| Rule | Gap | Ships as | Level |
| ---- | --- | -------- | ----- |
| `ZA0001` | [Adds a blank line beside a multi-line construct](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) | Suggestion | Moderate |
| `ZA0002` | [Adds a blank line between a run of declarations and its neighbors](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs) | Suggestion | Moderate |
| `ZA0003` | [Adds a blank line between a run of declarations of the exact same type and its neighbors](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-same-type-declaration-runs) | Suggestion, off by default | Strong |
| `ZA0004` | [Adds a blank line between a run of invocations and its neighbors](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs) | Suggestion | Moderate |
| `ZA0005` | [Adds a blank line between a run of assertions and its neighbors](https://github.com/Nickelony/Stanza/wiki/ZA0005-Separate-assertion-runs) | Suggestion | Moderate |
| `ZA0006` | [Adds a blank line between a run of await statements and its neighbors](https://github.com/Nickelony/Stanza/wiki/ZA0006-Separate-await-runs) | Suggestion, off by default | Strong |
| `ZA0007` | [Adds a blank line between a run of event subscriptions and its neighbors](https://github.com/Nickelony/Stanza/wiki/ZA0007-Separate-event-subscription-runs) | Suggestion, off by default | Strong |
| `ZA0008` | [Adds a blank line between a run of assignments and its neighbors](https://github.com/Nickelony/Stanza/wiki/ZA0008-Separate-assignment-runs) | Suggestion, off by default | Strong |
| `ZA0009` | [Adds a blank line above a jump statement that follows a stanza of two or more lines](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-a-jump-statement) | Suggestion, off by default | Strong |
| `ZA0010` | [Adds a blank line below the base call that opens a member body and above the one that closes it](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-the-base-call) | Suggestion, off by default | Strong |
| `ZA0011` | [Adds a blank line below the guard clauses that open a block](https://github.com/Nickelony/Stanza/wiki/ZA0011-Separate-the-guard-clauses) | Suggestion, off by default | Strong |
| `ZA0012` | [Adds a blank line above the comment that leads a single-line unit](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling) | Suggestion, off by default | Strong |
| `ZA0013` | [Adds a blank line between a run of members of one kind and its neighbors](https://github.com/Nickelony/Stanza/wiki/ZA0013-Separate-member-groups) | Suggestion | Moderate |
| `ZA0014` | [Adds a blank line between a run of members that share their access, `static` and `readonly` modifiers and its neighbors](https://github.com/Nickelony/Stanza/wiki/ZA0014-Separate-modifier-groups) | Suggestion, off by default | Strong |
| `ZA0015` | [Adds a blank line between the import section and the first member](https://github.com/Nickelony/Stanza/wiki/ZA0015-Separate-the-import-section) | Suggestion | Minimal |
| `ZA0016` | [Adds a blank line between import directives of different kinds](https://github.com/Nickelony/Stanza/wiki/ZA0016-Separate-the-import-directive-groups) | Suggestion, off by default | Strong |
| `ZA0017` | [Adds a blank line between a file-scoped namespace declaration and its first member](https://github.com/Nickelony/Stanza/wiki/ZA0017-Separate-a-file-scoped-namespace-declaration-from-its-first-member) | Suggestion | Minimal |
| `ZA0051` | [Removes the blank line between the only two single-line units of a container](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) | Suggestion, off by default | Strong |
| `ZA0052` | [Removes the blank lines between the statements of a loose run that holds more than `stanza_max_loose_run_size` statements](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) | Suggestion, off by default | Strong |
| `ZA0053` | [Removes the blank lines between the members of a loose run that holds more than `stanza_max_loose_run_size` members](https://github.com/Nickelony/Stanza/wiki/ZA0053-Gather-a-run-of-loose-members) | Suggestion, off by default | Strong |
| `ZA0054` | [Removes the blank lines inside a run of statements that the grouping rules read as one, and adds those they then ask for](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) | Suggestion, off by default | Opt-in |
| `ZA0055` | [Removes the blank lines inside a run of members that the member group rules read as one, and adds those they then ask for](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-members-together) | Suggestion, off by default | Opt-in |
| `ZA0056` | [Removes the blank lines of a run past `stanza_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/ZA0056-Collapse-extra-blank-lines) | Suggestion | Minimal |

### Delimited lists

| Rule | Gap | Ships as | Level |
| ---- | --- | -------- | ----- |
| `ZA0101` | [Adds a blank line between multi-line accessors (`get`, `set`, `init`, `add`, `remove`)](https://github.com/Nickelony/Stanza/wiki/ZA0101-Separate-multi-line-accessors) | Suggestion, off by default | Opt-in |
| `ZA0102` | [Removes the blank line between multi-line accessors](https://github.com/Nickelony/Stanza/wiki/ZA0102-Keep-multi-line-accessors-together) | Suggestion | Minimal |
| `ZA0103` | [Removes the blank line between single-line accessors](https://github.com/Nickelony/Stanza/wiki/ZA0103-Keep-single-line-accessors-together) | Suggestion | Minimal |
| `ZA0111` | [Adds a blank line between multi-line `switch` sections](https://github.com/Nickelony/Stanza/wiki/ZA0111-Separate-multi-line-switch-sections) | Suggestion | Moderate |
| `ZA0112` | [Removes the blank line between multi-line `switch` sections](https://github.com/Nickelony/Stanza/wiki/ZA0112-Keep-multi-line-switch-sections-together) | Suggestion, off by default | Opt-in |
| `ZA0113` | [Removes the blank line between single-line `switch` sections](https://github.com/Nickelony/Stanza/wiki/ZA0113-Keep-single-line-switch-sections-together) | Suggestion | Minimal |
| `ZA0121` | [Adds a blank line between multi-line `switch` expression arms](https://github.com/Nickelony/Stanza/wiki/ZA0121-Separate-multi-line-switch-expression-arms) | Suggestion | Moderate |
| `ZA0122` | [Removes the blank line between multi-line `switch` expression arms](https://github.com/Nickelony/Stanza/wiki/ZA0122-Keep-multi-line-switch-expression-arms-together) | Suggestion, off by default | Opt-in |
| `ZA0123` | [Removes the blank line between single-line `switch` expression arms](https://github.com/Nickelony/Stanza/wiki/ZA0123-Keep-single-line-switch-expression-arms-together) | Suggestion | Minimal |
| `ZA0131` | [Adds a blank line between multi-line initializer members (object, `with`, anonymous object)](https://github.com/Nickelony/Stanza/wiki/ZA0131-Separate-multi-line-initializer-members) | Suggestion, off by default | Strong |
| `ZA0132` | [Removes the blank line between multi-line initializer members](https://github.com/Nickelony/Stanza/wiki/ZA0132-Keep-multi-line-initializer-members-together) | Suggestion, off by default | Opt-in |
| `ZA0133` | [Removes the blank line between single-line initializer members](https://github.com/Nickelony/Stanza/wiki/ZA0133-Keep-single-line-initializer-members-together) | Suggestion | Minimal |
| `ZA0141` | [Adds a blank line between multi-line collection elements (arrays, collections, C# 12)](https://github.com/Nickelony/Stanza/wiki/ZA0141-Separate-multi-line-collection-elements) | Suggestion, off by default | Strong |
| `ZA0142` | [Removes the blank line between multi-line collection elements](https://github.com/Nickelony/Stanza/wiki/ZA0142-Keep-multi-line-collection-elements-together) | Suggestion, off by default | Opt-in |
| `ZA0143` | [Removes the blank line between single-line collection elements](https://github.com/Nickelony/Stanza/wiki/ZA0143-Keep-single-line-collection-elements-together) | Suggestion | Minimal |
| `ZA0151` | [Adds a blank line between multi-line arguments (calls, `new`, constructor initializers, attributes, indexers, tuple literals)](https://github.com/Nickelony/Stanza/wiki/ZA0151-Separate-multi-line-arguments) | Suggestion, off by default | Strong |
| `ZA0152` | [Removes the blank line between multi-line arguments](https://github.com/Nickelony/Stanza/wiki/ZA0152-Keep-multi-line-arguments-together) | Suggestion, off by default | Opt-in |
| `ZA0153` | [Removes the blank line between single-line arguments](https://github.com/Nickelony/Stanza/wiki/ZA0153-Keep-single-line-arguments-together) | Suggestion | Minimal |
| `ZA0161` | [Adds a blank line between multi-line parameters (methods, ctors, delegates, records, indexers, lambdas, tuple types)](https://github.com/Nickelony/Stanza/wiki/ZA0161-Separate-multi-line-parameters) | Suggestion, off by default | Strong |
| `ZA0162` | [Removes the blank line between multi-line parameters](https://github.com/Nickelony/Stanza/wiki/ZA0162-Keep-multi-line-parameters-together) | Suggestion, off by default | Opt-in |
| `ZA0163` | [Removes the blank line between single-line parameters](https://github.com/Nickelony/Stanza/wiki/ZA0163-Keep-single-line-parameters-together) | Suggestion | Minimal |
| `ZA0171` | [Adds a blank line between multi-line declarators (`int a = 1, b = 2;`, fields, `for`, `using`)](https://github.com/Nickelony/Stanza/wiki/ZA0171-Separate-multi-line-declarators) | Suggestion, off by default | Strong |
| `ZA0172` | [Removes the blank line between multi-line declarators](https://github.com/Nickelony/Stanza/wiki/ZA0172-Keep-multi-line-declarators-together) | Suggestion, off by default | Opt-in |
| `ZA0173` | [Removes the blank line between single-line declarators](https://github.com/Nickelony/Stanza/wiki/ZA0173-Keep-single-line-declarators-together) | Suggestion | Minimal |
| `ZA0198` | [Adds a comma after the last element of a list that closes on its own line](https://github.com/Nickelony/Stanza/wiki/ZA0198-Add-a-trailing-comma) | Suggestion, off by default | Strong |
| `ZA0199` | [Removes the comma after the last element of a list that closes on its own line](https://github.com/Nickelony/Stanza/wiki/ZA0199-Remove-the-trailing-comma) | Suggestion, off by default | Opt-in |

### Clauses and braces

| Rule | Gap | Ships as | Level |
| ---- | --- | -------- | ----- |
| `ZA0201` | [Adds a blank line between a closing brace and the clause keyword after it](https://github.com/Nickelony/Stanza/wiki/ZA0201-Separate-a-clause-keyword-from-the-brace-before-it) | Suggestion, off by default | Opt-in |
| `ZA0202` | [Removes the blank line between a closing brace and the clause keyword after it](https://github.com/Nickelony/Stanza/wiki/ZA0202-Keep-clause-keywords-adjacent) | Suggestion | Minimal |
| `ZA0203` | [Removes the blank padding beside a brace](https://github.com/Nickelony/Stanza/wiki/ZA0203-Remove-brace-padding) | Suggestion | Minimal |
| `ZA0204` | [Joins an empty pair of braces that sit on separate lines](https://github.com/Nickelony/Stanza/wiki/ZA0204-Collapse-empty-blocks) | Suggestion | Minimal |
| `ZA0205` | [Braces of a chain or a body](https://github.com/Nickelony/Stanza/wiki/ZA0205-Use-braces-consistently) | Suggestion | Moderate |
| `ZA0206` | [Redundant braces](https://github.com/Nickelony/Stanza/wiki/ZA0206-Remove-redundant-braces) | Suggestion, off by default | Strong |

### Member bodies

| Rule | Gap | Ships as | Level |
| ---- | --- | -------- | ----- |
| `ZA0301` | [A multi-line member from its arrow body](https://github.com/Nickelony/Stanza/wiki/ZA0301-Use-a-block-body-for-a-multi-line-member) | Suggestion | Moderate |
| `ZA0302` | [A single-line member from its block body](https://github.com/Nickelony/Stanza/wiki/ZA0302-Use-an-expression-body-for-a-single-line-member) | Suggestion, off by default | Strong |
| `ZA0303` | [A multi-line value member from its arrow body](https://github.com/Nickelony/Stanza/wiki/ZA0303-Use-a-block-body-for-a-multi-line-value-member) | Suggestion, off by default | Strong |
| `ZA0304` | [A single-line value member from its block body](https://github.com/Nickelony/Stanza/wiki/ZA0304-Use-an-expression-body-for-a-single-line-value-member) | Suggestion, off by default | Strong |
| `ZA0305` | [The expression of an expression body from the arrow line](https://github.com/Nickelony/Stanza/wiki/ZA0305-Break-the-expression-off-the-signature-line) | Suggestion | Moderate |
| `ZA0306` | [The arrow of an expression body from the line it opens](https://github.com/Nickelony/Stanza/wiki/ZA0306-Keep-the-arrow-on-the-signature-line) | Suggestion, off by default | Strong |
| `ZA0307` | [The arrow of an expression body from the signature line](https://github.com/Nickelony/Stanza/wiki/ZA0307-Put-the-arrow-on-the-line-after-the-signature) | Suggestion, off by default | Opt-in |

### Regions and conditional blocks

| Rule | Gap | Ships as | Level |
| ---- | --- | -------- | ----- |
| `ZA0401` | [Adds a blank line on each side of a region marker](https://github.com/Nickelony/Stanza/wiki/ZA0401-Separate-the-region-marker-from-its-neighbors) | Suggestion | Minimal |
| `ZA0402` | [Adds a blank line above an `#if` and below its `#endif`](https://github.com/Nickelony/Stanza/wiki/ZA0402-Separate-the-conditional-block-from-its-neighbors) | Suggestion | Moderate |
| `ZA0403` | [The closing region marker from the name of its region](https://github.com/Nickelony/Stanza/wiki/ZA0403-Name-the-closing-region-directive) | Suggestion, off by default | Strong |
| `ZA0404` | [The closing `#endif` of a long conditional block from the condition of its `#if`](https://github.com/Nickelony/Stanza/wiki/ZA0404-Name-the-closing-conditional-directive) | Suggestion, off by default | Strong |
| `ZA0405` | [An empty region from the two markers that bound it](https://github.com/Nickelony/Stanza/wiki/ZA0405-Remove-empty-regions) | Suggestion | Moderate |

### Argument names

| Rule | Gap | Ships as | Level |
| ---- | --- | -------- | ----- |
| `ZA0501` | [The unnamed arguments of a call that reaches the count threshold](https://github.com/Nickelony/Stanza/wiki/ZA0501-Name-the-arguments-of-a-call) | Suggestion, off by default | Strong |
| `ZA0502` | [The `true`, `false`, `null` and `default` arguments of a call that passes two or more](https://github.com/Nickelony/Stanza/wiki/ZA0502-Name-the-literal-arguments-of-a-call) | Suggestion, off by default | Strong |
| `ZA0503` | [The name of an argument that already carries its parameter's name](https://github.com/Nickelony/Stanza/wiki/ZA0503-Remove-a-redundant-argument-name) | Suggestion, off by default | Strong |

### Statement simplification

| Rule | Gap | Ships as | Level |
| ---- | --- | -------- | ----- |
| `ZA0601` | [Moves the statements of an `else` after their `if` when the branch of the `if` ends in a jump](https://github.com/Nickelony/Stanza/wiki/ZA0601-Remove-the-else-after-a-jump) | Suggestion, off by default | Strong |
| `ZA0602` | [Merges an `if` that holds only another `if` into one condition, within `stanza_max_logical_operators`](https://github.com/Nickelony/Stanza/wiki/ZA0602-Merge-nested-if-statements) | Suggestion, off by default | Strong |

### Expression style

| Rule | Gap | Ships as | Level |
| ---- | --- | -------- | ----- |
| `ZA0651` | [A comparison with `null` through `==` or `!=`, written as `is null` or `is not null`](https://github.com/Nickelony/Stanza/wiki/ZA0651-Use-a-pattern-for-a-null-check) | Suggestion | Moderate |
| `ZA0652` | [A null pattern, written as a comparison through `==` or `!=`](https://github.com/Nickelony/Stanza/wiki/ZA0652-Use-an-equality-operator-for-a-null-check) | Suggestion, off by default | Opt-in |

### Documentation comments

| Rule | Gap | Ships as | Level |
| ---- | --- | -------- | ----- |
| `ZA0901` | [The line breaks and wrapping of a documentation comment](https://github.com/Nickelony/Stanza/wiki/ZA0901-Lay-out-a-documentation-comment) | Suggestion, off by default | Strong |
| `ZA0902` | [Documentation paragraphs from the break between them](https://github.com/Nickelony/Stanza/wiki/ZA0902-Mark-documentation-paragraphs) | Suggestion, off by default | Strong |
| `ZA0903` | [Documentation padding from the lines around it](https://github.com/Nickelony/Stanza/wiki/ZA0903-Remove-documentation-padding) | Suggestion, off by default | Strong |
| `ZA0904` | [The items of a documentation list from its tag](https://github.com/Nickelony/Stanza/wiki/ZA0904-Lay-out-the-items-of-a-documentation-list) | Suggestion, off by default | Strong |
| `ZA0905` | [A documentation comment from its declaration](https://github.com/Nickelony/Stanza/wiki/ZA0905-Keep-a-documentation-comment-with-its-declaration) | Suggestion | Minimal |
| `ZA0906` | [The whitespace inside a tag from the tokens around it](https://github.com/Nickelony/Stanza/wiki/ZA0906-Collapse-the-whitespace-inside-a-tag) | Suggestion | Minimal |
| `ZA0907` | [The slash of a self-closing tag from the tag](https://github.com/Nickelony/Stanza/wiki/ZA0907-Separate-the-slash-of-a-self-closing-tag) | Suggestion, off by default | Opt-in |
| `ZA0908` | [The slash of a self-closing tag from the space before it](https://github.com/Nickelony/Stanza/wiki/ZA0908-Keep-the-slash-of-a-self-closing-tag-tight) | Suggestion, off by default | Strong |
| `ZA0909` | [An empty documentation element, written as a self-closing tag](https://github.com/Nickelony/Stanza/wiki/ZA0909-Write-an-empty-element-as-a-self-closing-tag) | Suggestion | Moderate |
| `ZA0910` | [A lowercase letter at the start of a documentation element's text](https://github.com/Nickelony/Stanza/wiki/ZA0910-Capitalize-the-documentation-text) | Suggestion, off by default | Strong |
| `ZA0911` | [A missing period at the end of a documentation element's text](https://github.com/Nickelony/Stanza/wiki/ZA0911-End-the-documentation-text-with-a-period) | Suggestion, off by default | Strong |
| `ZA0912` | [A language keyword in documentation prose, written as an element](https://github.com/Nickelony/Stanza/wiki/ZA0912-Mark-the-language-keywords) | Suggestion, off by default | Strong |
| `ZA0913` | [The order of the elements of a documentation comment](https://github.com/Nickelony/Stanza/wiki/ZA0913-Order-the-elements-of-a-documentation-comment) | Suggestion, off by default | Strong |
| `ZA0914` | [The order of a documentation list's header, items, terms and descriptions](https://github.com/Nickelony/Stanza/wiki/ZA0914-Order-the-items-of-a-documentation-list) | Suggestion, off by default | Strong |

### Plain comments

| Rule | Gap | Ships as | Level |
| ---- | --- | -------- | ----- |
| `ZA0951` | [A single-line comment marker from its text](https://github.com/Nickelony/Stanza/wiki/ZA0951-Separate-a-single-line-comment-marker-from-its-text) | Suggestion | Moderate |
| `ZA0952` | [A delimited comment marker from its text](https://github.com/Nickelony/Stanza/wiki/ZA0952-Separate-a-delimited-comment-marker-from-its-text) | Suggestion | Moderate |
| `ZA0953` | [A comment that trails code from the code before it](https://github.com/Nickelony/Stanza/wiki/ZA0953-Separate-a-trailing-comment-from-the-code) | Suggestion | Moderate |

### Limits

| Rule | Gap | Ships as | Level |
| ---- | --- | -------- | ----- |
| `ZA1001` | [Reports a line that runs past `stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/ZA1001-Shorten-a-long-line) | Suggestion, off by default | Opt-in |
| `ZA1002` | [Reports a member that holds more statements than `stanza_max_member_statements`](https://github.com/Nickelony/Stanza/wiki/ZA1002-Shorten-a-long-member) | Suggestion, off by default | Strong |
| `ZA1003` | [Reports a parameter list that holds more parameters than `stanza_max_parameters`](https://github.com/Nickelony/Stanza/wiki/ZA1003-Shorten-a-long-parameter-list) | Suggestion, off by default | Strong |
| `ZA1004` | [Reports a statement that nests deeper than `stanza_max_nesting_depth`](https://github.com/Nickelony/Stanza/wiki/ZA1004-Flatten-deeply-nested-code) | Suggestion, off by default | Strong |
| `ZA1005` | [Reports a condition that holds more logical operators than `stanza_max_logical_operators`](https://github.com/Nickelony/Stanza/wiki/ZA1005-Shorten-a-long-condition) | Suggestion, off by default | Strong |
| `ZA1006` | [Reports an `if` chain that holds more branches than `stanza_max_else_if_branches`](https://github.com/Nickelony/Stanza/wiki/ZA1006-Break-up-a-long-else-if-chain) | Suggestion, off by default | Strong |
| `ZA1007` | [Reports a ternary chain that holds more `?` operators than `stanza_max_ternary_operators`](https://github.com/Nickelony/Stanza/wiki/ZA1007-Break-up-a-long-ternary-chain) | Suggestion, off by default | Strong |

### Markup rules

| Rule | Gap | Ships as | Level |
| ---- | --- | -------- | ----- |
| `ZA2001` | [Adds a blank line between multi-line markup elements inside a raw string](https://github.com/Nickelony/Stanza/wiki/ZA2001-Separate-multi-line-markup-elements) | Suggestion, off by default | No level |
| `ZA2002` | [Removes the blank line between multi-line markup elements](https://github.com/Nickelony/Stanza/wiki/ZA2002-Keep-multi-line-markup-elements-together) | Suggestion, off by default | No level |
| `ZA2003` | [Removes the blank line between single-line markup elements](https://github.com/Nickelony/Stanza/wiki/ZA2003-Keep-single-line-markup-elements-together) | Suggestion, off by default | No level |
| `ZA2004` | [Adds a blank line above the comment that leads a single-line markup element](https://github.com/Nickelony/Stanza/wiki/ZA2004-Separate-a-commented-markup-element) | Suggestion, off by default | No level |
| `ZA2005` | [Removes the blank lines between an element's tags and the first and last item inside it](https://github.com/Nickelony/Stanza/wiki/ZA2005-Remove-markup-element-padding) | Suggestion, off by default | No level |
| `ZA2006` | [Removes extra blank lines between markup elements](https://github.com/Nickelony/Stanza/wiki/ZA2006-Collapse-extra-blank-lines-between-markup-elements) | Suggestion, off by default | No level |
| `ZA2007` | [A markup comment marker from its text](https://github.com/Nickelony/Stanza/wiki/ZA2007-Separate-a-markup-comment-marker-from-its-text) | Suggestion, off by default | No level |
| `ZA2008` | [The slash of a self-closing markup element from the tag](https://github.com/Nickelony/Stanza/wiki/ZA2008-Separate-the-slash-of-a-self-closing-markup-element) | Suggestion, off by default | No level |
| `ZA2009` | [The slash of a self-closing markup element from the space before it](https://github.com/Nickelony/Stanza/wiki/ZA2009-Keep-the-slash-of-a-self-closing-markup-element-tight) | Suggestion, off by default | No level |
| `ZA2010` | [An empty markup element, written as a self-closing tag](https://github.com/Nickelony/Stanza/wiki/ZA2010-Write-an-empty-markup-element-as-a-self-closing-tag) | Suggestion, off by default | No level |

Each rule has a code fix that supports Fix All, except a [report-only rule](https://github.com/Nickelony/Stanza/wiki/Terms), whose **Gap** text opens with Reports.
A report-only rule changes no file, so a check fails on its reports.

The markup rules share their IDs with [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md),
which also reports [ZA2901](https://github.com/Nickelony/Stanza/wiki/ZA2901-Report-a-file-that-could-not-be-read-or-parsed) and [ZA2902](https://github.com/Nickelony/Stanza/wiki/ZA2902-Reprint-the-body-of-a-documentation-member).
No analyzer reports those two.

## Severity and enablement

Each rule ships as a suggestion, and the rules of the Moderate level ship on.
The **Ships as** column marks every other rule *off by default*:
it reports nothing until a [level](#presets) or a line [enables](https://github.com/Nickelony/Stanza/wiki/Terms) it.

- A warning appears in the IDE and in a build, and fails the build only under `TreatWarningsAsErrors` or `WarningsAsErrors`.
  A rule raised to a warning fails such a build on unformatted code until the fixes have run.
- A suggestion appears in the IDE, and `dotnet format --severity info` fixes it, but `dotnet build` never reports it.
  Set `StanzaSeverity` to `warning` to have a build report the rules of the level, or raise one rule with its own line.
- A build parses documentation comments only while it writes the documentation file,
  so a build reports the [documentation rules](#documentation-comments)
  only under `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. `dotnet format` reports them either way.

Some rules carry the IDE's unnecessary tag, so the editor fades the code they flag instead of underlining it.

When a file enables one half of a pair, that half owns the gap.
[Which rule owns which gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap) lists the owner of each gap.

## Presets

A bulk key never reaches a rule that is off by default,
so MSBuild properties pick a level of rules, their severity and the formatter the team runs:

```xml
<PropertyGroup>
	<StanzaLevel>strong</StanzaLevel>
	<StanzaSeverity>warning</StanzaSeverity>
	<StanzaFormatter>csharpier</StanzaFormatter>
</PropertyGroup>
```

The [presets page](https://github.com/Nickelony/Stanza/wiki/Presets) lists the defaults, says which level and formatter to pick,
and says what wins over a level, such as a rule's own `.editorconfig` line.

## Configuration

Everything is configured in `.editorconfig`, and the [rule configurator](https://nickelony.github.io/Stanza/) writes the lines for the severities and values you pick.
A rule's `dotnet_diagnostic.<id>.severity` line sets both whether the rule is enabled and how loudly it reports.
It takes `error`, `warning`, `suggestion`, `silent`, `none` or `default`,
and [Terms](https://github.com/Nickelony/Stanza/wiki/Terms) says which of them enable or disable a rule.
[Disabling and silencing a rule](https://github.com/Nickelony/Stanza/wiki/Disabling-and-silencing-a-rule) covers `none`, `silent` and the pragma.

The rules are in the `Formatting` category, which `dotnet_analyzer_diagnostic.category-Formatting.severity` sets.
A `dotnet_analyzer_diagnostic` category or bulk key reaches only the rules that are on by default, and with a level file it reaches none,
so a rule that is off needs its own `dotnet_diagnostic` line.

A rule's own line, even one set to `default`, keeps a bulk key from reaching it.
The sample raises one rule, disables one, and shows how to enable one that is off by default:

```ini
[*.cs]
# On by default as a suggestion, raised so a build reports it.
dotnet_diagnostic.ZA0002.severity = warning

# Disabled: the rule reports nothing, and a bulk key never reaches it.
dotnet_diagnostic.ZA0204.severity = none

# Off by default until a line like this enables it.
# dotnet_diagnostic.ZA0051.severity = warning

stanza_max_line_length = 100
```

Each key links to its own page, which says what the key controls and lists the values it takes.

| Key | Default |
| --- | --- |
| [`stanza_assertion_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_assertion_roots) | `Assert`, `Expect`, `Should` |
| [`stanza_guard_roots`](https://github.com/Nickelony/Stanza/wiki/stanza_guard_roots) | `none` |
| [`stanza_assignment_targets`](https://github.com/Nickelony/Stanza/wiki/stanza_assignment_targets) | `mixed` |
| [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length) | `max_line_length`, then `100` |
| [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length) | `stanza_max_line_length`, then `max_line_length`, then `100` |
| [`stanza_named_argument_min_arguments`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_min_arguments) | `4` |
| [`stanza_named_argument_match`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_match) | `allow` |
| [`stanza_named_argument_case`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_case) | `sensitive` |
| [`stanza_named_argument_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_literals) | `keywords` |
| [`stanza_max_loose_run_size`](https://github.com/Nickelony/Stanza/wiki/stanza_max_loose_run_size) | `3` |
| [`stanza_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_max_blank_lines) | `1` |
| [`stanza_max_member_statements`](https://github.com/Nickelony/Stanza/wiki/stanza_max_member_statements) | `40` |
| [`stanza_max_parameters`](https://github.com/Nickelony/Stanza/wiki/stanza_max_parameters) | `7` |
| [`stanza_max_nesting_depth`](https://github.com/Nickelony/Stanza/wiki/stanza_max_nesting_depth) | `3` |
| [`stanza_max_logical_operators`](https://github.com/Nickelony/Stanza/wiki/stanza_max_logical_operators) | `4` |
| [`stanza_max_else_if_branches`](https://github.com/Nickelony/Stanza/wiki/stanza_max_else_if_branches) | `4` |
| [`stanza_max_ternary_operators`](https://github.com/Nickelony/Stanza/wiki/stanza_max_ternary_operators) | `2` |
| [`stanza_comment_banner_min_run`](https://github.com/Nickelony/Stanza/wiki/stanza_comment_banner_min_run) | `3` |
| [`stanza_min_lines_to_name_endif`](https://github.com/Nickelony/Stanza/wiki/stanza_min_lines_to_name_endif) | `10` |
| [`stanza_doc_langwords`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_langwords) | `null` |
| [`stanza_multi_line_member_part`](https://github.com/Nickelony/Stanza/wiki/stanza_multi_line_member_part) | `either` |
| [`stanza_markup_scope`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_scope) | `all` |
| [`stanza_markup_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_max_blank_lines) | `1` |
| [`stanza_markup_separated_elements`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_separated_elements) | `none` |
| [`stanza_markup_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_literals) | `marked` |
| [`stanza_doc_block_elements`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_block_elements) | `minimal` |
| [`stanza_doc_min_tail_words`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_min_tail_words) | `2` |
| [`stanza_doc_min_line_words`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_min_line_words) | `2` |
| [`stanza_doc_max_overflow_columns`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_overflow_columns) | `4` |
| [`stanza_doc_weak_line_end_words`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_weak_line_end_words) | `none` |
| [`stanza_doc_indent_size`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_indent_size) | `0` |
| [`stanza_doc_text_indent`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_text_indent) | `nested` |

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
