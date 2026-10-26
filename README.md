# <img width="32" height="32" alt="Stanza" src="https://github.com/user-attachments/assets/f7516968-7215-490b-b715-528c46e00a82" /> Stanza

*Where code becomes poetry.*

Roslyn analyzers and code fixes for the line layout of C# source,
plus command line tools for the markup files analyzers cannot see and for the order the formatters run in.
A *stanza* is a group of lines set apart by blank lines,
and the packages lay out those groups.

[The analyzer readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md) has the rule table,
the severity tiers and the configuration keys.

## Analyzers

```text
dotnet add package Nickelony.Stanza.Analyzers
dotnet format analyzers --severity info
```

The second command applies the fixes of every analyzer in the project, the suggestions included.
The analyzer package is a development dependency:
it ships analyzers and code fixes and adds nothing to the consuming project's output.

The fixes add or remove blank lines between neighboring units, as here:

```csharp
// Before
var options = BuildOptions();
logger.LogInformation("Bound {Count} options.", options.Count);
logger.LogInformation("Starting the host.");
return options;

// After
var options = BuildOptions();

logger.LogInformation("Bound {Count} options.", options.Count);
logger.LogInformation("Starting the host.");

return options;
```

The analyzer reads C# source, and its [readme](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#installation) states the Roslyn version it needs.

## Markup

```text
dotnet tool install --global Nickelony.Stanza.Markup
stanza-markup check . --strict
```

The separate tool [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) applies the same gap rules to markup files.
Its [behavior](https://github.com/Nickelony/Stanza/wiki/Markup-behavior) and [command reference](https://github.com/Nickelony/Stanza/wiki/Markup-command-reference) pages cover the profiles,
what the tool leaves alone, and the build output.

## Format

```text
dotnet tool install --global Nickelony.Stanza.Format
stanza-format
```

The tool [`stanza-format`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Format/README.md) runs `dotnet format`, CSharpier and `stanza-markup` in the order the rules need,
over the files git reports as changed or the whole solution.

## Building

[CONTRIBUTING.md](https://github.com/Nickelony/Stanza/blob/main/CONTRIBUTING.md) has the build, test and formatting steps.

## See also

- [Documentation wiki](https://github.com/Nickelony/Stanza/wiki) holds the guides and the reference pages.
- [Changelog](https://github.com/Nickelony/Stanza/blob/main/CHANGELOG.md)
- [Security policy](https://github.com/Nickelony/Stanza/blob/main/SECURITY.md)
- [License](https://github.com/Nickelony/Stanza/blob/main/LICENSE)
