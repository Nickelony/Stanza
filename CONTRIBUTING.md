# Contributing

How to build, test and format the repository, and what a rule, an option or a pull request needs.

## Requirements

- Install the .NET SDK that [`global.json`](global.json) names, or a later patch in its feature band, which CI builds with too.
- Install the .NET 8 runtime, which the test projects also need.
- Install PowerShell, which runs the formatting gate.
- Install [Vale](https://vale.sh/docs/install), which checks the prose of the pages and the comments.
- Install Node.js 24, the version of [`site/.node-version`](site/.node-version), which builds and tests the [`site/`](site/) configurator
  and, as [`js/.node-version`](js/.node-version) says, the [`js/`](js/) plugin.
- Install Python 3.10 or later, only to work on [`py/`](py/); CI tests 3.10 to 3.14.

## Documentation

- What each product ships is in its readme under [`src/`](src/).
- [`docs/`](docs/) is the reference, mirrored to the [documentation wiki](https://github.com/Nickelony/Stanza/wiki) by `wiki.ps1`.
- Edit the repository copy, never the wiki.
- Write a doc by [STYLEGUIDE.md](STYLEGUIDE.md), which states the documentation rules and shows them applied.

## Repository layout

- `src/` holds the projects that ship, the engine they share, and in `src/Shared` the host code the tools compile in.
- `tests/` holds the test projects and the analyzer self-check.
- `docs/` holds the documentation the wiki mirrors.
  [Architecture](https://github.com/Nickelony/Stanza/wiki/Architecture) maps the code to each part.
- `site/` holds the rule configurator, a static page that the [Pages workflow](.github/workflows/pages.yml) publishes.
  Its build reads the rule pages, the option pages and a few other files, so it keeps no list of rules;
  [its readme](site/README.md) lists every file it reads.
- `js/` holds [JS Stanza](js/README.md), the ESLint plugin that mirrors the gap rules for JavaScript and TypeScript.
  It builds with npm on its own, shares no code with the C# projects, and has its own rule pages under `js/docs/`.
  - `js/stylelint-config/` holds [`stylelint-config-stanza`](js/stylelint-config/README.md), the Stylelint config for the blank lines of CSS, SCSS and Emotion templates.
    It installs, tests and lints with npm on its own, apart from `js/`, and has its own pages under `js/stylelint-config/docs/`.
- `py/` holds [Python Stanza](py/README.md), the tool and flake8 plugin that mirror the gap rules for Python and name the arguments of its calls.
  It builds with pip on its own, shares no code with the C# projects, and has its own rule pages under `py/docs/`.
- `.github/` holds the workflows (verify, CI, release, Pages, soak, JS and Python), the setup action and the scripts they share,
  and the issue and pull request templates.
- `icon/` holds the package icon.
- A rule's page is `docs/rules/<id>-<title>.md`, whichever family it is in;
  a code no descriptor backs has its page under `docs/tool/`.

## Formatting

[`format.ps1`](format.ps1) owns the layout of this repository.
It builds [`stanza-format`](docs/Format-command-reference.md) from its project, which builds the Stanza analyzers in Debug for `dotnet format` to load,
and runs it over the solution, then runs `stanza-markup`, built the same way, over the markup files.
With no switch it formats the files git reports as changed, as the [command reference](docs/Format-command-reference.md#scope) says, and every markup file:

```powershell
./format.ps1            # write the changes, the changed files and every markup file
./format.ps1 -All       # write the changes, whole solution
./format.ps1 -Check     # report only, whole solution, exit non-zero when something is off
```

JSON, `.editorconfig` and Markdown are hand-kept:
no formatter rewrites them, but `-Check` checks their line endings. CI runs `-Check`.

## Verification

A change is ready when CI's verify run is green.
CI runs these gates and the package checks on every pull request, so run the gates locally, in this order, before you push:

```powershell
./format.ps1
dotnet build Nickelony.Stanza.slnx
dotnet test Nickelony.Stanza.slnx
dotnet test tests/Nickelony.Stanza.Analyzers.Tests -c Release -p:StanzaNewestRoslyn=true
dotnet run --project src/Nickelony.Stanza.Markup -- check . --strict
vale --glob='!**/{bin,obj,artifacts}/**' .
```

- A narrower command is fine while a change is in progress; CI still runs every gate before the pull request can merge.
- CI builds, tests and packs in Release so that the tested assemblies are the packed ones,
  and the `dotnet build` and `dotnet test` commands above use the default Debug, which runs the same tests with debug assertions on.
  Add `-c Release` to them to reproduce a failure that only CI shows.
- The build is warning-free by policy (`TreatWarningsAsErrors` in `Directory.Build.props`),
  except for the NuGet audit codes NU1900 to NU1904, so a new advisory or an unreachable feed fails no build.
  A weekly CI run restores the solution with `-p:StanzaAuditStrict=true`, which turns those codes into errors.
- `./format.ps1` restores the pinned CSharpier itself.
- `.vale.ini` skips the installed packages, the build output and the tool caches of [`js/`](js/README.md) and [`py/`](py/README.md),
  so a plain `vale .` also passes in a checkout that has built them.
- The `Repository` guards read the pages, the readmes and the release rows, so they need a full checkout.
- A page or a readme is read by no build step, so run Vale and the `Repository` filter after editing one:
  `dotnet test Nickelony.Stanza.slnx --filter TestCategory=Repository`.
- `release.ps1`, the changelog check and the release notes meet the real `CHANGELOG.md` only at a release,
  so CI tests their changelog steps on fixtures.
  [`Test-ReleaseScripts.ps1`](.github/scripts/Test-ReleaseScripts.ps1) turns each changelog of [`fixtures/changelog`](.github/scripts/fixtures/changelog) into its expected release,
  and reads the expected notes from each released one.
  Run it after you change a script or the shape of `CHANGELOG.md`; it never runs `release.ps1` itself.
  The scripts and the workflow find a version's heading with the one pattern of
  [`Get-ChangelogHeadingPattern.ps1`](.github/scripts/Get-ChangelogHeadingPattern.ps1) or [`Get-TrackingHeadingPattern.ps1`](.github/scripts/Get-TrackingHeadingPattern.ps1), so a heading that one accepts is accepted by all.
- The convergence and truncation tests run a sample by default.
  To run a larger set, set `STANZA_SOAK_SEEDS` to a seed count and filter on `TestCategory=Soak`; each test class says what the count means.
  The weekly [soak workflow](.github/workflows/soak.yml) sets it, and a manual run takes another count.
- A soak test starts at a base seed of its own, and `STANZA_SOAK_SEED_OFFSET` moves the first seed that far.
  The weekly run sets it to its run number times the count, so each week meets seeds no earlier week did;
  a local run leaves it unset and stays deterministic.
- A failed seed is printed as `seed <n>`, so to repeat it set the count to 1 and the offset to `n` minus the base of that test,
  which is 1000 for the convergence soaks and 5000 for the fix-loop fuzz.
- The fix-loop fuzz runs only in the soak.
  A soak test has no runner timeout, and `SoakRun` fails it after five minutes plus one for each seed.

### Skipped tests

- A test that its environment stops, such as one that needs a file link the machine cannot make, ends `Inconclusive` and leaves a local run green,
  but CI fails any skip that a test does not state as a design.
- A test that is meant to skip states it through `ExpectedSkip.Skip`, or through `[OSCondition]` outside `Analyzers.Tests`,
  as [`Assert-ExpectedSkips.ps1`](.github/scripts/Assert-ExpectedSkips.ps1) checks and [T6](CONVENTIONS.md#tests) says.
  To see what CI sees, run the tests with `--logger trx --results-directory <directory>` and pass that directory as `-Results`.

### Packages and Roslyn

- Add the ID of a new package to [`package-ids.txt`](.github/scripts/package-ids.txt),
  which the pack step, the publish job and the `Repository` guards read, and give each issue form's product dropdown an option for it.
- The `StanzaNewestRoslyn` line runs the analyzer tests on a newer Roslyn than the
  [floor](src/Nickelony.Stanza.Analyzers/README.md#installation) they build against.

### Site

- A change to `site/` or to a file the build reads, which [its readme](site/README.md) lists, also runs the site gates, in `site/`:
  `npm ci`, `npm run format:check`, `npm run lint`, `npm run typecheck`, `npm test` and `npm run build`.
  The lint loads JS Stanza, so `npm ci` and `npm run build` run in `js/` first.
- `npm run format` writes the changes that `npm run format:check` reports;
  [the site readme](site/README.md) says what the formatter and the linter require.
- The site tests read the pages, so a page the site cannot parse fails them.
  The Pages workflow runs the same steps on a pull request.

### JS and Python

- A change under [`js/`](js/) also runs the gates of the [JS workflow](.github/workflows/js.yml), in `js/`:
  `npm ci`, `npm run build`, `npm run typecheck`, `npm test`, `npm run format:check` and `npm run lint`.
  `npm run format` writes the changes that `npm run format:check` reports.
- A change under [`js/stylelint-config/`](js/stylelint-config/) also runs its gates, which the JS workflow runs after those of `js/`, in that directory:
  `npm ci`, `npm test`, `npm run format:check` and `npm run lint`.
  The lint loads the plugin from `js/dist`, so run `npm run build` in `js/` first.
- The JS workflow also runs the plugin tests on the newer ESLint major of the peer range, in its `eslint-10` job.
  Its pack smoke job installs both tarballs beside the newest ESLint and Stylelint and lints a `.ts` and a `.scss` file.
- A change under [`py/`](py/), or to the rule pages of `docs/rules` and `js/docs/rules` that its tests read,
  also runs the gates of the [Python workflow](.github/workflows/py.yml), in `py/`, after `pip install -e ".[dev]"`:
  `pytest`, `ruff check .`, `ruff format --check .`, `mypy` and `stanza-py check src tests`.
  The workflow runs them on Python 3.10 to 3.14.
- Neither product has a changelog or a release workflow yet, so the `Unreleased` entries and the releasing steps below are for the C# packages.

## Releasing

A release is a tag, which the [release workflow](.github/workflows/release.yml) verifies, packs and publishes.

The workflow needs this setup, which lives outside the repository:

- A GitHub environment named `release` that requires a reviewer, so the publish job waits for approval.
- A secret `NUGET_USER` in that environment, the nuget.org user name that the login step reads.
- A trusted publishing policy on nuget.org for each ID in [`package-ids.txt`](.github/scripts/package-ids.txt), naming the `release` environment,
  so the publish job holds no long-lived key.
- A policy for a new ID before the first tag that packs it,
  because the login step trades the OIDC token of the run for an API key that lasts an hour.

Release in this order:

1. Run `./release.ps1 -Version <x.y.z>` on a clean working tree, which commits and tags nothing.
   - It moves the rule rows of `AnalyzerReleases.Unshipped.md` into a `## Release <x.y.z>` section of `AnalyzerReleases.Shipped.md`.
   - It retitles `## [Unreleased]` in `CHANGELOG.md` with the version and the date, and opens an empty `## [Unreleased]` heading above it.
   - It writes the link references at the bottom of `CHANGELOG.md`:
     `[<x.y.z>]` links its compare range, or its release tag for the first release, and `[Unreleased]` links `compare/v<x.y.z>...HEAD`.
   - A prerelease version, one with a `-` suffix, leaves the two release tracking files alone.
2. Review the diff, commit it and merge it to `main`, because the workflow refuses a tag that is not an ancestor of `origin/main`.
3. Publish the wiki with [`wiki.ps1`](wiki.ps1), which runs only from a clean `main` whose HEAD is the tip of `origin/main`.
4. Push the tag `v<x.y.z>`.

The workflow first checks the tag against `main`, the changelog and, for a stable version, the release tracking file.
It then runs the gates of CI on a fresh restore.
After the reviewer approves, it publishes the packages that those gates built and checked,
and then creates the GitHub release from the changelog section.
Each push skips a file already on nuget.org, so rerunning the failed publish job finishes a partial publish.

The gates include the `newest-roslyn` job, which takes the newest Roslyn on nuget.org, so a new Roslyn can fail a release that changed nothing.
Such a failure publishes nothing: fix the analyzers on `main` and release the next patch version, rather than moving the tag.

The [Pages workflow](.github/workflows/pages.yml) publishes the site on each push to `main` that changes what the site reads.
[The site readme](site/README.md) names the one-time repository setting its first deployment needs.

## Adding or changing a rule

A rule's ID names its group of the analyzer readme, and each group holds one range,
so a new rule takes the next free number of its own in the range of its group:

| Range | Holds |
| --- | --- |
| `ZA0xxx` | The C# rules with a fix, a hundred to a group, with comments last in `ZA09xx`; `ZA07xx` stays free and `ZA08xx` stays unused, because `ZA38xx` holds the Python-only rules |
| `ZA01xx` | The delimited lists, each list in its own tens |
| `ZA1xxx` | The extension of the C# range, where `ZA10xx` holds the limits, which have no fix |
| `ZA2xxx` | The markup rules, which `stanza-markup` shares, and in `ZA29xx` the codes of that tool alone |
| `ZA3xxx` | The Python rules of `py/`, which that tool owns and the steps below leave out: a mirrored rule takes the code of its `ZA0xxx` counterpart plus 3000, and `ZA38xx` holds those with no C# counterpart |

- In `ZA00xx`, a rule that adds a blank line takes a number below `ZA0051`, and one that removes blank lines takes one from `ZA0051`.
- In `ZA06xx`, a rule that simplifies a statement takes a number below `ZA0651`, and one that sets the style of an expression takes one from `ZA0651`.
- In `ZA09xx`, a rule of documentation comments takes a number below `ZA0951`, and one of plain comments takes one from `ZA0951`.
- Within a list's tens in `ZA01xx`, the last digit means the same:
  1 separates a multi-line element, 2 keeps multi-line elements together and 3 keeps single-line elements together.
  `ZA0198` and `ZA0199` add and remove the trailing comma of every list.
- Past `ZA10xx`, the digit of the hundreds holds, so `ZA11xx` continues `ZA01xx`.

1. Add the `DiagnosticDescriptor` to `DiagnosticDescriptors` and the analyzer to its rule family's directory;
   the ID must not already be in use.
   - A rule `stanza-markup` also applies declares its ID and message in `src/Nickelony.Stanza.Markup.Core/MarkupDiagnostics.cs`,
     which `DiagnosticDescriptors` reads.
     Its behavior lives in the engine, with any key it reads in `OptionKeys`,
     and it needs parity tests in `tests/Nickelony.Stanza.Markup.Tests/*ParityTests`.
   - A rule whose pair, gap fix, node kinds or deferrals shared code reads is also an entry in `StanzaRules`, and so is a rule another defers to.
   - A rule that defers names the other rule in `DefersTo` and reads it through `GetDeferral`, for a gap it leaves or a shape it takes.
2. Add the rule's page at `docs/rules/<id>-<title>.md`, headed `# <id> - <title>`.
   In the [analyzer readme](src/Nickelony.Stanza.Analyzers/README.md), add its row to the rule table,
   with the severity the descriptor carries and a gap text no other row repeats,
   which leads with Adds or Removes for a rule that changes a blank line.
   - The row goes in the table of its group, in ID order.
   - A group is a `###` heading above a table, and `wiki.ps1` takes the groups of the sidebar from those headings.
   - The [site](site/README.md) lists the rule from its page and this row, so it needs no step of its own;
     its tests fail when the page and the row disagree on the tier.
3. Add the rule's row to `src/Nickelony.Stanza.Analyzers/AnalyzerReleases.Unshipped.md` in the shape the release-tracking analyzers read;
   a rule with no row is a build error.
4. Give the rule exactly one code fix in the `Nickelony.Stanza.Analyzers.CodeFixes` project, unless the rule only reports.
   A rule whose `StanzaRules` entry carries a `GapFix` gets its fix from `BlankLineCodeFixProvider` and needs no provider row;
   every other rule takes a `FixTable` row in the provider named for its directory.
   - A rule that only reports takes no fix and is listed in `TestRuleSets.ReportOnly`, which the test harness and `RuleRegistryTests` read.
   - Its readme gap text opens with Reports, its fixtures drive it with `ReportAsync`, and its page shows a rewrite by hand as the After block.
5. Add fixtures to `<Stem>Tests.cs`, the analyzer's name without its suffix, in its family's test directory:
   one the rule fires on and one it stays quiet on, each driving the rule's own analyzer type.
   A test of two rules goes in the family file of that directory, such as `ArgumentFamilyTests`.
6. Add the rule's row to `RuleSpanTests`, which `EveryRuleHasARow` requires.
7. Give a new set of sibling rules, a separating rule with its multi-line and single-line tight rules, a row in `SiblingRuleSets`.
   `RuleRegistryTests` fails a set the registry pairs that has no row, and a row that names rules it does not pair.
8. Pin the rule to `warning` in `.editorconfig`.
   A rule the repository must not run is pinned to `none` and listed in the `s_disabled` array of `FormatterSettingsGuardTests`.
9. Give the rule its [level](docs/Presets.md) in `src/Nickelony.Stanza.Analyzers.Package/levels.txt`, and the same level in its readme row.
   - A rule of the `minimal` or `moderate` level ships on as a suggestion, and any other rule ships off.
     Set the descriptor's `isEnabledByDefault` and the severity of its `AnalyzerReleases.Unshipped.md` row to match:
     `true` and `Info`, or `false` and `Disabled`.
   - A rule the repository pins to `warning` is `strong`, and one it pins to `none` is `opt-in`, unless `PresetGuardTests` names it,
     as it does for the opt-in rules that Stanza's own code runs.
   - A markup rule of `ZA2xxx` takes no level, because `stanza-markup` has none: a severity line enables it, in the analyzer and in the tool alike.
     Its readme row says `No level` in the Level column, and the level table has no line for it.
   - Run `PresetGuardTests` with `STANZA_WRITE_PRESETS=1` to write the files under `build/presets` from the table;
     the guards fail a rule the table skips and a file that differs from what the table writes.
10. Run the [verification](#verification),
    and add an `Unreleased` entry to `CHANGELOG.md` when the change is user visible.

The `Repository` guards in `tests/` own the exact contracts;
[`release.ps1`](release.ps1) and [`wiki.ps1`](wiki.ps1) each read some of the files above.

## Adding or changing an option

1. Declare the key as a constant in `OptionKeys.cs` in `src/Nickelony.Stanza.Markup.Core/Options`, and add it to `OptionKeys.Known`.
   The analyzers and both tools share that file, and a guard fails a `stanza_` key literal in any other file of `src`.
2. If the key takes one of a closed set of names, declare them as an `OptionChoice` in `OptionValues`,
   which its reader, its check and its warning read.
3. Add `docs/options/<key>.md`, which opens with `# <key>`.
   The [site](site/README.md) takes the input type of the key from the first column of the value table, so it needs no step of its own.
4. If the analyzer reads the key, add its row to the option table of the [analyzer readme](src/Nickelony.Stanza.Analyzers/README.md),
   with the default the reader resolves.
5. Run the [`Repository` guards](#verification), which check the page names, the readme table and its defaults.
6. Add an `Unreleased` entry to `CHANGELOG.md` when the change is user visible.

## Pull requests

- Keep a change focused: one rule, one fix or one documentation change per pull request.
- A review judges the change against [CONVENTIONS.md](CONVENTIONS.md).
- The [pull request template](.github/PULL_REQUEST_TEMPLATE.md) asks the author to confirm that the [verification](#verification) gates pass.
