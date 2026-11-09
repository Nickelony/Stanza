# Adopting the analyzers

Adopting the analyzers in a codebase that already has code takes a few steps, and their order matters.

1. **Install the package.** Add the reference that the [analyzer readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#installation) shows.
   The rules of the Moderate level appear as suggestions in the IDE, and a build reports nothing,
   so `TreatWarningsAsErrors` can stay on.
2. **Run CSharpier once.** [Install CSharpier](https://csharpier.com/docs/Installation),
   then run `dotnet csharpier format .` once before the first fix, so the rules start from its layout.
   CSharpier is optional: `stanza-format --layout whitespace` or `--layout none` runs the fixes without it,
   as the [`--layout` section](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#layout) says.
   [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters) lists the layout CSharpier undoes.
3. **Choose a level and a formatter.** Set `StanzaLevel` and `StanzaFormatter` in the project or in `Directory.Build.props`,
   as the [presets page](https://github.com/Nickelony/Stanza/wiki/Presets) says, and leave the severity at `suggestion` for now.
   Tune a rule that does not fit with its own `dotnet_diagnostic.<id>.severity` line, as the [starter `.editorconfig`](#starter-editorconfig) shows.
4. **Resolve the IDE style rules.** The built-in style rules can prefer shapes the analyzers reject.
   The [IDE style rules](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#ide-style-rules) section lists the settings that matter.
5. **Apply the fixes.** Run the commands of the [run order](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#run-order),
   or [`stanza-format --all`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Format/README.md),
   which runs them in turn.
   Run them again, because a fix can leave work for a second pass ([details](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#fixes-that-open-work)).
6. **Raise the severity.** Set `<StanzaSeverity>warning</StanzaSeverity>`,
   so a build reports each gap the level finds and fails on it under `TreatWarningsAsErrors`.
   [Severity and enablement](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#severity-and-enablement) says what a build reports.
   Raise it after step 5, because the level reports every gap the fixes have not yet written.
7. **Gate the result in CI.** [`stanza-format --check`](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#check) fails a run when a step would change a file.
   A plain `csharpier check` fails on every run while a rule that CSharpier undoes is enabled, such as
   [`ZA0151`](https://github.com/Nickelony/Stanza/wiki/ZA0151-Separate-multi-line-arguments), because each tool rewrites the other's blank lines.
   The `csharpier` formatter value leaves those rules out.
8. **Run the markup tool.** Install it as the [markup readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md#installation) shows,
   run `stanza-markup format .` once, and gate with `stanza-markup check . --strict`.
   The [command reference](https://github.com/Nickelony/Stanza/wiki/Markup-command-reference) lists the options.
   Only their own `.editorconfig` lines enable the markup rules, since no level holds them.

`dotnet format analyzers --severity info` applies the fixes of every analyzer in the project.
To apply only Stanza's, name its rules, as in `dotnet format analyzers --diagnostics ZA0013 ZA0001 --severity info`.

Until the codebase is formatted, [`stanza-format --check --since origin/main`](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#since-a-ref)
gates the files a branch changed alone, and the CI checkout needs the history of that ref.

Commit the formatter's changes before the rules' changes, so each diff stays readable.

## Starter `.editorconfig`

The level and the severity belong to the project, so this section holds what the `.editorconfig` of the solution adds:

```ini
[*.cs]
# Match the print width of the formatter, so the rules that measure a width agree with it.
stanza_max_line_length = 100

# Disable one rule of the level for the whole solution.
dotnet_diagnostic.ZA0205.severity = none
```

A rule's own line wins over the level, as the [presets page](https://github.com/Nickelony/Stanza/wiki/Presets#what-wins-over-a-level) says.
The [configuration keys](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#configuration) list the other lines a file can set.
