# Presets

MSBuild properties pick the [level](https://github.com/Nickelony/Stanza/wiki/Terms) of analyzer rules, the severity they report at and the formatter the team runs:

```xml
<PropertyGroup>
	<StanzaLevel>strong</StanzaLevel>
	<StanzaSeverity>warning</StanzaSeverity>
	<StanzaFormatter>csharpier</StanzaFormatter>
</PropertyGroup>
```

Set them in a project file or in `Directory.Build.props`.
A value that names nothing fails the build with `STANZA001`, which lists the values the property takes.

## The properties

| Property | Values | Default |
| --- | --- | --- |
| `StanzaLevel` | `minimal`, `moderate`, `strong`, `none` | `moderate` |
| `StanzaSeverity` | `suggestion`, `warning`, `error` | `suggestion` |
| `StanzaFormatter` | `stanza-format`, `csharpier` | `stanza-format` |

- A project that sets none of them gets the `moderate` level at `suggestion`, which the analyzers ship, so that setting adds no file.
  The IDE shows the suggestions, and a build reports nothing.
- Each level enables its own rules and those of the levels before it, and the **Level** column of the
  [analyzer readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#rules) names the first level of each rule.
  A rule marked *Opt-in* or *No level* is in no level, so only its own line enables it.
- `StanzaSeverity` sets the severity of each rule the level enables. A build reports only `warning` and `error`,
  and under `TreatWarningsAsErrors` a warning fails it, so run the [fixes](https://github.com/Nickelony/Stanza/wiki/Adopting-the-analyzers) first.
- No level enables both halves of a [pair](https://github.com/Nickelony/Stanza/wiki/Terms).

## Which level

| Level | Enables | Pick it when |
| --- | --- | --- |
| `minimal` | Spacing hygiene that rarely fires, such as the blank-line cap, the import and namespace gaps and brace padding | You want the fewest changes. |
| `moderate` | Adds the blank lines around multi-line constructs and runs, switch sections, consistent braces, `is null` and comment-marker spacing | You want the Stanza look without rewriting code. |
| `strong` | Adds taste and restructuring rules, documentation layout, argument names, limits and report-only rules | The team agrees on one full layout. |
| `none` | No rule | You enable rules one by one in `.editorconfig`. |

Start at `moderate`, and raise the level once its suggestions are fixed.
The higher the level, the more a codebase that was not written for it reports, so apply the fixes before the severity becomes `warning`.

## Which formatter value

The value says how the team formats, and it changes only the `strong` level, because only that level holds rules that CSharpier undoes.
At `minimal` and `moderate` both values enable the same rules.

`stanza-format` suits a team that formats with [`stanza-format`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Format/README.md), CSharpier or not.
The tool runs the Stanza fixes after CSharpier, so they write back the blank lines CSharpier drops,
and its check compares the layout with those blank lines aside.
It asks two things of the team:

- Turn off CSharpier format-on-save, or accept that a save drops the blank lines until the next run.
- Gate CI with `stanza-format --check`, not `csharpier check`, which fails on every run.

`csharpier` suits a team where CSharpier also runs on its own, as format-on-save or as `csharpier check` in CI.
At `strong` it disables each rule that [CSharpier undoes](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#layout-that-csharpier-undoes),
and turns on the tight half of each such pair, so no run of CSharpier reverts a fix.

Some rules can never settle beside CSharpier, such as [`ZA0199`](https://github.com/Nickelony/Stanza/wiki/ZA0199-Remove-the-trailing-comma),
because CSharpier writes back what they change or the rule has no fix.
They are in no level under either value, so they report only where a line of yours enables them.

## What wins over a level

- A rule's own `dotnet_diagnostic` line in an `.editorconfig` wins over the level.
- A `.globalconfig` of the project that sets no `global_level` wins too, because a level file sits at `global_level = -100`
  and the `csharpier` file at `-99`.
- A global file of your own at `-100` or `-99` ties with a preset file: Roslyn unsets each key the two share
  and reports the warning `MultipleGlobalAnalyzerKeys`, which names both files.
- A bulk key, such as `dotnet_analyzer_diagnostic.category-Formatting.severity`, reaches the rules that ship on while no level file is added.
  A level file states each rule, so with one a bulk key reaches no rule.

To drop or tune one rule of a level, set its own line:

```ini
[*.cs]
dotnet_diagnostic.ZA0305.severity = none
```

## Which tools read it

- A build and `dotnet format analyzers` read the properties, provided they sit in a project file or in `Directory.Build.props`.
- [`stanza-markup`](https://github.com/Nickelony/Stanza/wiki/Markup-configuration#rules) reads only `.editorconfig`,
  so a level does not reach it.
  No level holds the markup rules it shares with the analyzer, so their own lines there enable them in both.
