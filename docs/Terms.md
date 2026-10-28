# Terms

This page defines the words the rule, option and guide pages use, such as a gap and a unit.

- **gap**: the space between two neighboring units, filled by a blank line, a line break or a space.
- **unit**: a piece of code or markup beside a gap, such as a statement, a member, the import section, an `if` chain or a tag.
- **separating rule**: a rule that adds a blank line between two neighboring units.
- **tight rule**: a rule that removes that blank line.
- **single-line tight rule**: a tight rule in no pair that judges a gap only when neither unit spans more than one line.
- **pair**: two rules that judge one gap in opposite ways, such as a separating rule and its tight rule.
- **grouping rules**: [`ZA0001`](https://github.com/Nickelony/Stanza/wiki/ZA0001-Separate-declaration-and-invocation-runs) to `ZA0004`, which separate a run from its neighbors
  and leave a gap beside a multi-line unit to [`ZA0005`](https://github.com/Nickelony/Stanza/wiki/ZA0005-Separate-multi-line-constructs).
- **rule family**: the rules one directory of the analyzer holds, such as the blank-line rules or the documentation rules.
- **run** of units: two or more neighboring lines or units of one kind, such as declarations, `//` comment lines or blank lines;
  the grouping rules treat a run of declarations or invocations as one block.
- **run** of a tool: one invocation of `stanza-markup` or `stanza-format`, which ends in one exit code.
- **loose run**: neighboring single-line statements of one block or switch section, each set apart from the next by a blank line and nothing else,
  where no other rule asks for that blank line.
  [`ZA0052`](https://github.com/Nickelony/Stanza/wiki/ZA0052-Gather-a-run-of-loose-statements) gathers one that holds too many.
- **view**: a file as one project compiles it, by that project's target framework and compilation symbols.
  A file that several projects compile has one view for each.
- **pass**: one round of fixes over the files, by `dotnet format`, a Fix All or `stanza-markup format`;
  a fix can leave work that only the next pass sees.
- **enable**: a file enables a rule when it sets that rule's own `dotnet_diagnostic.<id>.severity` to any severity but `none` and `default`.
  So do a `.globalconfig` line and a `<WarningsAsErrors>` entry; the rule's default and a bulk key do not.
  `stanza-markup` reads only the `.editorconfig` line, as [Markup configuration](https://github.com/Nickelony/Stanza/wiki/Markup-configuration#rules) says.
- **disable**: a file disables a rule by setting its severity to `none`, or a [bulk key](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#configuration) to `none`,
  or the project lists the rule in `<NoWarn>`.
  `stanza-markup` reads only the rule's own key, and `stanza_markup_enabled = false` disables a file for it.
- **silence**: a `#pragma warning disable` line silences a rule over a span of lines, and a file setting stays as it is.
- **off**: a rule is off at a place when it reports nothing there,
  because a setting disables it, a pragma silences it, or it ships off and nothing enables it.
- **default half**: the half of a pair that owns a gap in the analyzer when a file enables neither half.
- **slash shape**: the form of the slash in a self-closing tag, spaced (`<c />`) or tight (`<c/>`).
- **container**: a code body or file that holds units, such as a block, a type or a namespace,
  or a markup element with child elements.
- **leaf**: an element with no child elements.
- **profile**: the policy that decides which elements the markup rules see in one file type.
  `--profile`, a `stanza_markup_profile` entry or the file extension picks it.
- **reprint**: the rewrite of a documentation `<member>` body into the layout its options set.
  It runs only when [`stanza_markup_doc_reprint`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_doc_reprint) or `--reprint` asks for it.
- **failure**: a file `stanza-markup` could not read, parse or lay out, or would not write back in its declared encoding.
  It is a warning, except a defect of the tool, which is an error.
- **outcome**: what a run did with one file, such as changing it or failing on it.
- **warning** and **error**: the levels `stanza-markup` reports a problem at, above a note, which never fails the run.
  [Markup behavior](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#failures-and-warnings) says which problems fail a run.
  An analyzer rule's `warning` and `error` are severities, which `.editorconfig` sets.
- **tier**: a rule's default severity and whether it is on by default.
  The [analyzer readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md) gives each rule's tier in its **Ships as** column.
- **body-shape rules**: the rules that judge how many lines a member spans, to choose its block or expression body or to break its expression.
  [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#run-order) lists them.
- **tail**: the text after the last clause mark on a line.
- **fragment**: the text up to the first clause mark on the next line.
- **penalty**: the cost the documentation wrapper gives a line that is too short, a short tail or fragment, or a line that ends on a weak word;
  it picks the line breaks with the lowest total.
- **clause mark**: a comma, colon, semicolon, sentence-ending punctuation, an ellipsis,
  a closing parenthesis or bracket, or a word that ends with a dash.
  An opening parenthesis or bracket starts a separate tail inside the parenthetical.
