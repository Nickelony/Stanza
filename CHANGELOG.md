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

[Unreleased]: https://github.com/Nickelony/Stanza/commits/main
