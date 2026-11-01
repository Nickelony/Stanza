# Troubleshooting

Each heading is a symptom. The text under it names the setting or the page that decides the outcome.

## A rule never reports

- Check its **Level** column in the [analyzer readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#rules).
  A rule above your `StanzaLevel`, or marked *Opt-in*, reports nothing until the level reaches it
  or a `dotnet_diagnostic.<id>.severity` line [enables](https://github.com/Nickelony/Stanza/wiki/Terms) it.
- A `none` severity disables a rule, and a `#pragma warning disable` silences it; [Disabling and silencing a rule](https://github.com/Nickelony/Stanza/wiki/Disabling-and-silencing-a-rule) covers both.
- In the analyzer, a file that enables neither half of a pair leaves the [gap](https://github.com/Nickelony/Stanza/wiki/Terms) to the [default half](https://github.com/Nickelony/Stanza/wiki/Terms).
  If that half is outside your level or disabled, nothing reports the gap.
  [Which rule owns which gap](https://github.com/Nickelony/Stanza/wiki/Which-rule-owns-which-gap) lists the owner of each gap.
- A level file states each rule, so a rule outside the `StanzaLevel` is off,
  and the `csharpier` formatter value also disables the rules CSharpier undoes.
  A rule's own line in an `.editorconfig` or a `.globalconfig` of the project wins over both, as the [presets page](https://github.com/Nickelony/Stanza/wiki/Presets#what-wins-over-a-level) says.

## The build fails with STANZA001

`StanzaLevel`, `StanzaSeverity` or `StanzaFormatter` holds a value its property does not take, and the error lists the values it does.
Case and surrounding spaces do not matter, so correct the spelling or remove the property.

## The build warns MultipleGlobalAnalyzerKeys

A `.globalconfig` of yours at the `global_level` of a preset file ties with it,
and Roslyn reports this warning, which fails a build under `TreatWarningsAsErrors`.
Give your file another `global_level`;
the [presets page](https://github.com/Nickelony/Stanza/wiki/Presets#what-wins-over-a-level) says which levels the preset files use.

## No Stanza rule reports

The compiler may be older than the [Roslyn floor](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#installation).
The package ships its analyzers in a directory named for the floor version, so an older compiler skips them,
and no rule reports in the IDE, in a build or in `dotnet format`.

- A build on such a compiler warns `STANZA002`, which names the Roslyn version it found,
  and fails where warnings are errors. The IDE and `dotnet format` print nothing, so check the versions below when a rule is silent there.
- Run `dotnet --version` in the project directory, because a `global.json` can pin an older SDK than the one installed.
  The [analyzer readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#installation) names the SDK and Visual Studio versions that meet the floor.
- Update the SDK, the pin or Visual Studio, then restore and build again.
- If the compiler is new enough, look at the settings of the rule: [A rule never reports](#a-rule-never-reports) lists them.

## A build or `dotnet format` run misses a rule

The IDE shows suggestions, but a build never reports them, and [`StanzaSeverity`](https://github.com/Nickelony/Stanza/wiki/Presets#the-properties) defaults to `suggestion`.
Set `<StanzaSeverity>warning</StanzaSeverity>` to have a build report the rules of the level.
The analyzer readme's [severity section](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#severity-and-enablement) says what a build reports,
when a build fails, and why `dotnet format` needs `--severity info`.

## A report stays after `dotnet format` ran

A [report-only rule](https://github.com/Nickelony/Stanza/wiki/Terms) has no code fix, so `dotnet format` prints `Unable to fix <ID>`.
Rewrite the code by hand, raise the limit that the rule's page names, or [disable the rule](https://github.com/Nickelony/Stanza/wiki/Disabling-and-silencing-a-rule).
The [format command reference](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#steps) says how `stanza-format` treats the report.

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
Set `<StanzaFormatter>csharpier</StanzaFormatter>` at the `strong` level, which disables the rules CSharpier undoes,
or disable the rule as described on [Disabling and silencing a rule](https://github.com/Nickelony/Stanza/wiki/Disabling-and-silencing-a-rule).
Also check whether an IDE style setting asks for the opposite shape.

## A documentation comment is not re-wrapped

[ZA0901](https://github.com/Nickelony/Stanza/wiki/ZA0901-Lay-out-a-documentation-comment) is the rule that re-wraps a documentation comment, and it is in the `strong` level; set that level or enable the rule.
The width comes from [`stanza_doc_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_doc_max_line_length).

## `stanza-markup` skips a markup file

- A directory scan takes only the extensions a profile claims, and it skips other files silently.
  Set `stanza_markup_profile` for the file, or name it with a glob and `--profile`, as in `stanza-markup check "src/**/*.resx" --profile xml`.
- A file named on the command line that no profile claims is skipped with a `note:` line that says why.
  The [profiles](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#profiles) explain the mapping.
- A file whose `.editorconfig` sets [`stanza_markup_enabled`](https://github.com/Nickelony/Stanza/wiki/stanza_markup_enabled) to `false` is left out of the run.

## `stanza-markup` reports ZA2901 under `--format msbuild`

The tool could not read or parse the file. The [ZA2901 page](https://github.com/Nickelony/Stanza/wiki/ZA2901-Report-a-file-that-could-not-be-read-or-parsed) lists the causes.

## `stanza-markup` warns about a key it does not read

A misspelled `stanza_` key, such as `stanza_doc_expnd` or `stanza_max_line_lenght`, is reported as a warning that names the key,
and the key has no effect.
Correct the spelling. Each key has an option page named after it, such as [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length).

## `stanza-markup` exits with 2

The run failed, or the command line could not be read. The [exit codes](https://github.com/Nickelony/Stanza/wiki/Markup-command-reference#exit-codes) say which.

## `stanza-format` refuses the layout with exit code 2

The tool manifest decides the layout, and a run stops before its first step when it cannot use CSharpier:

- `--layout csharpier` needs CSharpier in the tool manifest, so add it with `dotnet tool install csharpier`.
- A manifest that pins a CSharpier older than the [floor of a run](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#layout) fails a run that lays out with it,
  so update it with `dotnet tool update csharpier`, or pass `--layout whitespace`.

The [`--layout` section](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#layout) says which layout a run uses by default.

## `stanza-format --since` names no commit or no shared history

A CI checkout often fetches one commit, so the ref is missing or it joins HEAD only through history the clone lacks.
Fetch the history, such as with `git fetch --unshallow` or a fetch depth of `0`, and fetch the ref itself, such as `git fetch origin main`.
The [`--since` section](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#since-a-ref) says how a ref is read.

## `stanza-format` says the run did not settle

The [settle check](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#steps) that ends a write run found work a check would fail on.
A diagnostic that no fix can apply is fixed by hand, or its severity set below `info`.

A fix the run made and a later pass undid means a [rule and the formatter undo each other](https://github.com/Nickelony/Stanza/wiki/Troubleshooting#a-rule-and-the-formatter-undo-each-other).
`IDE2001` and CSharpier do so with an empty block body, as [IDE style rules](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#ide-style-rules) says.

## `stanza-format` says a step still changed a file

A step that runs again stops at the [pass limit](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#steps) and says so when its last pass still changed a file.
Run the tool again; if each run keeps changing files, a rule and the formatter undo each other,
as [A rule and the formatter undo each other](https://github.com/Nickelony/Stanza/wiki/Troubleshooting#a-rule-and-the-formatter-undo-each-other) describes.
