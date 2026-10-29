# Rule configurator

A static page that lists each Stanza rule and option, shows the code before and after, and writes the `.editorconfig` for the settings you pick.
It is published at <https://nickelony.github.io/Stanza/> by the [Pages workflow](../.github/workflows/pages.yml).
The first deployment needs a one-time setting in the repository: Settings, Pages, Source set to GitHub Actions.

```text
cd site
npm ci
npm run dev
```

`npm test` runs the extraction and `.editorconfig` tests, `npm run typecheck` checks the types, and `npm run build` writes `dist/`.
`npm run format` applies Prettier, and `npm run format:check` fails on code it would change.
`npm run lint` runs oxlint, which requires braces around the body of every `if` and loop; CI runs both checks.

## What the build reads

The build keeps no list of rules or options. It reads these files and fails with the name of any file it cannot parse:

| File | What the page takes from it |
| --- | --- |
| [`docs/rules`](../docs/rules) | The ID, the title, the tier line, the description, each `// Before` and `// After` example, and the bullets. |
| [`docs/options`](../docs/options) | The key, the default, the value table, the examples, and the rules the page names. |
| [Analyzer readme](../src/Nickelony.Stanza.Analyzers/README.md) | The groups, the gap text of each rule, the tier to check each page against, and the defaults of the keys. |
| [`docs/Markup-behavior.md`](../docs/Markup-behavior.md) | The names and extensions of the markup profiles. |
| [`MarkupDiagnostics.cs`](../src/Nickelony.Stanza.Markup.Core/MarkupDiagnostics.cs) | The rules `stanza-markup` reads. |
| [`icon/Stanza.png`](../icon/Stanza.png) | The logo in the header and the tab, and the image of a link preview. |

A link in these files must be an `https://` address, and the build fails on any other target, on an image, and on bold written with underscores.

The first column of an option's value table decides its input:
literal values make a dropdown, `a number from 1 to 64` makes a bounded number, a comma separated list makes text,
and `a profile name` makes a dropdown of the profiles.

## What it writes

- A rule left at its default writes no line,
  and a rule that is off by default is enabled by a `dotnet_diagnostic.<id>.severity` line, as [Terms](https://github.com/Nickelony/Stanza/wiki/Terms) says.
- A key writes a line only when its value differs from the default its page or the readme gives.
- A page whose `Default:` line gives the tool another default, such as `stanza_markup_scope`, has no default in the markup section.
  Any value you pick for it goes there.
- `stanza-markup` reads its own section, so a rule it reads and a key it reads also go under the extensions of its profiles.
- The **Markup section** checkbox leaves that section out.
  When a key only the tool would have written is changed, a comment at the top says so.
- A key that picks a profile for each file, such as `stanza_markup_profile`, gets a comment above its line,
  because the section matches every markup file.
- **Every setting** writes each rule and each key with a literal default, the rules that are off by default as comments.
- The page keeps the configuration in the address hash, and the theme and the **Mark changes** choice in local storage.
  Without the marks, the preview shows the code alone, with a blank line as an empty line.
