# Format command reference

`stanza-format` formats a solution in the [run order](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#run-order) the Stanza rules need.
The block below is the output of `stanza-format --help`.

```text
stanza-format - run dotnet format, CSharpier and stanza-markup in the order the Stanza rules need

Usage:
  stanza-format [options] [<path>]

Without --all, --check or --since, formats the files git reports as changed, through the projects that compile them.

Path:
  A solution file, or a directory that holds one solution. The default is the working directory.

Options:
  --all                  Format the whole solution, and lay out the files of its directory.
  --check                Write nothing, and exit with 1 when a step would change a file.
                         Checks the whole solution, or the files --since names.
  --since <ref>          Take the files changed since the current branch left <ref>, such as
                         origin/main, and the untracked ones. Scopes --check too. Not with --all.
  --layout <name>        What lays out the code between the rule passes: csharpier, whitespace
                         (the whitespace pass of dotnet format) or none. The default is csharpier
                         when the tool manifest lists it, and whitespace when it does not.
  -h, --help             Show this help.
  --version              Show the tool version.
  --                     Read the next argument as the path.

Options in full: https://github.com/Nickelony/Stanza/wiki/Format-command-reference
```

## Option values

- `--layout` and `--since` take their value as the next argument or after an equals sign, so `--since main` and `--since=main` are the same.
  An option is never the value, so give a value that starts with a dash after an equals sign.
- Each takes one value: a second use of either is a command-line error, whichever way it is written.

## Scope

- The default run formats the changed files: those that differ from `HEAD`, and the untracked ones.
  Before the first commit, each file the index holds counts as changed.
- An untracked file below a directory named `bin` or `obj` is build output, so it never counts as changed.
- A changed C# file goes through the projects that compile it, wherever it lies.
  One outside the solution's directory that no project compiles is left out, and a `Skipped` line names it.
- Under CSharpier, it also lays out each changed file of the solution's directory that CSharpier formats,
  such as a project, `.xml` or `.config` file, and a changed source there that no project compiles goes to CSharpier alone.
- `stanza-markup` formats the changed markup files of the solution's directory, when the tool manifest lists it.
  A changed file counts as markup by an extension a profile claims,
  so a file that only a `stanza_markup_profile` entry maps waits for `--all`.
- `--all` formats the whole solution, and CSharpier and `stanza-markup` read the files of the solution's directory.
- `--check` checks the whole solution, with or without `--all`, unless `--since` scopes it.
- `--since <ref>` swaps the changed files for those [changed since a ref](#since-a-ref), in a check too, and cannot be combined with `--all`.
- The `dotnet format` commands run from the root of the git repository that holds the solution.
  The tool commands, `dotnet tool restore`, CSharpier and `stanza-markup`, run from the solution's directory.
- A run writes and reports no file outside the repository,
  though a solution's pass would otherwise format each project it references, such as one in a sibling checkout.

## Since a ref

`--since <ref>` takes the files that differ from the commit where the current branch left the ref (their merge base),
and the untracked ones.
A pull request gate, such as `stanza-format --check --since origin/main`, then covers the new code alone.
A commit that only the ref holds is no change, and a file the working tree holds is read as it stands.

- The ref is any branch, tag or commit that names a commit, such as `origin/main` or `HEAD~3`, and a ref that starts with a dash is refused.
- A detached HEAD works, and a renamed file counts under its new name.
- A ref that names no commit, and a ref that shares no history with HEAD, end the run with exit code `2`.
  In a shallow clone the error says to fetch more history, such as with `git fetch --unshallow` or a fetch depth of `0` in CI.
- A write run formats the files as the [scope](#scope) says. A run that finds none says so, and exits with `0`.
- A check reads only those files:
  - **Analyzers** and **IDE style rules** include the changed sources, and **Source layout** or **Whitespace** the changed files they format.
  - **Markup files** checks the changed markup files, and runs only when the tool manifest lists it.
  - **Line endings** and **Merge markers** report only the changed files, untracked ones included, wherever they lie in the repository.
  - A step with nothing to read is left out.
- **Source layout** passes when CSharpier ignores every file it was given, as a `.csharpierignore` entry makes it.
- A check that finds work names `--since <ref>` in the run that fixes it.

## Steps

A write run runs these steps in turn, and leaves out a step with nothing to format:

1. **Local tools**: `dotnet tool restore`, when the run uses CSharpier or `stanza-markup`.
2. **Analyzers, body shapes aside**: every fix at `--severity info` but the body-shape rules.
3. **IDE style fixes**: the fixes of the IDE style rules at `--severity info`,
   but for [`IDE0130`](https://github.com/Nickelony/Stanza/wiki/Working-with-formatters#run-order), whose fix `dotnet format` cannot apply.
   `dotnet format style` can leave a fix for its next run, so the step runs again while it changes a file.
4. **CSharpier** or **Whitespace**: the layout step.
5. **Body shapes**: the body-shape rules, which judge the lines the layout step wrote.
6. **CSharpier again** or **Whitespace again**: the layout step over the bodies the body-shape rules wrote.
   A run leaves it out when no changed source is compiled by a project.
7. **Analyzers, body shapes aside, again**: the fixes again, which put back the blank lines the layout drops.
   The step runs again while it changes a file.
8. **Markup files**: `stanza-markup format`, when the tool manifest lists it.
9. **Merge markers**: the check that no file has a line that starts with a merge marker.
   A whole run reads every tracked or untracked file of the repository, and any other run the changed files, so a marker elsewhere does not stop it.
10. **Settle check**: the [check](#check) steps of the tools the run used, **Analyzers**, **IDE style rules**,
    the layout check and **Markup files**, over the files it formatted: the whole solution for `--all`, and the changed files otherwise.

A step that runs again stops after three passes, and says so when the third still changed a file.
The first command that fails ends the run.

- Two passes can undo each other's change, as `IDE2001` and CSharpier do with an empty block body,
  so the last pass leaves a file that a check fails on.
- The settle check finds such a file. The run then lists each check step that still finds work and its fix, and exits with `1`.
- The settle check costs one extra check pass.
- A merge marker ends a write run at the check that finds it, with exit code `2` and the fix line of the marker step.
- Under CSharpier the marker check also runs before the layout step, and again before the second one when `dotnet format` ran between them,
  because CSharpier fails on the syntax error a marker leaves.
  The last check catches a marker the final analyzer pass wrote.
- An analyzer pass applies every analyzer's fix at `info` or above,
  so a project sets a rule whose fix it does not want, such as CA1822 on a public API, to `silent` or `none`.
- A diagnostic that no fix can apply, such as CA1041 or the report of a [report-only rule](https://github.com/Nickelony/Stanza/wiki/Terms),
  does not stop a write run: `dotnet format` prints `Unable to fix <ID>. No associated code fix found.` and the run goes on.
- A check fails on such a diagnostic at `info` or above, and so does the settle check of a write run.
- The check prints each report, and the closing lines tell you to fix them by hand or to set the rule's severity below `info`.

## Layout

- `--layout csharpier` runs `dotnet csharpier format`, and needs CSharpier in the tool manifest.
- `--layout whitespace` runs the whitespace pass of `dotnet format`, which lays out the code as `.editorconfig` asks.
- `--layout none` runs the rule passes alone.
- Without `--layout`, a run uses CSharpier when the tool manifest lists it, and the whitespace pass when it does not.
- A run calls CSharpier 1.1 or later, because 1.0 prints a check report in another shape and 0.x lacks the `format` and `check` commands.
  When the layout would be CSharpier, a run refuses an older version in the tool manifest with exit code `2`;
  update it, or pass `--layout whitespace`.
- The tool manifest is the one `dotnet` reads from the solution's directory:
  each tool comes from the nearest `.config/dotnet-tools.json` or `dotnet-tools.json` that lists it,
  searching up the parent directories to a manifest that sets `isRoot` to `true`.
- A manifest that is not valid JSON ends the run with exit code `2`, and the error names the file.

## Shared source

A project compiles a file in a [view](https://github.com/Nickelony/Stanza/wiki/Terms).
`dotnet format` merges the views of a file that several projects compile, and writes an unmerged-change marker where they disagree.

- One MSBuild run reads the files each project of the solution compiles, and the view of each project.
- A view holds the target framework, the compilation symbols and the analyzers:
  the package references, the `Analyzer` items and the project references that load an analyzer.
- Two projects of one framework that list different packages are two views, so a rule only one of them loads still runs on a file both compile.
- A file of one view goes through the solution, in a default run too, so each project that compiles it applies the rules it loads.
- A file of more than one view is held out of the solution's pass and formatted through the first project of each view, in turn.
- `dotnet format` merges the frameworks of a project that targets several, and it writes a marker where they disagree.
- A rule that reports on one framework only, such as CA1822 on `net8.0` but not on `netstandard2.0`, leaves a marker,
  so set it to `silent` or `none` for such a project.
  A marker written anyway ends the run at the marker check.
- A project that targets several frameworks counts as one view, and its files are those that any framework compiles.

## Check

`--check` writes nothing. It first restores the local tools, as a write run does, and then runs these steps:

1. **Analyzers** and **IDE style rules**: `dotnet format` with `--verify-no-changes`.
   **IDE style rules** leaves out `IDE0130` too, which a build still reports at the severity a project sets.
2. **Source layout (CSharpier, blank lines aside)**: a source matches CSharpier's layout but for its line endings
   and any gap of one blank line or none between two lines, and any other file CSharpier formats matches it exactly.
   Under the whitespace layout, **Whitespace** runs `dotnet format whitespace --verify-no-changes`.
3. **Markup files**: `stanza-markup check --strict`, when the tool manifest lists it.
   A finding of that check, a warning that `--strict` raises included, fails the check like another step, and the steps after it still run.
4. **Line endings**: each tracked or untracked file has the ending its `.gitattributes` entry pins.
   An untracked file below `bin` or `obj` is build output, so the step skips it, as the [scope](#scope) does.
5. **Merge markers**: no line of any tracked or untracked file is `<<<<<<<` or `>>>>>>>`, alone or before whitespace,
   the markers of a conflict and of views `dotnet format` could not merge.

A blank line that no Stanza rule writes back therefore passes the source layout check, and a write still removes it.
A file CSharpier cannot parse ends the check with exit code `2`, though `csharpier check` exits with `1` for it too.

## Exit codes

| Exit code | Meaning |
| --- | --- |
| `0` | The run formatted the files and its settle check passed, or `--check` found nothing to change. |
| `1` | `--check` found a step that would change a file, or a line ending, merge marker, diagnostic or markup finding to fix by hand. The settle check of a write run found a step that would still change a file, or a diagnostic or markup finding to fix by hand. |
| `2` | A write run stopped at a step or found a merge marker, or a command failed rather than finding a change. Git found no repository or refused it, a `--since` ref cannot be compared with, a tool manifest is not valid JSON or pins a CSharpier the run cannot call, or the command line could not be read. |
| `130` | Ctrl+C or SIGTERM canceled the run, and the command it was running. |

## Output

- Each step prints `==> <title>`.
  A step that runs commands prints each one and what it printed, and a check step prints each file it finds, relative to the repository root.
- A command's standard output goes to standard output and its standard error to standard error.
- A command longer than 400 characters is cut where it is shown.
- A default run that leaves out changed sources prints a `Skipped` line after its first line, naming the first five.
- An error goes to standard error as an `error: <message>` line.
- A write run prints `Checking the formatted files` before its settle check, and `Formatting finished` when the check passes.
  When it fails, an `error: the run did not settle` line lists the steps and the fix of each.
- A check that finds work lists the steps, then the run that fixes each one.
  That run repeats the `--layout` and the path the check was given, and writes `--since <ref>` for a scoped check and `--all` for a whole one.
