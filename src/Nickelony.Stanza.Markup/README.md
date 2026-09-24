# Nickelony.Stanza.Markup

`stanza-markup` puts a blank line between two neighboring elements of a markup container as soon as either of them is multi-line under its [profile](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#profiles),
and it reports the files that do not follow that layout.

## Installation

The tool needs the .NET 8 runtime or a later one.

```text
dotnet tool install --global Nickelony.Stanza.Markup
stanza-markup check . --strict
```

As a local tool in a repository:

```text
dotnet new tool-manifest
dotnet tool install Nickelony.Stanza.Markup
dotnet stanza-markup check . --strict
```

## Example

Two multi-line siblings get a blank line between them:

```xml
<!-- Before -->
<root>
  <child>
    text
  </child>
  <child>
    text
  </child>
</root>

<!-- After -->
<root>
  <child>
    text
  </child>

  <child>
    text
  </child>
</root>
```

Elements that each fit on one line stay tight:

```xml
<root>
  <add key="a" value="1" />
  <add key="b" value="2" />
</root>
```

## Commands

- `check` reports the files that are not formatted, and exits with `1` when there are any.
- `format` adds the missing blank lines, caps the gaps that are too long, and writes the files back.

The [command reference](https://github.com/Nickelony/Stanza/wiki/Markup-command-reference) lists the options, the exit codes and the build output.

## More

- [Markup behavior](https://github.com/Nickelony/Stanza/wiki/Markup-behavior) covers the profiles, what the tool changes and what it leaves alone.
- [Markup encoding](https://github.com/Nickelony/Stanza/wiki/Markup-encoding) covers how the tool reads and writes each file's encoding.
- [Markup configuration](https://github.com/Nickelony/Stanza/wiki/Markup-configuration) covers the `.editorconfig` keys and how a file resolves them.
- [Troubleshooting](https://github.com/Nickelony/Stanza/wiki/Troubleshooting) maps a symptom, such as a skipped file or exit code `2`, to the setting or page that decides it.
- [Analyzer readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md) covers the same layout rules for C# source.
