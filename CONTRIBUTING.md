# Contributing

How to build, test and format the repository, and what a rule, an option or a pull request needs.

## Requirements

- Install the .NET SDK that [`global.json`](global.json) names, or a later patch in its feature band, which CI builds with too.
- Install the .NET 8 runtime, which the test projects also need.
- Install PowerShell, which runs the formatting gate.
- Install [Vale](https://vale.sh/docs/install), which checks the prose of the pages and the comments.

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
- `.github/` holds the CI and release workflows, the setup action and the scripts they share,
  and the issue and pull request templates; `icon/` holds the package icon.
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

- The build is warning-free by policy (`TreatWarningsAsErrors` in `Directory.Build.props`),
  except for the NuGet audit codes NU1900 to NU1904, so a new advisory or an unreachable feed fails no build.
  A weekly CI run restores the solution with `-p:StanzaAuditStrict=true`, which turns those codes into errors.
- `./format.ps1` restores the pinned CSharpier itself.
- The `Repository` guards read the pages, the readmes and the release rows, so they need a full checkout.
- A page or a readme is read by no build step, so run Vale and the `Repository` filter after editing one:
  `dotnet test Nickelony.Stanza.slnx --filter TestCategory=Repository`.
- The convergence and truncation tests run a sample by default.
  To run a larger set, set `STANZA_SOAK_SEEDS` to a seed count and filter on `TestCategory=Soak`; each test class says what the count means.
  The weekly [soak workflow](.github/workflows/soak.yml) sets it, and a manual run takes another count.
- CI fails a skip that a test does not state as a design, so a test that cannot run states its skip through `ExpectedSkip.Skip`,
  or through `[OSCondition]` outside `Analyzers.Tests`, as [`Assert-ExpectedSkips.ps1`](.github/scripts/Assert-ExpectedSkips.ps1) checks and [T6](CONVENTIONS.md#tests) says.
- Add the ID of a new package to [`package-ids.txt`](.github/scripts/package-ids.txt),
  which the pack step, the publish job and the `Repository` guards read, and give each issue form's product dropdown an option for it.
- The `StanzaNewestRoslyn` line runs the analyzer tests on a newer Roslyn than the
  [floor](src/Nickelony.Stanza.Analyzers/README.md#installation) they build against.
- A narrower command is fine while a change is in progress; CI still runs every gate before the pull request can merge.

## Releasing

A release is a tag, which the [release workflow](.github/workflows/release.yml) verifies, packs and publishes.

## Adding or changing a rule

1. Add the `DiagnosticDescriptor` to `DiagnosticDescriptors` and the analyzer to its rule family's directory;
   the ID must not already be in use.
   - The ID range names the family:
     `ZA00xx` lays out C# code, `ZA01xx` lays out markup, in raw strings and in `stanza-markup`,
     and `ZA02xx` lays out documentation and plain comments.
   - A rule `stanza-markup` also applies declares its ID and message in `src/Nickelony.Stanza.Markup.Core/MarkupDiagnostics.cs`,
     which `DiagnosticDescriptors` reads.
     Its behavior lives in the engine, with any key it reads in `MarkupKeys`,
     and it needs parity tests in `tests/Nickelony.Stanza.Markup.Tests/*ParityTests`.
   - A rule whose pair, gap fix, node kinds or deferrals shared code reads is also an entry in `StanzaRules`, and so is a rule another defers to.
   - A rule that defers names the other rule in `DefersTo` and reads it through `GetDeferral`, for a gap it leaves or a shape it takes.
2. Add the rule's page at `docs/rules/<id>-<title>.md`, headed `# <id> - <title>`.
   In the [analyzer readme](src/Nickelony.Stanza.Analyzers/README.md), add its row to the rule table,
   with the severity the descriptor carries and a gap text no other row repeats,
   which leads with Adds or Removes for a rule that changes a blank line.
   - The row goes in the table of its group, in ID order.
   - A group is a `###` heading above a table, and `wiki.ps1` takes the groups of the sidebar from those headings.
3. Add the rule's row to `src/Nickelony.Stanza.Analyzers/AnalyzerReleases.Unshipped.md` in the shape the release-tracking analyzers read;
   a rule with no row is a build error.
4. Give the rule exactly one code fix in the `Nickelony.Stanza.Analyzers.CodeFixes` project.
   A rule whose `StanzaRules` entry carries a `GapFix` gets its fix from `BlankLineCodeFixProvider` and needs no provider row;
   every other rule takes a `FixTable` row in the provider of its family.
5. Add fixtures to `<Stem>Tests.cs`, the analyzer's name without its suffix:
   one the rule fires on and one it stays quiet on, each driving the rule's own analyzer type.
   A test of two rules goes in the family file named after their directory in the singular, such as `ArgumentFamilyTests` for `Arguments/`.
6. Add the rule's row to `RuleSpanTests`, which `EveryRuleHasARow` requires.
7. Give a new set of sibling rules, a separating rule with its multi-line and single-line tight rules, a row in `SiblingRuleSets`.
   `RuleRegistryTests` fails a set the registry pairs that has no row, and a row that names rules it does not pair.
8. Pin the rule to `warning` in `.editorconfig`.
   A rule the repository must not run is pinned to `none` and listed in the `disabled` array of `FormatterSettingsGuardTests`.
9. Run the [verification](#verification),
   and add an `Unreleased` entry to `CHANGELOG.md` when the change is user visible.

The `Repository` guards in `tests/` own the exact contracts;
[`release.ps1`](release.ps1) and [`wiki.ps1`](wiki.ps1) each read some of the files above.

## Adding or changing an option

1. Declare the key. A key that only the analyzer reads goes in `src/Nickelony.Stanza.Analyzers/StanzaOptions.cs`.
   Any other key is a constant in `MarkupKeys.cs` in `src/Nickelony.Stanza.Markup.Core/Options`.
2. Add `docs/options/<key>.md`, which opens with `# <key>`.
3. If the analyzer reads the key, add its row to the option table of the [analyzer readme](src/Nickelony.Stanza.Analyzers/README.md),
   with the default the reader resolves.
4. Run the [`Repository` guards](#verification), which check the page names, the readme table and its defaults.
5. Add an `Unreleased` entry to `CHANGELOG.md` when the change is user visible.

## Pull requests

- Keep a change focused: one rule, one fix or one documentation change per pull request.
- A review judges the change against [CONVENTIONS.md](CONVENTIONS.md).
- The [pull request template](.github/PULL_REQUEST_TEMPLATE.md) asks the author to confirm that the [verification](#verification) gates pass.
