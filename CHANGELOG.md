# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- [`Nickelony.Stanza.Analyzers`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md): C# formatting analyzers with code fixes, shipped as a NuGet package.
  The rules lay out blank lines, delimited lists, braces, member bodies, regions, conditional blocks, argument names,
  comments and the markup in raw string literals, simplify statements, write null checks in one style, and report code past a limit.
  A build on a compiler older than Roslyn 4.11, which loads no rule, warns `STANZA002`.
- Presets: `<StanzaLevel>` picks the `minimal`, `moderate` or `strong` level of analyzer rules, or `none`,
  and `<StanzaSeverity>` sets their severity.
  `<StanzaFormatter>csharpier</StanzaFormatter>` keeps the strong level clear of the layout CSharpier undoes.
  The `moderate` level ships on as suggestions.
- [`Nickelony.Stanza.Markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md): the `stanza-markup` dotnet tool,
  which checks and formats markup documents from the command line and can report its findings as MSBuild diagnostics.
- [`Nickelony.Stanza.Format`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Format/README.md): the `stanza-format` dotnet tool,
  which runs `dotnet format`, CSharpier and `stanza-markup` in the order the rules need, and checks a working tree without writing.
  `--since <ref>` scopes a run to the files changed since the branch left the ref.
- [Rule configurator](https://nickelony.github.io/Stanza/): a web page that lists each rule and option with the code before and after.
  It has pickers for the level, the formatter and the severity,
  and writes the MSBuild properties and the `.editorconfig` for the rules and values you set.
- The configurator imports an existing `.editorconfig` and warns when both halves of a rule pair are enabled.
  It hides the rules CSharpier undoes while **CSharpier wins** is picked.

[Unreleased]: https://github.com/Nickelony/Stanza/commits/main
