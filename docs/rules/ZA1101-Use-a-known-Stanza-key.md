# ZA1101 - Use a known Stanza key

**Warning, on by default.**
Reports a `stanza_` key of the analyzer configuration that no Stanza rule or tool reads,
and names the known key it most likely means.
It is a [report-only rule](https://github.com/Nickelony/Stanza/wiki/Terms), so it has no code fix.

```ini
// Before - the key is misspelled, so the default width stays in force
[*.cs]
stanza_max_line_lenght = 120

// After - rewritten by hand, with the key the rules read
[*.cs]
stanza_max_line_length = 120
```

- The message names the key and, when a known key lies within three edits of it, adds that key as the nearest known key.
  An edit is an insertion, a deletion, a substitution or a swap of two neighbors, and a tie goes to the key that sorts first.
- A known key is one that has an option page, such as
  [`stanza_max_line_length`](https://github.com/Nickelony/Stanza/wiki/stanza_max_line_length),
  so the markup keys that only `stanza-markup` reads are known too.
- The rule judges the name of a key and never its value,
  so a value that a reader rejects falls back to its default without a report.
- A key is read as Roslyn reads it, in lower case,
  so `Stanza_Max_Line_Length` is known, and a misspelling in capitals is reported in lower case.
- A key that does not start with `stanza_` is never reported,
  whether it is a standard key such as `max_line_length` or a spelling such as `stanza-max-line-length`.
- The rule reads the keys that reach a C# file of the compilation, from the global configuration
  and from each section that matches the file.
  A section that matches no C# file, such as `[*.md]`, is never read,
  so a misspelled key there is left to the warning of `stanza-markup`.
- An analyzer cannot see the path or the line of a configuration file,
  and a diagnostic with no location ignores the severity lines of an `.editorconfig`.
  So the rule reports each key once for the compilation, at the first token of a source file the key reaches.
- A report says nothing of the file that holds the key, so search the configuration for the key the message names.
- The rule skips generated files, and picks the first file by path whose top holds no `#pragma warning disable` that names `ZA1101` or no ID.
  A pragma could still hide the report, but only when every file the key reaches holds one.
- The rule is a configuration check, which sits outside the [levels](https://github.com/Nickelony/Stanza/wiki/Presets),
  so every level, `none` included, and every `StanzaSeverity` leave it on at warning.
- A project turns it off with `dotnet_diagnostic.ZA1101.severity = none` in a `[*.cs]` section or in a `.globalconfig`.
- The rule is in the `Configuration` category, so `dotnet_analyzer_diagnostic.category-Formatting.severity` leaves it alone,
  and `category-Configuration` or the analyzer-wide `dotnet_analyzer_diagnostic.severity` key reaches it, because no level file names it.
