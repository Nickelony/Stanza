# Which rule owns which gap

Each [gap](https://github.com/Nickelony/Stanza/wiki/Terms) has at most one [owner](https://github.com/Nickelony/Stanza/wiki/Terms).
[The analyzer readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md) lists the rules with their tiers;
this page maps a gap to the rule that owns it.
A [report-only rule](https://github.com/Nickelony/Stanza/wiki/Terms) judges the shape of a statement, not a gap, so no row names it.

## Gaps and owners

| Gap | Owner |
| --- | --- |
| A blank line beside a brace, including the one between a clause header and its `{`, but not the one above a block that follows a sibling | [ZA0203](https://github.com/Nickelony/Stanza/wiki/ZA0203-Remove-brace-padding) |
| The blank line above a comment that leads the second of two single-line units | [ZA0012](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling), where it is on; the grouping and import rules defer to it there, and it yields to the single-line tight rule of a delimited list and to [ZA0051](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) in a container of two units |
| A gap with a multi-line neighbor, such as the unit before a multi-line construct or the unit after its `}` | [ZA0001](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) |
| Runs of declarations, invocations, assertions, await statements, event subscriptions and assignments, members of one kind, members that share their access, `static` and `readonly` modifiers, and declarations of one type | [ZA0002](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs), [ZA0004](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs), [ZA0005](https://github.com/Nickelony/Stanza/wiki/ZA0005-Separate-assertion-runs), [ZA0006](https://github.com/Nickelony/Stanza/wiki/ZA0006-Separate-await-runs), [ZA0007](https://github.com/Nickelony/Stanza/wiki/ZA0007-Separate-event-subscription-runs), [ZA0008](https://github.com/Nickelony/Stanza/wiki/ZA0008-Separate-assignment-runs), [ZA0013](https://github.com/Nickelony/Stanza/wiki/ZA0013-Separate-member-groups), [ZA0014](https://github.com/Nickelony/Stanza/wiki/ZA0014-Separate-modifier-groups) and [ZA0003](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-same-type-declaration-runs) |
| The import section, its directive groups, and the header of a file-scoped namespace | [ZA0015](https://github.com/Nickelony/Stanza/wiki/ZA0015-Separate-the-import-section), [ZA0016](https://github.com/Nickelony/Stanza/wiki/ZA0016-Separate-the-import-directive-groups) and [ZA0017](https://github.com/Nickelony/Stanza/wiki/ZA0017-Separate-a-file-scoped-namespace-declaration-from-its-first-member) |
| A region marker and the lines beside it | [ZA0401](https://github.com/Nickelony/Stanza/wiki/ZA0401-Separate-the-region-marker-from-its-neighbors) |
| The line above an `#if` and the line below its `#endif` | [ZA0402](https://github.com/Nickelony/Stanza/wiki/ZA0402-Separate-the-conditional-block-from-its-neighbors), where it is on |
| The blank line above a `return`, `break`, `continue`, `throw`, `goto` or `yield break` that follows a stanza of two or more lines | [ZA0009](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-a-jump-statement), where it is on; it leaves a gap that another row of this table owns to that owner |
| The blank line below the base call that opens a member body, and above the one that closes it | [ZA0010](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-the-base-call), where it is on; the run, jump and guard rules defer to it there, and it yields to `ZA0001` beside a multi-line statement, to `ZA0012` above a comment and to `ZA0051` in a body of two statements |
| The blank line below the guard clauses that open a block | [ZA0011](https://github.com/Nickelony/Stanza/wiki/ZA0011-Separate-the-guard-clauses), where it is on; it leaves a gap that another row of this table owns to that owner, and the gap above a jump to `ZA0009` where that rule asks for it |
| A blank line between two elements of a delimited list, such as the arguments of a call or the elements of a tuple | the pair for that list when an element spans more than one line, such as [ZA0151](https://github.com/Nickelony/Stanza/wiki/ZA0151-Separate-multi-line-arguments) and [ZA0152](https://github.com/Nickelony/Stanza/wiki/ZA0152-Keep-multi-line-arguments-together) for arguments, and its single-line tight rule otherwise, such as [ZA0153](https://github.com/Nickelony/Stanza/wiki/ZA0153-Keep-single-line-arguments-together) |
| A blank line between two units of a construct with rules of its own, such as accessors | the rules for that construct, such as [ZA0101](https://github.com/Nickelony/Stanza/wiki/ZA0101-Separate-multi-line-accessors) and [ZA0102](https://github.com/Nickelony/Stanza/wiki/ZA0102-Keep-multi-line-accessors-together) for multi-line accessors and [ZA0103](https://github.com/Nickelony/Stanza/wiki/ZA0103-Keep-single-line-accessors-together) for single-line ones |
| A blank line between a closing brace and the clause keyword that continues it | [ZA0201](https://github.com/Nickelony/Stanza/wiki/ZA0201-Separate-a-clause-keyword-from-the-brace-before-it) and [ZA0202](https://github.com/Nickelony/Stanza/wiki/ZA0202-Keep-clause-keywords-adjacent) |
| Whitespace between the braces of an empty body | [ZA0204](https://github.com/Nickelony/Stanza/wiki/ZA0204-Collapse-empty-blocks) |
| The blank line in a container of two units, unless a conditional directive can change the count | [ZA0051](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) |
| The blank lines of a loose run of statements, except a gap another rule asks for or a container where a conditional directive can change the runs | [ZA0052](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements), where it is on; it leaves a container of two units to `ZA0051` where that rule is on |
| The blank lines of a loose run of members, on the same terms | [ZA0053](https://github.com/Nickelony/Stanza/wiki/ZA0053-Gather-a-run-of-loose-members), where it is on; a file splits between the two, with its top-level statements to `ZA0052` and the members below them to `ZA0053` |
| A blank line between two single-line neighbors of one run, which the separating rules read as one run and ask nothing of | [ZA0054](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) for statements and [ZA0055](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-members-together) for members, where each is on. They read a [kept run](https://github.com/Nickelony/Stanza/wiki/Terms), and leave a gap with a comment, a directive or a multi-line unit to its owner |
| How many blank lines in a row stand anywhere in C# code, including beside a comment or a directive | [ZA0056](https://github.com/Nickelony/Stanza/wiki/ZA0056-Collapse-extra-blank-lines), where it is on; the owner of the gap decides whether the gap holds a blank line, and this rule decides how many |
| The break between an expression body's arrow and its expression | [ZA0305](https://github.com/Nickelony/Stanza/wiki/ZA0305-Break-the-expression-off-the-signature-line) |
| The space before a trailing comment on a code line | [ZA0953](https://github.com/Nickelony/Stanza/wiki/ZA0953-Separate-a-trailing-comment-from-the-code) |
| The elements of a documentation comment | [ZA0901](https://github.com/Nickelony/Stanza/wiki/ZA0901-Lay-out-a-documentation-comment), [ZA0902](https://github.com/Nickelony/Stanza/wiki/ZA0902-Mark-documentation-paragraphs), [ZA0903](https://github.com/Nickelony/Stanza/wiki/ZA0903-Remove-documentation-padding), [ZA0904](https://github.com/Nickelony/Stanza/wiki/ZA0904-Lay-out-the-items-of-a-documentation-list), [ZA0913](https://github.com/Nickelony/Stanza/wiki/ZA0913-Order-the-elements-of-a-documentation-comment) and [ZA0914](https://github.com/Nickelony/Stanza/wiki/ZA0914-Order-the-items-of-a-documentation-list) |
| A documentation comment and the declaration it documents | [ZA0905](https://github.com/Nickelony/Stanza/wiki/ZA0905-Keep-a-documentation-comment-with-its-declaration) |
| The whitespace inside a documentation tag | [ZA0906](https://github.com/Nickelony/Stanza/wiki/ZA0906-Collapse-the-whitespace-inside-a-tag) |
| An element that holds nothing, written as a self-closing tag | [ZA0909](https://github.com/Nickelony/Stanza/wiki/ZA0909-Write-an-empty-element-as-a-self-closing-tag) in a documentation comment and [ZA2010](https://github.com/Nickelony/Stanza/wiki/ZA2010-Write-an-empty-markup-element-as-a-self-closing-tag) in markup |
| A comment marker and the text it touches, in a markup comment, a `//` comment or a `/* */` block | [ZA2007](https://github.com/Nickelony/Stanza/wiki/ZA2007-Separate-a-markup-comment-marker-from-its-text), [ZA0951](https://github.com/Nickelony/Stanza/wiki/ZA0951-Separate-a-single-line-comment-marker-from-its-text) and [ZA0952](https://github.com/Nickelony/Stanza/wiki/ZA0952-Separate-a-delimited-comment-marker-from-its-text) |
| Two markup elements inside a raw string literal | [ZA2001](https://github.com/Nickelony/Stanza/wiki/ZA2001-Separate-multi-line-markup-elements), [ZA2002](https://github.com/Nickelony/Stanza/wiki/ZA2002-Keep-multi-line-markup-elements-together), [ZA2003](https://github.com/Nickelony/Stanza/wiki/ZA2003-Keep-single-line-markup-elements-together) and [ZA2006](https://github.com/Nickelony/Stanza/wiki/ZA2006-Collapse-extra-blank-lines-between-markup-elements) |
| The blank line above a comment that leads the second of two single-line markup elements | [ZA2004](https://github.com/Nickelony/Stanza/wiki/ZA2004-Separate-a-commented-markup-element), where it is on and `ZA2003` is not; `ZA2003` owns the gap where it is on |
| The blank lines inside a markup element, beside its start tag and its end tag | [ZA2005](https://github.com/Nickelony/Stanza/wiki/ZA2005-Remove-markup-element-padding), where it is on; no other markup rule judges them |
| Two closing braces of nested containers | the .NET SDK's [`IDE2002`](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide2002), when `csharp_style_allow_blank_lines_between_consecutive_braces_experimental` is `false`; no Stanza rule owns the gap, since [`ZA0203`](https://github.com/Nickelony/Stanza/wiki/ZA0203-Remove-brace-padding) skips the pair |
| Two opening braces of nested containers | nothing: the rules leave it as it is |

## One class, two owners

The blank line after the fields belongs to [ZA0002](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs),
and the blank line after the properties belongs to [ZA0013](https://github.com/Nickelony/Stanza/wiki/ZA0013-Separate-member-groups).
The [ZA0013 page](https://github.com/Nickelony/Stanza/wiki/ZA0013-Separate-member-groups) shows the class before and after.

## One run of statements, several owners

The declaration, invocation, assertion, await, event subscription and assignment rules each own the runs of their own kind.
Where two of them ask for one gap, the one with the lower ID reports it, and the other reports it only where the lower is off:
[ZA0002](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs), [ZA0004](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs),
[ZA0005](https://github.com/Nickelony/Stanza/wiki/ZA0005-Separate-assertion-runs), [ZA0006](https://github.com/Nickelony/Stanza/wiki/ZA0006-Separate-await-runs),
[ZA0007](https://github.com/Nickelony/Stanza/wiki/ZA0007-Separate-event-subscription-runs) and [ZA0008](https://github.com/Nickelony/Stanza/wiki/ZA0008-Separate-assignment-runs).

Where [`stanza_assignment_targets`](https://github.com/Nickelony/Stanza/wiki/stanza_assignment_targets) is `split`,
a run of assignments also ends where the target kind changes between neighbors that write through different roots.
`ZA0008` owns that gap as it owns the others of its runs.

The await rule also changes what the invocation rule reads: where `ZA0006` is on, an awaited call is an await statement whatever method it calls,
and an awaited call of an assertion root stays an assertion.
The rules that read runs all read that state, so they agree on which run each statement belongs to.

A declaration on its own, directly above a run, takes its blank line from the rule of that run, and stays tight against the run when that rule is off.
It gets none above it when it is the first unit of its container or a blank line already stands between it and the run.

A single fix of the gap below such a declaration writes the gap above it too, so the fix order writes one text.
A Fix All writes each gap from its own diagnostic.

Where two grouping rules ask for the gaps on either side of one unit, and a run crosses one gap to reach the other,
the lower gap is reported once the upper one holds its blank line.
A blank line in the crossed gap could leave the unit alone and end the ask for the other gap, and the fix order would choose the text.

A gap directly below a waiting gap waits with it.
An upper gap that no fix can write or that a pragma silences holds nothing back.

## A blank line inside a run

A blank line between two single-line neighbors of one run has no owner among the separating rules, which write the gaps around a run.
[ZA0054](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) and [ZA0055](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-members-together) own it where they are on.
They read a [kept run](https://github.com/Nickelony/Stanza/wiki/Terms): [ZA0003](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-same-type-declaration-runs) over [ZA0002](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs) for declarations, the fields of a type among them, and [ZA0014](https://github.com/Nickelony/Stanza/wiki/ZA0014-Separate-modifier-groups) over [ZA0013](https://github.com/Nickelony/Stanza/wiki/ZA0013-Separate-member-groups) for members.

Their fix writes the blank lines the grouping rules then ask for,
and leaves a blank line that sets two [sections](https://github.com/Nickelony/Stanza/wiki/Terms) apart or that joining would only [move](https://github.com/Nickelony/Stanza/wiki/Terms).
The [ZA0054 page](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) states when its fix leaves a blank line.

The gather rules, [ZA0052](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) and [ZA0053](https://github.com/Nickelony/Stanza/wiki/ZA0053-Gather-a-run-of-loose-members), leave a container to `ZA0054` or `ZA0055` while that rule has a run in it,
and leave a blank line that rule keeps as a section break or [declines](https://github.com/Nickelony/Stanza/wiki/Terms) as written.

## Pairs and their default halves

The [default half](https://github.com/Nickelony/Stanza/wiki/Terms) owns the gap unless a file [enables](https://github.com/Nickelony/Stanza/wiki/Terms) only the other half,
or, in the arrow pair, enables both.
The table holds for the analyzer. In [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md),
[ZA2001](https://github.com/Nickelony/Stanza/wiki/ZA2001-Separate-multi-line-markup-elements) owns the markup gap unless a file disables it or enables `ZA2002`.
The tool applies neither half of the markup slash pair unless a file enables one.

| Gap or construct | Halves | Default half |
| --- | --- | --- |
| Multi-line accessors | [ZA0101](https://github.com/Nickelony/Stanza/wiki/ZA0101-Separate-multi-line-accessors) and [ZA0102](https://github.com/Nickelony/Stanza/wiki/ZA0102-Keep-multi-line-accessors-together) | `ZA0102` |
| Multi-line switch sections | [ZA0111](https://github.com/Nickelony/Stanza/wiki/ZA0111-Separate-multi-line-switch-sections) and [ZA0112](https://github.com/Nickelony/Stanza/wiki/ZA0112-Keep-multi-line-switch-sections-together) | `ZA0111` |
| Multi-line switch expression arms | [ZA0121](https://github.com/Nickelony/Stanza/wiki/ZA0121-Separate-multi-line-switch-expression-arms) and [ZA0122](https://github.com/Nickelony/Stanza/wiki/ZA0122-Keep-multi-line-switch-expression-arms-together) | `ZA0121` |
| Multi-line initializer members | [ZA0131](https://github.com/Nickelony/Stanza/wiki/ZA0131-Separate-multi-line-initializer-members) and [ZA0132](https://github.com/Nickelony/Stanza/wiki/ZA0132-Keep-multi-line-initializer-members-together) | `ZA0132` |
| Multi-line collection elements | [ZA0141](https://github.com/Nickelony/Stanza/wiki/ZA0141-Separate-multi-line-collection-elements) and [ZA0142](https://github.com/Nickelony/Stanza/wiki/ZA0142-Keep-multi-line-collection-elements-together) | `ZA0142` |
| Multi-line arguments | [ZA0151](https://github.com/Nickelony/Stanza/wiki/ZA0151-Separate-multi-line-arguments) and [ZA0152](https://github.com/Nickelony/Stanza/wiki/ZA0152-Keep-multi-line-arguments-together) | `ZA0152` |
| Multi-line parameters | [ZA0161](https://github.com/Nickelony/Stanza/wiki/ZA0161-Separate-multi-line-parameters) and [ZA0162](https://github.com/Nickelony/Stanza/wiki/ZA0162-Keep-multi-line-parameters-together) | `ZA0162` |
| Multi-line declarators | [ZA0171](https://github.com/Nickelony/Stanza/wiki/ZA0171-Separate-multi-line-declarators) and [ZA0172](https://github.com/Nickelony/Stanza/wiki/ZA0172-Keep-multi-line-declarators-together) | `ZA0172` |
| A closing brace and the clause keyword after it | [ZA0201](https://github.com/Nickelony/Stanza/wiki/ZA0201-Separate-a-clause-keyword-from-the-brace-before-it) and [ZA0202](https://github.com/Nickelony/Stanza/wiki/ZA0202-Keep-clause-keywords-adjacent) | `ZA0202` |
| Multi-line markup elements | [ZA2001](https://github.com/Nickelony/Stanza/wiki/ZA2001-Separate-multi-line-markup-elements) and [ZA2002](https://github.com/Nickelony/Stanza/wiki/ZA2002-Keep-multi-line-markup-elements-together) | `ZA2002` |
| The arrow of an expression body | [ZA0306](https://github.com/Nickelony/Stanza/wiki/ZA0306-Keep-the-arrow-on-the-signature-line) and [ZA0307](https://github.com/Nickelony/Stanza/wiki/ZA0307-Put-the-arrow-on-the-line-after-the-signature) | `ZA0306`, or `ZA0307` when both are enabled |
| The slash of a self-closing tag | [ZA0907](https://github.com/Nickelony/Stanza/wiki/ZA0907-Separate-the-slash-of-a-self-closing-tag) and [ZA0908](https://github.com/Nickelony/Stanza/wiki/ZA0908-Keep-the-slash-of-a-self-closing-tag-tight) | `ZA0908` |
| The comma after the last element of a list | [ZA0198](https://github.com/Nickelony/Stanza/wiki/ZA0198-Add-a-trailing-comma) and [ZA0199](https://github.com/Nickelony/Stanza/wiki/ZA0199-Remove-the-trailing-comma) | `ZA0198` |
| The slash of a self-closing markup element | [ZA2008](https://github.com/Nickelony/Stanza/wiki/ZA2008-Separate-the-slash-of-a-self-closing-markup-element) and [ZA2009](https://github.com/Nickelony/Stanza/wiki/ZA2009-Keep-the-slash-of-a-self-closing-markup-element-tight) | `ZA2008` |
| A test for `null` | [ZA0651](https://github.com/Nickelony/Stanza/wiki/ZA0651-Use-a-pattern-for-a-null-check) and [ZA0652](https://github.com/Nickelony/Stanza/wiki/ZA0652-Use-an-equality-operator-for-a-null-check) | `ZA0651` |

## When a pair judges one gap

- A file that enables one half of a pair makes that half the owner. The other half reports nothing over the gap.
- A file that enables both halves still gets one owner, so the gap is reported once.
  The owner is the default half, except in the arrow pair, as [the pair table](#pairs-and-their-default-halves) shows.
- In the analyzer, a file that enables neither half leaves the gap to the default half,
  which reports it only when that rule is on.
  At the default [level](https://github.com/Nickelony/Stanza/wiki/Presets), no line enables a half,
  so a pair reports its default half when the Moderate level holds it, and nothing otherwise.
- A pragma over the owner hands the gap to the other half only when the file enables a half of the pair.
  Otherwise no rule reports the gap.
- A single-line [tight rule](https://github.com/Nickelony/Stanza/wiki/Terms) covers only gaps where neither unit spans more than one line,
  so it never shares a gap with a separating rule.
- A rule without a partner owns its gap alone.
- The blank-line cap, [ZA2006](https://github.com/Nickelony/Stanza/wiki/ZA2006-Collapse-extra-blank-lines-between-markup-elements), shares its gaps with the markup tight rules
  as [`stanza_markup_max_blank_lines`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_max_blank_lines) says.
- Where [ZA2003](https://github.com/Nickelony/Stanza/wiki/ZA2003-Keep-single-line-markup-elements-together) is on, it removes the blank line above a comment that leads a single-line element,
  and [ZA2004](https://github.com/Nickelony/Stanza/wiki/ZA2004-Separate-a-commented-markup-element) writes nothing there.
  With only ZA2004 on, the cap cuts a longer run there back to the cap.
- A rule that defers a gap to its owner reports the gap itself where that owner is off.
  For example, the grouping rules defer a gap beside a multi-line unit to [ZA0001](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs).

## A comment or a directive in a gap

- A comment directly above the second unit travels with it.
- A documentation comment travels with its declaration however many blank lines stand between them.
  A separating rule writes its blank line above the comment, and a tight rule removes the blank line above it,
  though `ZA0051` keeps a gap that holds a comment.
- Where [ZA0012](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling) is on, it writes the blank line above a comment that stands directly above a single-line unit.
  The grouping and import rules defer to it there.
- The single-line tight rule of a delimited list wins that gap where it is on:
  it removes the blank line above the comment, and ZA0012 writes nothing in the list.
- [ZA0051](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) wins the gap of a container of exactly two units in the same way,
  but it leaves a gap that holds a comment as it is.
- A pragma that silences a tight rule over a gap hands the gap back to ZA0012.
- Where ZA0012 is off, the grouping and import rules judge that gap by their own terms, and a tight rule removes the blank line above the comment.
- A comment with a blank line between it and the unit keeps the gap as written, and so does a directive in the gap,
  except that `ZA0202` and `ZA0203` remove the blank lines before the comment.
- A comment or a directive in a gap ends a loose run for [ZA0052](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) and [ZA0053](https://github.com/Nickelony/Stanza/wiki/ZA0053-Gather-a-run-of-loose-members), which gather only gaps that hold nothing but whitespace.
  It ends a kept run for [ZA0054](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) and [ZA0055](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-members-together) in the same way, so a blank line above a comment stays.
- A region marker in the gap ends a run for the grouping rules,
  because [ZA0401](https://github.com/Nickelony/Stanza/wiki/ZA0401-Separate-the-region-marker-from-its-neighbors) writes a blank line beside it.
  Where ZA0401 is off, the run reaches across the marker.
- An `#if` or an `#endif` in the gap ends a run for the grouping rules in the same way,
  because [ZA0402](https://github.com/Nickelony/Stanza/wiki/ZA0402-Separate-the-conditional-block-from-its-neighbors) writes a blank line above the `#if` and below the `#endif`.
  Where ZA0402 is off, the run reaches across the directive.
- A commented sibling in the gap ends a run for the grouping rules,
  because [ZA0012](https://github.com/Nickelony/Stanza/wiki/ZA0012-Separate-a-commented-sibling) writes a blank line above the comment.
  A documentation comment ends it whatever blank lines stand between the comment and its unit.
  Where ZA0012 is off, the run reaches across the comment.
- A declaration that a commented sibling splits off from its run stands alone, so no grouping rule sets it apart from the unit after it.
- The same gaps end the [stanza](https://github.com/Nickelony/Stanza/wiki/Terms) that [ZA0009](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-a-jump-statement) reads above a jump,
  and so does a directive, which also keeps the gap above the jump as written.
  A comment between the statements of the stanza counts as one of its lines where ZA0012 is off.
- The blank line [ZA0011](https://github.com/Nickelony/Stanza/wiki/ZA0011-Separate-the-guard-clauses) asks for below the guards ends that stanza as well, where it is on.
- A gap beside a base call that [ZA0010](https://github.com/Nickelony/Stanza/wiki/ZA0010-Separate-the-base-call) asks for ends a run and a stanza, where it is on,
  so the rules that read them agree on one owner for the gap in every fix order.
  A pragma that silences ZA0010 at the gap leaves it inside the run.
- A blank line inside disabled text separates nothing, so a run reaches across it.

## A blank line a fix leaves behind

- A rule judges the gap as it stands, so a blank line that one fix writes stays when another fix changes the unit beside it.
- [ZA0302](https://github.com/Nickelony/Stanza/wiki/ZA0302-Use-an-expression-body-for-a-single-line-member) or [ZA0204](https://github.com/Nickelony/Stanza/wiki/ZA0204-Collapse-empty-blocks) can make a multi-line unit single-line,
  and the blank lines that stand beside it stay,
  because a rule removes a blank line between single-line members only where a project enables it.
- [ZA0051](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) removes a blank line between single-line members in a container of exactly two units,
  [ZA0053](https://github.com/Nickelony/Stanza/wiki/ZA0053-Gather-a-run-of-loose-members) in a loose run,
  and [ZA0055](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-members-together) in a run the grouping rules read as one.
  These rules skip the container where a conditional directive can change the count or the runs.
- [ZA0302](https://github.com/Nickelony/Stanza/wiki/ZA0302-Use-an-expression-body-for-a-single-line-member) and [ZA0304](https://github.com/Nickelony/Stanza/wiki/ZA0304-Use-an-expression-body-for-a-single-line-value-member) write the blank lines
  [ZA0001](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs) asks for beside the member they collapse, where that rule is on, so the fix order writes one text.
  They do the same for [ZA0101](https://github.com/Nickelony/Stanza/wiki/ZA0101-Separate-multi-line-accessors) beside an accessor.
- Beside a multi-line accessor they collapse, `ZA0302` and `ZA0304` remove the blank lines that
  [ZA0102](https://github.com/Nickelony/Stanza/wiki/ZA0102-Keep-multi-line-accessors-together) would remove, where it owns the gap.
- [ZA0204](https://github.com/Nickelony/Stanza/wiki/ZA0204-Collapse-empty-blocks) does it for `ZA0001` where it joins a block to the line of its header,
  and for the multi-line rule of the list where the block stands in a list unit.
- [ZA0601](https://github.com/Nickelony/Stanza/wiki/ZA0601-Remove-the-else-after-a-jump) and [ZA0602](https://github.com/Nickelony/Stanza/wiki/ZA0602-Merge-nested-if-statements) write the blank lines `ZA0001` asks for above and below the multi-line chain or pair they rewrite,
  where `ZA0001` is on.
  `ZA0601` also writes the one between the branch and the statements it frees, where a neighbor of that gap spans lines.
- Both leave a single-line chain tight, and every gap where `ZA0001` is off or a pragma silences it,
  so the text does not depend on which rule fixes first.
- Where these fixes write a blank line for a multi-line rule,
  they move a sibling that shares a line with the unit down to a line of its own, as that rule would.
- [ZA0206](https://github.com/Nickelony/Stanza/wiki/ZA0206-Remove-redundant-braces) takes the blank lines below a closing brace it drops, when an `else` or the `while` of a `do` follows.
  [ZA0201](https://github.com/Nickelony/Stanza/wiki/ZA0201-Separate-a-clause-keyword-from-the-brace-before-it) and [ZA0202](https://github.com/Nickelony/Stanza/wiki/ZA0202-Keep-clause-keywords-adjacent) then settle on the same text in either order.
  A blank line before an `else` that never had a brace stays.
