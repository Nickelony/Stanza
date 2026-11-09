# Rule configurator

A static page that lists each Stanza rule and option, shows the code before and after, and writes the `.editorconfig` for the settings you pick.
It is published at <https://nickelony.github.io/Stanza/> by the [Pages workflow](../.github/workflows/pages.yml).
The first deployment needs a one-time setting in the repository: Settings, Pages, Source set to GitHub Actions.

```text
cd js
npm ci
npm run build
cd ../site
npm ci
npm run dev
```

The site lints with [JS Stanza](../js/README.md), which it links from `js/`, so `js/` is built first.
oxlint loads the plugin, because typescript-eslint does not yet support the TypeScript 7 of the site.
A test keeps the rules of [`.oxlintrc.json`](.oxlintrc.json) equal to the `strong` preset and the opt-in rules that keep a run together.

`npm test` runs every test of the site, `npm run typecheck` checks the types,
and `npm run build` writes `dist/`, which `npm run preview` serves.
`npm run format` applies Prettier, which also sorts the imports into the groups of [`.prettierrc.json`](.prettierrc.json),
and `npm run format:check` fails on code it would change.

`npm run lint` runs oxlint and then `npm run lint:css`.
oxlint requires braces around the body of every `if` and loop, and the blank lines that JS Stanza asks for.
stylelint keeps the properties of [`src/style.css`](src/style.css)
and of the styled templates in [`src/components`](src/components) in SMACSS order.
`npm run lint:fix` applies the fixes of oxlint, the blank lines of JS Stanza among them,
and `npx stylelint --fix "src/**/*.{css,ts,tsx}"` reorders the properties.

The [Pages workflow](../.github/workflows/pages.yml) builds `js/`, then runs the format check, lint, typecheck, test and build
on a pull request, and on a push to `main`, that changes a file the build or the lint reads.

No page is a file on disk: [`build/render/pages.ts`](build/render/pages.ts) writes the HTML of each from one template,
with its own title, description, canonical address and `og:url`.
The Content-Security-Policy of every page allows the one inline theme script by the hash of its text,
and `npm test` checks each page against its policy.

## The page

The page is Preact, with [Emotion](https://emotion.sh) styled components that preact/compat answers for React.
A component renders one semantic element, so a button, a link or a heading is never a styled `div`.

[`src/style.css`](src/style.css) holds what the components share:
the color tokens, the reset, the base rules and the layout of the page.
Each component keeps its own rules, its forced-colors rules included, in the template beside its markup,
and the inline script of the template sets the theme before the first paint.

The state of the form lives in [`src/state/store.ts`](src/state/store.ts), which the components read through `useAppState`.
`npm test` runs in Node, and only a test that reaches a browser API asks for happy-dom.

The components read the language they show and its catalog from a context, so none of them names a language.

## Layout

The directories of `src` and `build` group the files by what they do, and `test` mirrors them:

- [`src/state`](src/state) holds the form, what each setting means for a writer, and the text the page shows of it.
- [`src/address`](src/address) encodes and decodes the address hash, and shortens and restores the links of the catalog.
- [`src/writers`](src/writers) writes the output of a page, and colors it for the browser.
- [`src/editorconfig`](src/editorconfig) reads an `.editorconfig` back into the form and lists the conflicts.
- [`src/languages`](src/languages) holds the descriptor of each language.
- [`src/entries`](src/entries) holds the script each page loads, which renders the components for its language.
- [`src/components`](src/components) draws the page, one directory per component.
- [`src/hooks`](src/hooks) holds the hooks that several components share, and [`src/util`](src/util) holds the small helpers.
- [`build/readers`](build/readers) reads the pages of each product into a catalog.
- [`build/render`](build/render) turns Markdown into HTML, highlights the code and writes the page of each language.

## Languages

The site has one page for each language and one for the markup rules, and the pages share every component:

| Page | Address | Output | The hash holds |
| --- | --- | --- | --- |
| C# | `/Stanza/` | The MSBuild properties and the `.editorconfig` | `level`, `formatter`, `severity`, `all`, `markup`, `sel`, and a rule ID or an option key with a value. |
| Markup | `/Stanza/markup/` | The `.editorconfig` | `all`, `markup`, `sel`, and a rule ID or an option key with a value. |
| JS/TS | `/Stanza/js/` | The `eslint.config.mjs` | `level`, `severity`, `sel`, and a rule name or an option key with a value, where a rule's own option is written `<rule>.<key>`. |
| Python | `/Stanza/py/` | The `[tool.stanza]` table, which downloads as `pyproject-stanza.toml` | `level`, `sel`, and a rule code or a key with a value. |

The links of the header switch between the pages and carry the picked level in the hash, which the Markup page ignores.
Python Stanza reports no severity, so its page has no severity picker, and a rule is enabled or disabled.

Only the C# page and the Markup page import an `.editorconfig` and list conflicts.
That code loads as a chunk of its own, which the other pages never fetch.
A language with `levelHints` of `null`, as the Markup page is, has no Level picker, no level hint and no level in the hash.

The build reads JS Stanza and Python Stanza into catalogs of the same shape, and two writers produce the config each of them reads.
The facts only the C# page and the Markup page hold, such as what CSharpier does to a rule and which tool reads a key,
sit in the `csharp` object of a rule or an option.
The JS and Python catalogs leave that object out.

A language is a descriptor of data and a few small functions ([`src/languages/language.ts`](src/languages/language.ts), [`src/languages`](src/languages)):
its pickers, its labels and the blocks of output it writes.
The reader of each language is registered in [`build/catalogs.ts`](build/catalogs.ts),
and the escaping, link compaction and Shiki highlighting run once for all of them.
Each catalog is the module `virtual:stanza-catalog/<language>`, so a page can load its own.

The JS and Python readers take the rules from the rule pages, and the levels and summaries from the readme table.
They take the keys and defaults from the code that owns them, and fail the build when the sources disagree.
A rule page links its neighbors by a relative path, which the reader turns into a GitHub address.
A link to another rule page selects that rule.

The JS and Python packages take a half of a pair out of the level when a file enables its opposite,
so their pages show that half as off.
The Python page writes it in `enable` only when the visitor enables both halves.
The JS page writes the entry as the level gives it, because ESLint keeps the half of the preset and the plugin gives the gap to the tight half.

The pairs of the JS and Python pages change no warning, because only the C# and Markup pages list conflicts.

The Python build fails when the pair table and the `opposite` of the rule modules describe other pairs,
or when the owner of a pair is not the half with the `keep` role.
The JS build fails when the separating half of a pair does not ask `context.isEnabled` about its tight half in its source,
or when a rule source asks about a rule that the pair table does not pair with it.

Both fail when the default half of a pair is not the one half in a level.

A JS or Python rule may have the level opt-in: no preset holds it, so only an entry of the project's own turns it on.
The JS reader fails the build when `optInRules` of `presets.ts` and the rule pages disagree.

A default written as a whole number makes a bounded number input, with the bounds that the code of the language gives.
A default written as a quoted word makes a dropdown of the values that the code names, and the build fails when the page lists others.

A Python list whose items the code closes to a set of words, such as `named-argument-sources`, takes only those words,
and the build fails the same way.

The effect cell of such a key names its values between backticks after its last colon,
and a whole number that the cell gives as `from X to Y` must equal the bounds of the code.

- [`src/writers/eslint-config.ts`](src/writers/eslint-config.ts) writes the `eslint.config.mjs` that loads a preset of JS Stanza,
  then the rules and options that differ from it, and `settings.stanza`.
- [`src/writers/pyproject.ts`](src/writers/pyproject.ts) writes the `[tool.stanza]` table of a `pyproject.toml`:
  the level, the rules to enable or disable by code, and the keys that differ from their defaults,
  so a `formatter` of `auto` writes no line.

Both writers escape the quote and the backslash of a value for their syntax.

## The Markup page

The markup rules `ZA2001` to `ZA2010` and the six `stanza_markup_*` keys that `stanza-markup` reads have a page of their own.
No level holds a markup rule, so the page has no Level, Severity or formatter picker, and its address holds no level.
The C# page keeps `stanza_markup_literals`, which picks the C# literals the analyzer reads, and each page names the other under its pickers.

The build reads one catalog and splits it in two in [`build/readers/markup.ts`](build/readers/markup.ts).
That file is the one place that names the markup rules.
The Markup page shows each example as the XML a markup file holds, because the reader cuts the C# raw string away.
It fails the build on an example that is not a raw string constant with indented markup.

The readme marks a markup rule `No level`, and the level table has no row for it.
The build fails when a markup rule has a level in either, when a rule that is no markup rule says `No level`, and when a markup rule ships on.
A key whose page says that `stanza-markup` ignores it, as `stanza_markup_literals` does, is the analyzer's alone.

The page writes the `.editorconfig` the C# page writes, with the none level held and one state for each rule:

- A rule starts as `stanza-markup` applies it: `ZA2001`, `ZA2006` and `ZA2007` on, and the others off.
  Its dropdown reads `Default (Suggestion)` or `Default (Off)`, because there is no level to follow.
- The `[*.cs]` section holds a `dotnet_diagnostic` line for each rule that is on, because the analyzer enables no markup rule on its own.
- The section of the markup files holds a line only where the tool differs:
  a `none` for a rule it applies that the visitor turned off, and a severity for a rule it leaves off.
- The tool applies any severity but `none` alike, so a raised severity changes the `[*.cs]` section alone.
- The page writes `ZA2001`, `ZA2006` and `ZA2007` by default, and a file with only those lines imports as no change.
- Every setting writes each rule: one that is on at its severity, one that is off as a comment, and a `none` line where the tool would apply it.
- A pair, such as `ZA2001` and `ZA2002`, shows in the conflict warnings once the file enables both halves, as on the C# page.

## What the build reads

The build keeps no list of rules or options. It reads these files and fails with the name of any file it cannot parse:

| File | What the page takes from it |
| --- | --- |
| [`docs/rules`](../docs/rules) | The ID, the title, the tier line, the description, each `// Before` and `// After` example, and the bullets. |
| [`docs/options`](../docs/options) | The key, the default, the value table, the examples, and the rules the page names. |
| [Analyzer readme](../src/Nickelony.Stanza.Analyzers/README.md) | The groups, the gap text of each rule, the tier and level to check each page against, and the defaults of the keys. |
| [`levels.txt`](../src/Nickelony.Stanza.Analyzers.Package/levels.txt) | The level of each rule and its CSharpier mark, from the table the package writes its preset files from; a markup rule has no row. |
| [`docs/Markup-behavior.md`](../docs/Markup-behavior.md) | The names and extensions of the markup profiles. |
| [`MarkupDiagnostics.cs`](../src/Nickelony.Stanza.Markup.Core/MarkupDiagnostics.cs) | The rules `stanza-markup` reads. |
| [`docs/Which-rule-owns-which-gap.md`](../docs/Which-rule-owns-which-gap.md) | The pairs of the pair table, with the default half and the half that owns the gap or construct when both are enabled. |
| [`docs/Working-with-formatters.md`](../docs/Working-with-formatters.md) | The rules the section on layout that CSharpier undoes links, together with the rule pages that link that section. |
| [`icon/Stanza.png`](../icon/Stanza.png) | The image of the C# page's link preview and home-screen icon. |
| [`icon/Stanza-Mu.png`](../icon/Stanza-Mu.png) | The image of the Markup page's link preview and home-screen icon. |
| [`icon/Stanza-JS.png`](../icon/Stanza-JS.png) | The image of the JS page's link preview and home-screen icon. |
| [`icon/Stanza-Py.png`](../icon/Stanza-Py.png) | The image of the Python page's link preview and home-screen icon. |
| [`icon/Stanza.svg`](../icon/Stanza.svg) | The logo of the C# page, which [`src/icons/csharp.svg`](src/icons/csharp.svg) copies. |
| [`icon/Stanza-Mu.svg`](../icon/Stanza-Mu.svg) | The logo of the Markup page, which [`src/icons/markup.svg`](src/icons/markup.svg) copies. |
| [`icon/Stanza-JS.svg`](../icon/Stanza-JS.svg) | The logo of the JS page, which [`src/icons/js.svg`](src/icons/js.svg) copies. |
| [`icon/Stanza-Py.svg`](../icon/Stanza-Py.svg) | The logo of the Python page, which [`src/icons/python.svg`](src/icons/python.svg) copies. |

### JS Stanza

| File | What the catalog takes from it |
| --- | --- |
| [`js/docs/rules`](../js/docs/rules) | The name, the level line, the description, each `// Before` and `// After` example, the options table and the settings. |
| [JS readme](../js/README.md) | The rule table: the order, the level and the summary of each rule. |
| [`presets.ts`](../js/src/presets.ts) | The first preset that holds each rule, and the opt-in rules that no preset holds. |
| [`js/src/rules`](../js/src/rules) | The default of each option of a rule, from its `defaultOptions`, and the tight half that a separating half asks about. |
| [`Rule-map.md`](../js/docs/Rule-map.md) | The pair table: the separating half and the tight half of each pair. |
| [`environment.ts`](../js/src/analysis/environment.ts) | The defaults of the settings in `settings.stanza`, and the least and greatest value of a number. |

### Python Stanza

| File | What the catalog takes from it |
| --- | --- |
| [`py/docs/rules`](../py/docs/rules) | The code, the name, the level line, the description, each `# Before` and `# After` example, and the options table. |
| [Python readme](../py/README.md) | The rule table: the order, the level and the summary of each rule. |
| [`Configuration.md`](../py/docs/Configuration.md) | The keys of `[tool.stanza]`, their defaults and what they do. |
| [`py/src/nickelony_stanza/rules`](../py/src/nickelony_stanza/rules) | The code, the name, the level, the opposite and the `keep` role each rule registers. |
| [`options.py`](../py/src/nickelony_stanza/options.py) | The default of a list that the configuration page defers to a rule page, the default and greatest value of a number, and the words of a key declared as a `Literal`. |
| [`config.py`](../py/src/nickelony_stanza/config.py) | The keys the table accepts, which key reads which `DEFAULT_` and `_LIMIT` constants through `_read_bounded`, and the least value of a number. |
| [`formatter.py`](../py/src/nickelony_stanza/formatter.py) | The formatters the `formatter` key accepts, and its default. |
| [`Rule-map.md`](../py/docs/Rule-map.md) | The pair table: the halves of each pair, the default half and the owner when both are enabled. |

A test keeps these tables and the `paths` of the [Pages workflow](../.github/workflows/pages.yml) equal to the files the build reads.

The build ships the catalog as a JSON string that the page parses with `JSON.parse`, which V8 parses faster than an object literal.
It keeps each link as its address and each highlighted token as `<t3>text</t>`, and [`src/util/compact.ts`](src/util/compact.ts) writes both back as the page shows them.

The header and the tab of each page show its logo from `src/icons`, a copy of its SVG in `icon/` that a test keeps byte for byte.
Copy it again when a logo changes.

A code fence on a rule or option page opens with triple backticks in the first column, with at most a language after them,
so an example never sits inside a list item.
The build fails on any other fence, and a `Repository` guard fails on it in the C# pages.

A link in a C# page must be an `https://` address.
A link in a JS or Python page may also be a path of the repository, which the build rewrites to a GitHub address.
The build fails on any other target, on a link with a title or a parenthesis in its address, on an image,
and on bold written with underscores.

The first column of an option's value table decides its input:
literal values make a dropdown, `a number from 1 to 64` makes a bounded number, a comma separated list makes text,
and `a profile name` makes a dropdown of the profiles.

## The pickers

- The **Level**, **Severity** and **On conflict with CSharpier** pickers set the `StanzaLevel`, `StanzaSeverity`
  and `StanzaFormatter` properties of the package.
  The build reads the level of each rule from `levels.txt` and checks it against the **Level** column of the readme and the tier line of the page.
- A markup rule has no level, so the [Markup page](#the-markup-page) has none of these pickers.
- **Stanza wins** is the `stanza-format` formatter, and **CSharpier wins** the `csharpier` one.
- Picking a level fills the form: its rules are on at the picked severity, and the others are off.
  Rules the visitor set against the old level are dropped, after the visitor confirms.
- The `csharpier` formatter changes only the strong level, as the package does:
  it turns off the rules marked `csharpier-off` and turns on those marked `csharpier-on`.
  At every level it also hides the rules CSharpier undoes.
- The summary beside the form, such as **Moderate plus 2 changes**, counts the rules and options that differ from the level and the defaults.
  A rule set to what the level already gives is no change.

## What it writes

This is the C# page; the Markup page writes the second block alone.

- The first block holds the MSBuild properties, without the ones at their default.
- The second block is the `.editorconfig`.
  A rule that is what the level gives writes no line, because the package applies the level.
  A rule the level leaves off is enabled by a `dotnet_diagnostic.<id>.severity` line, as [Terms](https://github.com/Nickelony/Stanza/wiki/Terms) says.
- A key writes a line only when its value differs from the default its page or the readme gives.
- A page whose `Default:` line gives the tool another default, such as `stanza_markup_scope`, has no default in the markup section.
  Any value you pick for it goes there.
- `stanza-markup` reads its own section, so a rule it reads and a key it reads also go under the extensions of its profiles.
- The tool reads only the file and never the level, so its section is held against the tool's own default.
  It repeats each rule of the level that the tool reads, and a level never turns off a rule the tool applies by default.
- The **Markup section** checkbox leaves that section out.
  When a key only the tool would have written is changed, a comment at the top says so.
- A key that picks a profile for each file, such as `stanza_markup_profile`, gets a comment above its line,
  because the section matches every markup file.
- **Every setting** writes each rule of the level at its severity and each key with a literal default, so the file works without the properties.
  A rule the level leaves off is a comment, unless the package would turn it on, and then the line is `none`.
- The **Every setting** tooltip says its `none` lines and comments hold only for a project that sets the same level, or none.
- The page keeps the configuration in the address hash, and the theme and the **Diff marks** choice in local storage.
  Without the marks, the preview shows the code alone, with a blank line as an empty line.
- The **Download** button saves the `.editorconfig` block as a file.
  A browser that drops the leading dot of a download name, such as Chrome and Edge, saves it as `editorconfig`, which you rename.
- In the hash, `all=1` turns on **Every setting**, `markup=0` turns off the **Markup section**,
  and `sel` holds the ID or key of the rule or option the preview shows.
- A hash edited by hand that holds a `sel` selects that row and clears the filter, so the row is not hidden.

## What it reads from an `.editorconfig`

The **Import** button fills in the form from a file.
It replaces the changed settings, after the visitor confirms, and reports what it skipped; it never stops on a file it cannot read.
It keeps the pickers, and holds each line against the level they give, so a file the page wrote imports as no change.

- A file over 1 MB or with a NUL character is refused whole, and the form stays as it was.
- A section whose glob matches every C# file, such as `[*]`, `[*.cs]` and `[*.{cs,vb}]`, gives the analyzer settings,
  and one that names an extension of a markup profile gives the `stanza-markup` settings.
  The lines above the first section count as C#, as in a `.globalconfig`.
- The form holds one value for both tools, so where the markup section disagrees with the C# one, the C# value wins and a note says so.
- A section for a path or another file type, such as `[tests/**/*.cs]`, is skipped, and one entry counts its Stanza lines.
- A line is read as `stanza-markup` and Roslyn read it: `=` or `:` ends the key, and a trailing `#` or `;` comment is cut,
  as is the `:severity` suffix of a `stanza_` value.
- A Stanza line is skipped, with its number, when its rule or key is unknown, its severity is none of the five or `default`,
  or its value is one the option page does not allow.
  A line of another setting, such as `indent_style`, is counted and left alone.
- The report lists the first 200 skipped lines and counts the rest.
- The severity `refactoring` is read as `silent`, as Roslyn does.
- A rule or key that the other of the two pages holds is skipped, and its note says to import the file there.
- A rule set to `default`, a rule set to what the level gives and an option set to its default stay at the default.
  A value in the C# section is held against the analyzer's default, so the `stanza_markup_scope = all` that **Every setting** writes is no change.

## Conflict warnings

The page lists the rule combinations that fight each other in the output, above the `.editorconfig` code:

- A pair with both halves enabled has one owner of the gap, and the other half reports nothing.
  The build reads the pairs, their default halves and the owner when both are enabled from the pair table of `Which-rule-owns-which-gap.md`.
- A file enables a rule by a line of its own or by **Every setting**,
  and by the preset file that the package adds for any pick but the moderate level at suggestion.
- The `csharpier` formatter hides the rules CSharpier undoes, apart from one the configuration sets, and warns about one that is enabled.
