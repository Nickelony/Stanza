# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- [`Nickelony.Stanza.Analyzers`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md): C# formatting analyzers, each with a code fix, shipped as a NuGet package.
- [`Nickelony.Stanza.Markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md): the `stanza-markup` dotnet tool,
  which checks and formats markup documents from the command line and can report its findings as MSBuild diagnostics.
- [`Nickelony.Stanza.Format`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Format/README.md): the `stanza-format` dotnet tool,
  which runs `dotnet format`, CSharpier and `stanza-markup` in the order the rules need, and checks a working tree without writing.
- [Rule configurator](https://nickelony.github.io/Stanza/): a web page that lists each rule and option with the code before and after,
  and writes the `.editorconfig` for the severities and values you pick.

### Fixed

- `ZA0050` and `ZA0051` leave an argument list that holds a directive as written,
  because a name written or removed in one view could bind an argument to a different parameter in another.
- `ZA0038` and `ZA0039` judge a block multi-line when a comment or a directive stands on a line that holds no statement,
  so a chain around such a block is braced throughout, not left half braced.
- `ZA0039` leaves a chain as written while any of its blocks must keep its braces, unless `csharp_prefer_braces` is `false`,
  because `IDE0011` braces a chain with one braced branch and the check never settled.
- `ZA0006` and `ZA0052` skip a container with a conditional directive among its units or around them,
  because its count of units and its runs can differ between views, and no layout cleared both.
- `ZA0052` gathers a long block to the same text under one fix at a time and under Fix All.

[Unreleased]: https://github.com/Nickelony/Stanza/commits/main
