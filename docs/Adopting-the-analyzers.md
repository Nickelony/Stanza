# Adopting the analyzers

Adopting the analyzers in a codebase that already has code takes a few steps, and their order matters.

1. **Install the package.** Add the reference that the [analyzer readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#installation) shows.
2. **Run CSharpier once.** [Install CSharpier](https://csharpier.com/docs/Installation),
   then run `dotnet csharpier format .` once before the first fix, so the rules start from its layout.
   CSharpier is optional: `stanza-format --layout whitespace` or `--layout none` runs the fixes without it,
   as the [`--layout` section](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#layout) says.
   [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters) lists the layout CSharpier undoes.
3. **Choose the severities.** Start from the [starter `.editorconfig`](#starter-editorconfig),
   which raises the rules that are on by default to `warning`.
   [Severity and enablement](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#severity-and-enablement) says what each tier does.
   [Enable](https://github.com/Nickelony/Stanza/wiki/Terms) a rule that is off by default with its own `dotnet_diagnostic.<id>.severity` line;
   the [configuration sample](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#configuration) shows one.
4. **Resolve the IDE style rules.** The built-in style rules can prefer shapes the analyzers reject.
   The [IDE style rules](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#ide-style-rules) section lists the settings that matter.
5. **Apply the fixes.** Run the commands of the [run order](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#run-order),
   or [`stanza-format --all`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Format/README.md),
   which runs them in turn.
   Run them again, because a fix can leave work for a second pass ([details](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#fixes-that-open-work)).
6. **Gate the result in CI.** [`stanza-format --check`](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#check) fails a run when a step would change a file.
   With a rule enabled that CSharpier undoes, such as [`ZA0024`](https://github.com/Nickelony/Stanza/wiki/ZA0024-Separate-multi-line-arguments),
   a plain `csharpier check` fails on every run, because each tool rewrites the blank lines the other writes.
7. **Run the markup tool.** Install it as the [markup readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md#installation) shows,
   run `stanza-markup format .` once, and gate with `stanza-markup check . --strict`.
   The [command reference](https://github.com/Nickelony/Stanza/wiki/Markup-command-reference) lists the options.

`dotnet format analyzers --severity info` applies the fixes of every analyzer in the project.
To apply only Stanza's, name its rules, as in `dotnet format analyzers --diagnostics ZA0002 ZA0004 --severity info`.

Commit the formatter's changes before the rules' changes, so each diff stays readable.

## Starter `.editorconfig`

Add this section to the `.editorconfig` of the solution:

```ini
[*.cs]
# Report each rule that is on by default in a build, suggestions included.
dotnet_analyzer_diagnostic.category-Formatting.severity = warning
```

A project with no gap to fix builds clean under it, and a gap is a warning until step 5 has fixed it.
Under `TreatWarningsAsErrors` that warning fails the build, so run step 5 before the setting reaches CI.
The [configuration keys](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#configuration) list the other lines a file can set.
