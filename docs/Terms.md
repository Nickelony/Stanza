# Terms

This page defines the words the rule, option and guide pages use, such as a gap and a unit.
The entries sit under headings by topic.

## Gaps and units

- **gap**: the space between two neighboring units, filled by a blank line, a line break or a space.
- **unit**: a piece of code or markup beside a gap, such as a statement, a member, the import section, an `if` chain or a tag.
- **sibling**: a unit that shares its container with the unit beside it, such as a statement of the same block or an element of the same list.
- **container**: a code body or file that holds units, such as a block, a type or a namespace,
  or a markup element with child elements.
- **stanza**: neighboring statements of one block, switch section or file that no blank line separates.
  It spans from the first line of its first statement to the last line of its last,
  and [`ZA0009`](https://github.com/Nickelony/Stanza/wiki/ZA0009-Separate-a-jump-statement) judges the one above a jump.

## Rules and pairs

- **separating rule**: a rule that adds a blank line between two neighboring units.
- **tight rule**: a rule that removes that blank line.
- **single-line tight rule**: a `Keep single-line ... together` rule, a tight rule in no pair that judges the gaps between the elements
  of one construct, such as arguments or accessors, only when neither element spans more than one line.
  [`ZA0051`](https://github.com/Nickelony/Stanza/wiki/ZA0051-Keep-the-two-units-of-a-container-together) to `ZA0056` also remove blank lines, but none of them is one.
- **pair**: two rules that judge one gap or one construct in opposite ways, such as a separating rule and its tight rule.
- **owner**: the rule, or the half of a pair, that decides whether a gap holds a blank line. A gap has at most one,
  and [Which rule owns which gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap) maps each gap to its owner.
- **grouping rules**: [`ZA0002`](https://github.com/Nickelony/Stanza/wiki/ZA0002-Separate-declaration-runs) to [`ZA0008`](https://github.com/Nickelony/Stanza/wiki/ZA0008-Separate-assignment-runs),
  [`ZA0013`](https://github.com/Nickelony/Stanza/wiki/ZA0013-Separate-member-groups) and [`ZA0014`](https://github.com/Nickelony/Stanza/wiki/ZA0014-Separate-modifier-groups), which separate a run from its neighbors
  and leave a gap beside a multi-line unit to [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-multi-line-constructs).
- **report-only rule**: a rule that reports a shape and has no code fix, such as
  [`ZA1007`](https://github.com/Nickelony/Stanza/wiki/ZA1007-Break-up-a-long-ternary-chain).
  A pass of `dotnet format` lists its reports and changes no file, so the author rewrites the code by hand.
- **rule family**: the rules one directory of the analyzer holds, such as the blank-line rules or the documentation rules.
- **body-shape rules**: the rules that judge how many lines a member spans, to choose its block or expression body or to break its expression.
  [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#run-order) lists them.
- **value member**: a property, an indexer or an accessor.
  [`ZA0303`](https://github.com/Nickelony/Stanza/wiki/ZA0303-Use-a-block-body-for-a-multi-line-value-member) and [`ZA0304`](https://github.com/Nickelony/Stanza/wiki/ZA0304-Use-an-expression-body-for-a-single-line-value-member) judge their bodies,
  and [`ZA0301`](https://github.com/Nickelony/Stanza/wiki/ZA0301-Use-a-block-body-for-a-multi-line-member) and [`ZA0302`](https://github.com/Nickelony/Stanza/wiki/ZA0302-Use-an-expression-body-for-a-single-line-member) judge those of a method, a constructor, a destructor, an operator and a local function.

## Runs of units

- **run** of units: two or more neighboring lines or units of one kind, such as declarations, `//` comment lines or blank lines.
  The grouping rules treat a run of declarations, invocations, assertions, await statements, event subscriptions, assignments or members as one block.
- **loose run**: neighboring single-line statements or members, each set apart by a blank line and nothing else,
  where no other rule asks for it.
  A unit tight against a neighbor stays out.
  [`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) gathers one of statements and [`ZA0053`](https://github.com/Nickelony/Stanza/wiki/ZA0053-Gather-a-run-of-loose-members) one of members that holds too many.
- **kept run**: single-line statements or members that the grouping rules which are on read as one run, blank lines left out.
  The rule that reads two neighbors apart wins, so declarations of two types are two kept runs while [`ZA0003`](https://github.com/Nickelony/Stanza/wiki/ZA0003-Separate-same-type-declaration-runs) is on.
  [`ZA0054`](https://github.com/Nickelony/Stanza/wiki/ZA0054-Keep-a-run-of-statements-together) and [`ZA0055`](https://github.com/Nickelony/Stanza/wiki/ZA0055-Keep-a-run-of-members-together) keep one together.
- **section** of a kept run: at least two units of the run tight against each other, or a lone unit between two such sections.
  The blank line between two sections, a section break, stays as written.
  A lone unit beside one section is absorbed, so its blank line goes.
- **move**: a join of a kept run that removes no more blank lines than it writes beyond those the grouping rules already ask for,
  so it only moves a blank line.
- **decline**: a keep rule declines a move, so it reports nothing and leaves the blank lines of that run as written.

## Configuration

- **enable**: a file enables a rule when it sets that rule's own `dotnet_diagnostic.<id>.severity` to any severity but `none` and `default`.
  So do a `.globalconfig` line and a `<WarningsAsErrors>` entry; the rule's default and a bulk key do not.
  `stanza-markup` reads only the `.editorconfig` line, as [Markup configuration](https://github.com/Nickelony/Stanza/wiki/Markup-configuration#rules) says.
- **disable**: a file disables a rule by setting its severity to `none`, or a [bulk key](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#configuration) to `none`,
  or the project lists the rule in `<NoWarn>`.
  `stanza-markup` reads only the rule's own key, and `stanza_markup_enabled = false` disables a file for it.
- **silence**: a `#pragma warning disable` line silences a rule over a span of lines, and a file setting stays as it is.
- **off**: a rule is off at a place when it reports nothing there,
  because a setting disables it, a pragma silences it, or it ships off and neither a level nor a line enables it.
- **tier**: whether a rule ships on or off, at the severity `suggestion`.
  The [analyzer readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md) gives each rule's tier in its **Ships as** column.
- **level**: a set of rules that the `StanzaLevel` MSBuild property enables, each holding the rules of the one before it.
  The [presets page](https://github.com/Nickelony/Stanza/wiki/Presets) lists the levels, the default and which to pick.
- **opt-in**: a rule that is in no level, so only its own line enables it.
- **default half**: the half of a pair that owns a gap or a construct in the analyzer when a file enables neither half.

## Compilation

- **view**: a file as one project compiles it, by that project's target framework, compilation symbols and analyzers.
  A file that several projects compile has one view for each.
- **disabled text**: the text of a conditional-directive branch that the symbols of one view leave out,
  such as the body of an `#if DEBUG` in a release build. Another view can compile it as code.

## Tool runs

- **run** of a tool: one invocation of `stanza-markup` or `stanza-format`, which ends in one exit code.
- **pass**: one round of fixes over the files, by `dotnet format`, a Fix All or `stanza-markup format`;
  a fix can leave work that only the next pass sees.
- **failure**: a file `stanza-markup` could not read, parse or lay out, or would not write back in its declared encoding.
  It is a warning, except a defect of the tool, which is an error.
- **warning** and **error**: the severities `stanza-markup` reports a problem at, above a note, which never fails the run.
  [Markup behavior](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#failures-and-warnings) says which problems fail a run.
  An analyzer rule's `warning` and `error` are severities too, which `.editorconfig` sets.

## Markup

- **profile**: the policy that decides which elements the markup rules see in one file type.
  `--profile`, a `stanza_markup_profile` entry or the file extension picks it.
- **property element**: an element whose local name, the part after any namespace prefix, contains a dot, as in the `<Button.Content>` of XAML.
  A [profile](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#profiles) can skip everything inside one.
- **leaf**: an element with no child elements.
- **slash shape**: the form of the slash in a self-closing tag, spaced (`<c />`) or tight (`<c/>`).
- **reprint**: the rewrite of a documentation `<member>` body into the layout its options set.
  It runs only when [`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) or `--reprint` asks for it.

## Documentation wrapping

- **clause mark**: a comma, colon, semicolon, sentence-ending punctuation, an ellipsis,
  a closing parenthesis or bracket, or a word that ends with a dash.
  An opening parenthesis or bracket starts a separate tail inside the parenthetical.
- **tail**: the text after the last clause mark on a line.
- **fragment**: the text up to the first clause mark on the next line.
- **penalty**: the cost the documentation wrapper gives a line that is too short, a short tail or fragment, or a line that ends on a weak word;
  it picks the line breaks with the lowest total.
