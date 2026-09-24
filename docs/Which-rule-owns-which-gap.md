# Which rule owns which gap

Each [gap](https://github.com/Nickelony/Stanza/wiki/Terms) has at most one owner: a rule, or one half of a [pair](https://github.com/Nickelony/Stanza/wiki/Terms).
[The analyzer readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md) lists the rules with their tiers;
this page maps a gap to the rule that owns it.

## Gaps and owners

| Gap | Owner |
| --- | --- |
| A blank line beside a brace, including the one between a clause header and its `{`, but not the one above a block that follows a sibling | [ZA0035](https://github.com/Nickelony/Stanza/wiki/ZA0035-Remove-brace-padding) |
| A gap with a multi-line neighbor, such as the unit before a multi-line construct or the unit after its `}` | [ZA0004](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-multi-line-constructs) |
| Runs of statements, members of one kind, and declarations of one type | [ZA0001](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-declaration-and-invocation-runs), [ZA0002](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-member-groups) and [ZA0003](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-same-type-declaration-groups) |
| The import section, its directive groups, and the header of a file-scoped namespace | [ZA0006](https://github.com/Nickelony/Stanza/wiki/ZA0006-Separate-the-import-section), [ZA0007](https://github.com/Nickelony/Stanza/wiki/ZA0007-Separate-the-import-directive-groups) and [ZA0008](https://github.com/Nickelony/Stanza/wiki/ZA0008-Separate-a-file-scoped-namespace-declaration-from-its-first-member) |
| A region marker and the lines beside it | [ZA0046](https://github.com/Nickelony/Stanza/wiki/ZA0046-Separate-the-region-marker-from-its-neighbors) |
| A blank line between two elements of a delimited list | the pair for that list when an element spans more than one line, such as [ZA0024](https://github.com/Nickelony/Stanza/wiki/ZA0024-Separate-multi-line-arguments) and [ZA0025](https://github.com/Nickelony/Stanza/wiki/ZA0025-Keep-multi-line-arguments-together) for arguments, and its single-line tight rule otherwise, such as [ZA0026](https://github.com/Nickelony/Stanza/wiki/ZA0026-Keep-single-line-arguments-together) |
| A blank line between two units of a construct with rules of its own, such as accessors | the rules for that construct, such as [ZA0009](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-multi-line-accessors) and [ZA0010](https://github.com/Nickelony/Stanza/wiki/ZA0010-Keep-multi-line-accessors-together) for multi-line accessors and [ZA0011](https://github.com/Nickelony/Stanza/wiki/ZA0011-Keep-single-line-accessors-together) for single-line ones |
| A blank line between a closing brace and the clause keyword that continues it | [ZA0033](https://github.com/Nickelony/Stanza/wiki/ZA0033-Separate-a-clause-keyword-from-the-brace-before-it) and [ZA0034](https://github.com/Nickelony/Stanza/wiki/ZA0034-Keep-clause-keywords-adjacent) |
| Whitespace between the braces of an empty body | [ZA0036](https://github.com/Nickelony/Stanza/wiki/ZA0036-Collapse-empty-blocks) |
| The blank line in a container of two units | [ZA0005](https://github.com/Nickelony/Stanza/wiki/ZA0005-Keep-the-two-units-of-a-container-together) |
| The break between an expression body's arrow and its expression | [ZA0043](https://github.com/Nickelony/Stanza/wiki/ZA0043-Break-the-expression-off-the-signature-line) |
| The space before a trailing comment on a code line | [ZA0216](https://github.com/Nickelony/Stanza/wiki/ZA0216-Separate-a-trailing-comment-from-the-code) |
| The elements of a documentation comment | [ZA0201](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment), [ZA0202](https://github.com/Nickelony/Stanza/wiki/ZA0202-Mark-documentation-paragraphs), [ZA0203](https://github.com/Nickelony/Stanza/wiki/ZA0203-Remove-documentation-padding), [ZA0204](https://github.com/Nickelony/Stanza/wiki/ZA0204-Lay-out-the-items-of-a-documentation-list), [ZA0212](https://github.com/Nickelony/Stanza/wiki/ZA0212-Order-the-elements-of-a-documentation-comment) and [ZA0213](https://github.com/Nickelony/Stanza/wiki/ZA0213-Order-the-items-of-a-documentation-list) |
| A documentation comment and the declaration it documents | [ZA0205](https://github.com/Nickelony/Stanza/wiki/ZA0205-Keep-a-documentation-comment-with-its-declaration) |
| The whitespace inside a documentation tag | [ZA0206](https://github.com/Nickelony/Stanza/wiki/ZA0206-Collapse-the-whitespace-inside-a-tag) |
| An element that holds nothing, written as a self-closing tag | [ZA0209](https://github.com/Nickelony/Stanza/wiki/ZA0209-Write-an-empty-element-as-a-self-closing-tag) |
| A comment marker and the text it touches, in a markup comment, a `//` comment or a `/* */` block | [ZA0105](https://github.com/Nickelony/Stanza/wiki/ZA0105-Separate-a-markup-comment-marker-from-its-text), [ZA0214](https://github.com/Nickelony/Stanza/wiki/ZA0214-Separate-a-single-line-comment-marker-from-its-text) and [ZA0215](https://github.com/Nickelony/Stanza/wiki/ZA0215-Separate-a-delimited-comment-marker-from-its-text) |
| Two markup elements inside a raw string literal | [ZA0101](https://github.com/Nickelony/Stanza/wiki/ZA0101-Separate-multi-line-markup-elements), [ZA0102](https://github.com/Nickelony/Stanza/wiki/ZA0102-Keep-multi-line-markup-elements-together), [ZA0103](https://github.com/Nickelony/Stanza/wiki/ZA0103-Keep-single-line-markup-elements-together) and [ZA0104](https://github.com/Nickelony/Stanza/wiki/ZA0104-Collapse-extra-blank-lines-between-markup-elements) |
| Two closing braces of nested containers | the .NET SDK's [`IDE2002`](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide2002), when `csharp_style_allow_blank_lines_between_consecutive_braces_experimental` is `false`; no Stanza rule owns the gap, since [`ZA0035`](https://github.com/Nickelony/Stanza/wiki/ZA0035-Remove-brace-padding) skips the pair |
| Two opening braces of nested containers | nothing: the rules leave it as it is |

## One class, two owners

The blank line after the fields belongs to [ZA0001](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-declaration-and-invocation-runs),
and the blank line after the properties belongs to [ZA0002](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-member-groups).
The [ZA0002 page](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-member-groups) shows the class before and after.

## Pairs and their default halves

The [default half](https://github.com/Nickelony/Stanza/wiki/Terms) owns the gap unless a file [enables](https://github.com/Nickelony/Stanza/wiki/Terms) only the other half,
or, in the arrow pair, enables both.
The table holds for the analyzer. In [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md),
[ZA0101](https://github.com/Nickelony/Stanza/wiki/ZA0101-Separate-multi-line-markup-elements) owns the markup gap unless a file disables it or enables `ZA0102`.

| Gap | Halves | Default half |
| --- | --- | --- |
| Multi-line accessors | [ZA0009](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-multi-line-accessors) and [ZA0010](https://github.com/Nickelony/Stanza/wiki/ZA0010-Keep-multi-line-accessors-together) | `ZA0010` |
| Multi-line switch sections | [ZA0012](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-multi-line-switch-sections) and [ZA0013](https://github.com/Nickelony/Stanza/wiki/ZA0013-Keep-multi-line-switch-sections-together) | `ZA0012` |
| Multi-line switch expression arms | [ZA0015](https://github.com/Nickelony/Stanza/wiki/ZA0015-Separate-multi-line-switch-expression-arms) and [ZA0016](https://github.com/Nickelony/Stanza/wiki/ZA0016-Keep-multi-line-switch-expression-arms-together) | `ZA0015` |
| Multi-line initializer members | [ZA0018](https://github.com/Nickelony/Stanza/wiki/ZA0018-Separate-multi-line-initializer-members) and [ZA0019](https://github.com/Nickelony/Stanza/wiki/ZA0019-Keep-multi-line-initializer-members-together) | `ZA0019` |
| Multi-line collection elements | [ZA0021](https://github.com/Nickelony/Stanza/wiki/ZA0021-Separate-multi-line-collection-elements) and [ZA0022](https://github.com/Nickelony/Stanza/wiki/ZA0022-Keep-multi-line-collection-elements-together) | `ZA0022` |
| Multi-line arguments | [ZA0024](https://github.com/Nickelony/Stanza/wiki/ZA0024-Separate-multi-line-arguments) and [ZA0025](https://github.com/Nickelony/Stanza/wiki/ZA0025-Keep-multi-line-arguments-together) | `ZA0025` |
| Multi-line parameters | [ZA0027](https://github.com/Nickelony/Stanza/wiki/ZA0027-Separate-multi-line-parameters) and [ZA0028](https://github.com/Nickelony/Stanza/wiki/ZA0028-Keep-multi-line-parameters-together) | `ZA0028` |
| Multi-line declarators | [ZA0030](https://github.com/Nickelony/Stanza/wiki/ZA0030-Separate-multi-line-declarators) and [ZA0031](https://github.com/Nickelony/Stanza/wiki/ZA0031-Keep-multi-line-declarators-together) | `ZA0031` |
| A closing brace and the clause keyword after it | [ZA0033](https://github.com/Nickelony/Stanza/wiki/ZA0033-Separate-a-clause-keyword-from-the-brace-before-it) and [ZA0034](https://github.com/Nickelony/Stanza/wiki/ZA0034-Keep-clause-keywords-adjacent) | `ZA0034` |
| Multi-line markup elements | [ZA0101](https://github.com/Nickelony/Stanza/wiki/ZA0101-Separate-multi-line-markup-elements) and [ZA0102](https://github.com/Nickelony/Stanza/wiki/ZA0102-Keep-multi-line-markup-elements-together) | `ZA0102` |
| The arrow of an expression body | [ZA0044](https://github.com/Nickelony/Stanza/wiki/ZA0044-Keep-the-arrow-on-the-signature-line) and [ZA0045](https://github.com/Nickelony/Stanza/wiki/ZA0045-Put-the-arrow-on-the-line-after-the-signature) | `ZA0044`, or `ZA0045` when both are enabled |
| The slash of a self-closing tag | [ZA0207](https://github.com/Nickelony/Stanza/wiki/ZA0207-Separate-the-slash-of-a-self-closing-tag) and [ZA0208](https://github.com/Nickelony/Stanza/wiki/ZA0208-Keep-the-slash-of-a-self-closing-tag-tight) | `ZA0208` |

## When a pair judges one gap

- A file that enables one half of a pair makes that half the owner. The other half reports nothing over the gap.
- A file that enables both halves still gets one owner, so the gap is reported once.
  The owner is the default half, except in the arrow pair, as [the pair table](#pairs-and-their-default-halves) shows.
- In the analyzer, a file that enables neither half leaves the gap to the default half,
  which reports it only when that rule is on.
- A pragma over the owner hands the gap to the other half only when the file enables a half of the pair.
  Otherwise no rule reports the gap.
- A single-line [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms) covers only gaps where neither unit spans more than one line,
  so it never shares a gap with a separating rule.
- A rule without a partner owns its gap alone.
- The blank-line cap, [ZA0104](https://github.com/Nickelony/Stanza/wiki/ZA0104-Collapse-extra-blank-lines-between-markup-elements), shares its gaps with the markup tight rules
  as [`stanza_markup_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_max_blank_lines) says.
- A rule that defers a gap to its owner reports the gap itself where that owner is off.
  For example, the grouping rules defer a gap beside a multi-line unit to [ZA0004](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-multi-line-constructs).

## A comment or a directive in a gap

- A comment directly above the second unit travels with it.
- A documentation comment travels with its declaration however many blank lines stand between them.
  A separating rule writes its blank line above the comment, and a tight rule removes the blank line above it,
  though `ZA0005` keeps a gap that holds a comment.
- A comment with a blank line between it and the unit keeps the gap as written, and so does a directive in the gap,
  except that `ZA0034` and `ZA0035` remove the blank lines before the comment.
- A region marker in the gap ends a run for the grouping rules,
  because [ZA0046](https://github.com/Nickelony/Stanza/wiki/ZA0046-Separate-the-region-marker-from-its-neighbors) writes a blank line beside it.
  Where ZA0046 is off, the run reaches across the marker.
- A blank line inside an inactive `#if` block separates nothing, so a run reaches across the block.

## A blank line a fix leaves behind

- A rule judges the gap as it stands, so a blank line that one fix writes stays when another fix changes the unit beside it.
- [ZA0040](https://github.com/Nickelony/Stanza/wiki/ZA0040-Use-an-expression-body-for-a-single-line-member) or [ZA0036](https://github.com/Nickelony/Stanza/wiki/ZA0036-Collapse-empty-blocks) can make a multi-line unit single-line,
  and the blank lines [ZA0004](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-multi-line-constructs) wrote beside it stay,
  because no rule removes a blank line between single-line members,
  except [ZA0005](https://github.com/Nickelony/Stanza/wiki/ZA0005-Keep-the-two-units-of-a-container-together) in a container of exactly two units.
- [ZA0038](https://github.com/Nickelony/Stanza/wiki/ZA0038-Remove-redundant-braces) can drop the braces above an `else` and leave a blank line before it,
  because [ZA0034](https://github.com/Nickelony/Stanza/wiki/ZA0034-Keep-clause-keywords-adjacent) owns only the gap after a closing brace.
