# Architecture

The repository holds the C# analyzers, the `stanza-markup` and `stanza-format` tools, and the markup engine they share.
This page maps which code does what, for a contributor who changes a rule or a tool.

## Projects

- `src/Nickelony.Stanza.Analyzers` holds the Roslyn analyzers and the descriptors.
  Its project compiles the `Markup.Core` sources but `Formatting/` into its own assembly,
  so the analyzer and the tool share one documentation-comment layout.
  A new file or directory of `Markup.Core` joins the analyzer too, unless the analyzer's project excludes it, like `Formatting/`.
- `src/Nickelony.Stanza.Analyzers.CodeFixes` holds the code fixes.
  They sit in an assembly of their own because they need Roslyn Workspaces, which the compiler does not provide to analyzers.
  They hold the providers and what the providers share; a family's builders, such as `BraceEdits`, stay beside its analyzers.
- `src/Nickelony.Stanza.Analyzers.Package` holds no code and builds no assembly.
  It packs the analyzer and code fix assemblies into the `Nickelony.Stanza.Analyzers` package,
  with the `build/` targets and presets a consumer's build imports.
- `src/Nickelony.Stanza.Markup.Core` holds the markup engine, one type per file, in layers that each use only the ones before them.
  The root holds the primitives, `DocComments/` lays out documentation comments, `Options/` reads `.editorconfig` values,
  and `Xml/` parses and measures markup. `Formatting/` holds the formatter only `stanza-markup` uses.
- `src/Nickelony.Stanza.Markup` holds the `stanza-markup` command line.
  `Cli/` holds the commands and reports, `Collection/` the file collection,
  `Configuration/` the `.editorconfig` reading and the profiles, and `Notices/` the reported problems.
  `Paths/` holds the glob matching and report paths, and `Files/` the file input and output.
- `src/Nickelony.Stanza.Format` holds the `stanza-format` command line, where `FormatPlanner` plans the steps of a run and `FormatRunner` runs them.
  `Planning/` turns the options and the repository's facts into the steps, `Running/` reads the facts and runs the steps,
  and `Cli/` reads the command line and drives a run.
- [CONVENTIONS.md](https://github.com/Nickelony/Stanza/blob/main/CONVENTIONS.md) A11 owns the layering rule and takes this order from here.
  In the engine it is the root, `DocComments/`, `Options/`, `Xml/` and `Formatting/`, in `stanza-markup` `Paths/`, `Notices/` and `Files/`,
  then `Configuration/`, `Collection/` and `Cli/`, and in `stanza-format` `Planning/`, `Running/` and `Cli/`.
- `src/Shared` holds the console host, the argument reader, the file-failure filters, the path helpers and the extensions the markup profiles claim,
  which the tools compile in outside `Markup.Core`, so the analyzer never carries them and `stanza-format` ships no engine.
- `tests/` holds the test projects. The guards that read the docs and the release files are tagged `Repository`.
  `Markup.Core.Tests` tests the engine alone, `Format.Tests` tests `stanza-format`,
  and `Markup.Tests` reaches the analyzer's copy of the engine through the `stanza` extern alias, because both copies declare the same types.
- `Analyzers.SelfCheck` compiles the analyzer and code fix sources under the analyzer-authoring rules that the shipping projects leave out.
- `Analyzers.Tests` mirrors the analyzer project, so a rule's tests sit in the test directory of its family.
  `Convergence/` fixes corpora until they settle,
  `Guards/` holds the `Repository` guards, named `<Subject>GuardTests`, with what they share and their tests,
  and `Harness/` the harness.
  Tests of several families stay at its root.

## Analyzer pipeline

- Each rule has an analyzer in the directory of its rule family, a descriptor in `DiagnosticDescriptors.cs`,
  and one provider in the `CodeFixes` project that fixes it, unless it is a [report-only rule](https://github.com/Nickelony/Stanza/wiki/Terms).
  A directory holds the analyzers of one rule family and the helpers only that family uses.
- A provider is `<Family>CodeFixProvider` for the directory of its rules, as `DocumentationCodeFixProvider` is for `Documentation/`,
  and holds a `FixTable` row for each.
- A rule whose `StanzaRules` entry carries a `GapFix` is fixed by `BlankLineCodeFixProvider`, wherever its analyzer stands,
  so it has no row in the provider of its own directory.

| Rule IDs | Directory | Fix provider |
| --- | --- | --- |
| `ZA0001`-`ZA0017`, `ZA0051`-`ZA0056`, `ZA0101`-`ZA0173`, `ZA0201`-`ZA0203` | `BlankLines/` | `BlankLineCodeFixProvider` |
| `ZA0198`-`ZA0199` | `Commas/` | `CommaCodeFixProvider` |
| `ZA0204`-`ZA0206`, `ZA0601`-`ZA0602` | `Braces/` | `BraceCodeFixProvider` |
| `ZA0301`-`ZA0307` | `Bodies/` | `BodyCodeFixProvider` |
| `ZA0401`-`ZA0405` | `Regions/` | `RegionCodeFixProvider` |
| `ZA0501`-`ZA0503` | `Arguments/` | `ArgumentCodeFixProvider` |
| `ZA0651`-`ZA0652` | `Expressions/` | `ExpressionCodeFixProvider` |
| `ZA0901`-`ZA0904`, `ZA0906`-`ZA0914` | `Documentation/` | `DocumentationCodeFixProvider` |
| `ZA0905` | `Documentation/` | `BlankLineCodeFixProvider` |
| `ZA0951`-`ZA0953` | `Comments/` | `CommentCodeFixProvider` |
| `ZA1001`-`ZA1007` | `Limits/` | None, because the rules only report |
| `ZA2001`-`ZA2006` | `MarkupLiterals/` | `BlankLineCodeFixProvider` |
| `ZA2007`-`ZA2010` | `MarkupLiterals/` | `MarkupLiteralCodeFixProvider` |

`AnalyzerStructureGuardTests` checks each row against the analyzers and the providers, so a new rule fails it until its row is right.

The [analyzer readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md) groups the rules for a reader,
and its groups and these directories do not line up one to one.
"Clauses and braces" spans `BlankLines/` and `Braces/`, "Statement simplification" is in `Braces/`,
and the trailing-comma pair of "Delimited lists" is in `Commas/`.

- An analyzer derives from a Stanza base, as [CONVENTIONS.md](https://github.com/Nickelony/Stanza/blob/main/CONVENTIONS.md) A4 says;
  each base skips generated code.
- The root of the analyzer project, beside `DiagnosticDescriptors.cs`, holds the bases, the registry and the helpers that several families share,
  and none of them reads a family directory.
- A diagnostic carries its fix as data: the analyzer stores what the fix reads as `FixOptions`,
  and the provider runs the `FixRow` of its ID in its `FixTable` on a `FixDocument`.
  A Fix All merges the change sets with `FixMerge`, which drops each set that conflicts with an earlier one.
- `StanzaRules` holds each rule whose facts shared code reads: its `RulePair`, the half that owns a shared [gap](https://github.com/Nickelony/Stanza/wiki/Terms) or construct, its fix,
  its node kinds and the rules it defers to.
  A rule deferred to has an entry too, and `StanzaOptions.RulesDisabledUnlessEnabled` answers for each entry.
- `StanzaRuleState` decides which half of a pair owns a gap or construct, and whether a rule is off at a position, including by a pragma.
- `StanzaOptions` reads the `.editorconfig` keys for a file once.
- `MarkupLiteral` reads a raw string for the markup rules, and `XmlLanguageComment` and `XmlStringSyntax` say whether it is marked as XML,
  which the `marked` value of [`stanza_markup_literals`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_literals) asks for.
- `DocCommentEdits` and the other `DocComment*` classes in `Documentation/` analyze documentation comments.
  The capital, period, keyword, padding and empty-element rules report the positioned changes `DocCleanup` records,
  the clean-ups the tool applies.
  The element order itself is `DocElementOrder.ElementOrder` in `Markup.Core`.

- `BlankLines/` holds the machinery its rules share, and reads a gap as a boundary, the index of the sibling below it.
- `BlankLineGaps` measures a gap and creates the changes that add or remove its blank lines,
  and `BlankLineSurplus` removes the blank lines past a cap.
- `BlankLineGuards` and `BlankLineJumps` read the guard clauses and jump statements that ask for a blank line above them.
- `BlankLineOwnership` reads which rule owns a gap and `BlankLineGrouping` the run borders of the grouping rules,
  while `BlankLineLooseRuns` and `BlankLineKeptRuns` read the [loose and kept runs](https://github.com/Nickelony/Stanza/wiki/Terms) of single-line siblings.

These files pass sets of boundaries around, and the names of the sets mean:

| Set | Boundaries |
| --- | --- |
| asked | Where a rule asks for a blank line whatever is gathered: the held ones, the standing runs and the jump gaps. |
| owned | Where the rules ahead of the asking rule own the gap, so the asker defers. |
| held | Where a rule keeps a blank line whether the runs stand or are gathered, such as the gap below the last import or below the guards. |
| standing | The run borders the grouping rules read from the blank lines as written. |
| gathered | The gaps a read treats as holding no blank line. |
| loose | Between single-line units, where the gap holds a blank line and nothing else. |
| removable | The loose ones whose blank line no rule asks for once tight, which a rule that gathers a loose run removes. |
| kept | The loose ones that are not held and lie inside a run the grouping rules read as one, less the section breaks and the moves the keep rules decline. |

## Markup pipeline

1. `MarkupFileCollector` expands the paths and globs into files, with `MarkupDirectoryWalker` walking each directory,
   and `MarkupConfiguration` resolves each file's keys through `EditorConfigReader` and `EditorConfigChain`.
2. `MarkupSource` reads the bytes, `MarkupEncodings` resolves the encoding from the mark, the declaration or the bytes,
   and `MarkupAtomicWrite` writes back through a temporary file.
3. `MarkupParser` parses the document, and `MarkupProfiles` gives the policy of the file's profile.
4. `MarkupAnalysis` finds the gaps the rules judge, and `MarkupFormatter` applies the edits.
   `MarkupReprint` and `DocCleanup` rewrite `<member>` bodies with the documentation layout.
5. `MarkupTool` runs each command, and a `MarkupReporter` prints the findings for a person or for MSBuild.
   `MarkupCommandLine` parses the options and holds the usage text.

