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
  The opt-in rules `ZA0010`, `ZA0012` and `ZA0057` to `ZA0064` separate checked stanzas and receiver groups from their neighbors.
  They also keep a binding with the guard, loop, jump or assertion that reads it, a chain of guards, and the operations of one receiver together.
  The rules `ZA0007` and `ZA0015` separate runs of `yield return` statements and of single-line local functions from their neighbors,
  `ZA0204` removes the blank line below a switch label, and `ZA0403` separates a `#pragma warning` suppression block from its neighbors.
  `ZA0404` removes the blank lines inside a region, a conditional block or a suppression block whose padding key says `tight`.
  The options `stanza_region_padding`, `stanza_conditional_block_padding` and `stanza_suppression_block_padding`
  choose whether the rule that separates each kind also pads its inside.
  The options `stanza_receiver_roots`, `stanza_receiver_comparison`, `stanza_declaration_shapes`, `stanza_declaration_typing`
  and `stanza_local_function_modifiers` choose which receivers, declarations and local functions they group.
  `ZA1101` reports a `stanza_` key that no rule or tool reads, such as a misspelled one, and names the key it most likely means;
  every level but `none` enables it.
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
