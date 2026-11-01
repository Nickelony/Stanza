# <img width="32" height="32" alt="Stanza" src="https://github.com/user-attachments/assets/f7516968-7215-490b-b715-528c46e00a82" /> Stanza

*Where code becomes poetry.*

Stanza lays out the blank lines of source files, and each product below sets the [stanzas](https://github.com/Nickelony/Stanza/wiki/Terms) of one kind of file apart:

- [C# analyzers](#analyzers) are Roslyn analyzers and code fixes for the line layout of C# source.
- [Markup](#markup) is the command line tool `stanza-markup`, for the markup files analyzers cannot see.
- [JavaScript and TypeScript](#javascript-and-typescript) has an ESLint plugin and a Stylelint config.
- [Python](#python) has a command line tool and a flake8 plugin.

## Analyzers

```text
dotnet add package Nickelony.Stanza.Analyzers
dotnet format analyzers --severity info
```

The second command applies the fixes of every analyzer in the project, the suggestions included.
The analyzer package is a development dependency:
it ships analyzers and code fixes and adds nothing to the consuming project's output.

A fresh install enables the Moderate level of rules as suggestions:
the IDE shows them and `dotnet format` fixes them, but a build reports nothing, so no build breaks.
MSBuild properties pick another level, the formatter the team runs and the severity:

```xml
<PropertyGroup>
	<StanzaLevel>moderate</StanzaLevel>
	<StanzaFormatter>stanza-format</StanzaFormatter>
	<StanzaSeverity>warning</StanzaSeverity>
</PropertyGroup>
```

The sample shows the defaults except the severity, and the `warning` severity makes a build enforce the level once the fixes have run.
The [presets page](https://github.com/Nickelony/Stanza/wiki/Presets) says which level and formatter to pick.

The fixes add or remove blank lines between neighboring units, as [`ZA0004`](https://github.com/Nickelony/Stanza/wiki/ZA0004-Separate-invocation-runs) does here:

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
That readme has the rule table, the level of each rule and the configuration keys,
and the [rule configurator](https://nickelony.github.io/Stanza/) writes the `.editorconfig` for the severities and values you pick.
Markup inside a C# raw string literal follows the [Markup](#markup) section.

### Formatter order

```text
dotnet tool install --global Nickelony.Stanza.Format
stanza-format
```

The tool [`stanza-format`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Format/README.md) runs `dotnet format`, CSharpier and `stanza-markup` in the order the rules need,
over the files git reports as changed or the whole solution.

## Markup

```text
dotnet tool install --global Nickelony.Stanza.Markup
stanza-markup check . --strict
```

The separate tool [`stanza-markup`](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Markup/README.md) applies the gap rules to markup files.
It lays out each file under a [profile](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#profiles) such as `xml`, `msbuild`, `slnx` or `xaml`,
and reads its `stanza_markup_*` keys and the severity of each markup rule from `.editorconfig`.
The [Markup](https://github.com/Nickelony/Stanza/wiki/Markup) page opens the behavior, configuration and command reference pages.

The C# analyzers share the markup rules but by default read only the literals that a C# file marks as XML;
[Markup behavior](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#what-it-changes) says how the two differ.

## JavaScript and TypeScript

The same gap rules lay out JavaScript and TypeScript.
None of these packages is published yet:

- [JS Stanza](https://github.com/Nickelony/Stanza/blob/main/js/README.md) is an ESLint plugin for JavaScript and TypeScript.
- [Stylelint config Stanza](https://github.com/Nickelony/Stanza/blob/main/js/stylelint-config/README.md) lays out the blank lines of CSS, SCSS and Emotion templates.

## Python

[Python Stanza](https://github.com/Nickelony/Stanza/blob/main/py/README.md) is a command line tool and a flake8 plugin for Python.
It lays out the blank lines with the same gap rules and names the arguments of calls.
The package is not published yet.

## Building

[CONTRIBUTING.md](https://github.com/Nickelony/Stanza/blob/main/CONTRIBUTING.md) has the build, test and formatting steps.

## See also

- [Documentation wiki](https://github.com/Nickelony/Stanza/wiki) holds the guides and the reference pages.
- [Changelog](https://github.com/Nickelony/Stanza/blob/main/CHANGELOG.md)
- [Security policy](https://github.com/Nickelony/Stanza/blob/main/SECURITY.md)
- [License](https://github.com/Nickelony/Stanza/blob/main/LICENSE)
