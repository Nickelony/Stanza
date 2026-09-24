# Troubleshooting

Each heading is a symptom. The text under it names the setting or the page that decides the outcome.

## A rule never reports

- Check its tier in the **Ships as** column of the [analyzer readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#rules).
  A rule that ships off reports nothing until a `dotnet_diagnostic.<id>.severity` line [enables](https://github.com/Nickelony/Stanza/wiki/Terms) it.
- A `none` severity disables a rule, and a `#pragma warning disable` silences it; [Disabling and silencing a rule](https://github.com/Nickelony/Stanza/wiki/Disabling-and-silencing-a-rule) covers both.
- In the analyzer, a file that enables neither half of a pair leaves the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) to the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
  If that half is off by default or disabled, nothing reports the gap.
  [Which rule owns which gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap) lists the owner of each gap.

## A build or `dotnet format` run misses a rule

The analyzer readme's [severity section](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#severity-and-enablement) says what a build reports,
when a build fails, and why `dotnet format` needs `--severity info`.

## `dotnet format` runs out of memory on a large class

The .NET SDK's own CA1822 fix, which marks members `static`, grows faster than the class, with or without Stanza.
In a measured run at `--severity info`, a class of 400 such methods took 0.7 GB, and one of 800 took 3 GB.
`stanza-format` applies every fix at that severity, so it meets the same limit.
Set `dotnet_diagnostic.CA1822.severity = none` in a section of `.editorconfig` that matches the file.

## A second pass changes more lines

A fix can leave work for a second [pass](https://github.com/Nickelony/Stanza/wiki/Terms);
[Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#fixes-that-open-work) explains the case.

## A rule and the formatter undo each other

[Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#layout-that-csharpier-undoes) says which rules disagree with the formatter.
Disable the rule as described on [Disabling and silencing a rule](https://github.com/Nickelony/Stanza/wiki/Disabling-and-silencing-a-rule),
or check whether an IDE style setting asks for the opposite shape.

## A documentation comment is not re-wrapped

[ZA0201](https://github.com/Nickelony/Stanza/wiki/ZA0201-Lay-out-a-documentation-comment) is the rule that re-wraps a documentation comment, and it is off by default; enable it.
The width comes from [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length).

## `stanza-markup` skips a markup file

- A directory scan takes only the extensions a profile claims, and it skips other files silently.
  Set `stanza_markup_profile` for the file, or name it with a glob and `--profile`, as in `stanza-markup check "src/**/*.resx" --profile xml`.
- A file named on the command line that no profile claims is skipped with a `note:` line that says why.
  The [profiles](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#profiles) explain the mapping.
- A file whose `.editorconfig` sets [`stanza_markup_enabled`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_enabled) to `false` is left out of the run.

## `stanza-markup` reports ZA0100 under `--format msbuild`

The tool could not read or parse the file. The [ZA0100 page](https://github.com/Nickelony/Stanza/wiki/ZA0100-Report-a-file-that-could-not-be-read-or-parsed) lists the causes.

## `stanza-markup` warns about a key it does not read

A misspelled `stanza_markup_` or `stanza_doc_` key, such as `stanza_doc_expnd`, is reported as a warning that names the key,
and the key has no effect.
Correct the spelling. [Markup configuration](https://github.com/Nickelony/Stanza/wiki/Markup-configuration) lists the keys.

## `stanza-markup` exits with 2

The run failed, or the command line could not be read. The [exit codes](https://github.com/Nickelony/Stanza/wiki/Markup-command-reference#exit-codes) say which.

## `stanza-format` refuses the layout with exit code 2

The tool manifest decides the layout, and a run stops before its first step when it cannot use CSharpier:

- `--layout csharpier` needs CSharpier in the tool manifest, so add it with `dotnet tool install csharpier`.
- A manifest that pins a CSharpier older than the [floor of a run](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#layout) fails a run that lays out with it,
  so update it with `dotnet tool update csharpier`, or pass `--layout whitespace`.

The [`--layout` section](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#layout) says which layout a run uses by default.

## `stanza-format` says a step still changed a file

A step that runs again stops at the [pass limit](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#steps) and says so when its last pass still changed a file.
Run the tool again; if each run keeps changing files, a rule and the formatter undo each other,
as [A rule and the formatter undo each other](https://github.com/Nickelony/Stanza/wiki/Troubleshooting#a-rule-and-the-formatter-undo-each-other) describes.
