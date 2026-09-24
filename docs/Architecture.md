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
  It packs both assemblies into the `Nickelony.Stanza.Analyzers` package.
- `src/Nickelony.Stanza.Markup.Core` holds the markup engine, one type per file, in layers that each use only the ones before them.
  The root holds the primitives, `DocComments/` lays out documentation comments, `Xml/` parses and measures markup,
  and `Options/` reads `.editorconfig` values. `Formatting/` holds the formatter only the tools use.
- `src/Nickelony.Stanza.Markup` holds the `stanza-markup` command line.
  `Cli/` holds the commands and reports, `Collection/` the file collection,
  `Configuration/` the `.editorconfig` reading and the profiles, and `Notices/` the reported problems.
  `Paths/` holds the glob matching and report paths, and `Files/` the file input and output.
- `src/Nickelony.Stanza.Format` holds the `stanza-format` command line, where `FormatPlanner` plans the steps of a run and `FormatRunner` runs them.
  `Planning/` turns the options and the repository's facts into the steps, `Running/` reads the facts and runs the steps,
  and `Cli/` reads the command line and drives a run.
- `ConventionGuardTests` lets each engine and tool directory use only the earlier directories its table allows.
  In the engine the order is the root, `DocComments/`, `Xml/`, `Options/` and `Formatting/`, in `stanza-markup` `Paths/`, `Notices/` and `Files/`,
  then `Configuration/`, `Collection/` and `Cli/`, and in `stanza-format` `Planning/`, `Running/` and `Cli/`.
- `src/Shared` holds the console host, the argument reader, the path helpers and the extensions the markup profiles claim,
  which the tools compile in outside `Markup.Core`, so the analyzer never carries them and `stanza-format` ships no engine.
- `tests/` holds the test projects. The guards that read the docs and the release files are tagged `Repository`.
  `Markup.Core.Tests` tests the engine alone,
  and `Markup.Tests` reaches the analyzer's copy of the engine through the `stanza` extern alias, because both copies declare the same types.

## Analyzer pipeline

- Each rule has an analyzer in the directory of its rule family, a descriptor in `DiagnosticDescriptors.cs`,
  and one provider in the `CodeFixes` project that fixes it.
  A directory holds the analyzers of one rule family and the helpers only that family uses.
- An analyzer derives from a Stanza base, as [CONVENTIONS.md](https://github.com/Nickelony/Stanza/blob/main/CONVENTIONS.md) A4 says;
  each base skips generated code.
- The `Stanza*` helpers beside `DiagnosticDescriptors.cs` hold what several families share, and none of them reads a family directory.
- `StanzaRules` holds each rule whose facts shared code reads: its pair, the half that owns a shared [gap](https://github.com/Nickelony/Stanza/wiki/Terms), its fix,
  its node kinds and the rules it defers to.
  A rule deferred to has an entry too, and `StanzaOptions.RulesOffUnlessEnabled` answers for each entry.
- `StanzaRuleState` decides which half of a pair owns a gap, and whether a rule is off at a position, including by a pragma.
- `StanzaOptions` reads the `.editorconfig` keys for a file once.
- `DocCommentEdits` and the other `DocComment*` classes in `Documentation/` analyze documentation comments.
  The period, null keyword, padding and empty-element rules report the positioned changes `DocCleanup` records,
  the clean-ups the tool applies.
  The element order itself is `DocElementOrder.ElementOrder` in `Markup.Core`.

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

