# Nickelony.Stanza.Format

`stanza-format` runs `dotnet format`, CSharpier and `stanza-markup` in the [run order](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#run-order)
that the [Stanza analyzers](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md) need,
so no tool undoes the lines another one wrote.

## Installation

The tool needs the .NET 8 runtime or a later one, git, and a .NET SDK that builds the solution.
The SDK must meet the floor in the [analyzer package installation](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#installation), because below it the Stanza rules never load.

```text
dotnet tool install --global Nickelony.Stanza.Format
stanza-format
```

As a local tool in a repository:

```text
dotnet new tool-manifest
dotnet tool install Nickelony.Stanza.Format
dotnet stanza-format
```

The Stanza rules run in the projects that reference the analyzer package.
CSharpier and `stanza-markup` are optional, and a run uses each one the repository's tool manifest lists;
the [command reference](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#layout) names the oldest CSharpier a run calls:

```text
dotnet tool install csharpier
dotnet tool install Nickelony.Stanza.Markup
```

## Usage

- `stanza-format` formats the files git reports as changed; the [scope](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#scope) says which tool takes each one.
- `stanza-format --all` formats the whole solution.
- `stanza-format --check` writes nothing, and exits with `1` when a step would change a file.

The path is a solution file or a directory that holds one, and the working directory is the default.
The [command reference](https://github.com/Nickelony/Stanza/wiki/Format-command-reference) lists the steps, the options and the exit codes.

## More

- [Working with formatters](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters) explains why the passes run in this order.
- [Troubleshooting](https://github.com/Nickelony/Stanza/wiki/Troubleshooting) maps a symptom, such as a refused layout or a step that still changed a file, to the setting or page that decides it.
- [Markup readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) covers `stanza-markup`.
