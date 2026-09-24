# Markup command reference

`stanza-markup` runs one command over the paths it is given. The block below is the output of `stanza-markup --help`.

```text
stanza-markup - keep the multi-line sibling elements of a markup document apart

Usage:
  stanza-markup <command> [options] <path>...

Commands:
  format   Add the blank lines that are missing, cap each gap of blank lines that is too long,
           and write the files back.
  check    Report the files that are not formatted. Exits with 1 when there are any.

Paths:
  A file, a directory that is scanned recursively, or a glob such as "src/**/*.xml".

Options:
  --profile <name>       Lay out the files a glob or a file argument names as this profile,
                         whatever their extension.
  --exclude <glob>       Skip the files the glob matches, judged by their path from the working
                         directory. May be repeated.
  --format <name>        How check reports: human (the default) prints one line per file, and
                         msbuild prints one MSBuild diagnostic per finding.
  --reprint              Write the body of every <member> with the documentation layout,
                         whatever stanza_markup_doc_reprint says.
  --strict               Treat each warning as an error.
  -h, --help             Show this help.
  --version              Show the tool version.
  --                     Read every later argument as a path or a command.

Profiles: xml (.xml, .config), msbuild (.csproj, .vbproj, .fsproj, .vcxproj, .props, .targets, .proj, .projitems, .shproj), slnx (.slnx), xaml (.xaml, .axaml)

Each file takes its keys from the .editorconfig files that apply to it.

Options in full: https://github.com/Nickelony/Stanza/wiki/Markup-command-reference
Keys:            https://github.com/Nickelony/Stanza/wiki/Markup-configuration
```

## Paths and globs

- A glob is an explicit selection: it collects every match that `.editorconfig` does not disable and a profile claims,
  and `--profile` lays out each match whatever the file is called.
- A directory scan collects only the files a profile or a `stanza_markup_profile` entry claims,
  and with `--profile` only the extensions that profile claims.
- Globs use the `.editorconfig` dialect - `*` and `?` inside one segment, `**` across segments,
  `{a,b}` and `[abc]`/`[!abc]` - and match without regard to case.
- A glob without a separator matches the file name in every directory.
- `--exclude` takes the same globs, and a directory path or a glob ending in `/**` skips the whole subtree.
- A command-line glob that the dialect refuses, or that matches no file, is an error, as a path that does not exist is.
- A glob whose matches are all excluded, disabled or claimed by no profile is a warning.
- A directory scan or a glob skips every subdirectory named `.git`, `.hg`, `.svn`, `.vs`, `.idea`, `bin`, `obj` or `node_modules`,
  whatever its case. A directory named on the command line is still scanned.
- [Markup behavior](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#what-it-leaves-alone) says how a scan treats a link.
- [Markup globs](https://github.com/Nickelony/Stanza/wiki/Markup-globs) lists the refused forms and limits, escaping, how a root and `./` read, and what a plain name excludes.

## Option values

- `--profile`, `--exclude` and `--format` take their value as the next argument or after an equals sign,
  so `--profile xml` and `--profile=xml` are the same.
  A next argument that starts with a dash is never taken as the value, so write such a value with the equals sign.
- `--exclude` may be repeated, but `--profile` and `--format` take one value each:
  a second use of either is a command-line error, whichever way it is written.
- No other option takes a value with an equals sign.
- `--format msbuild` applies only to `check`, and an unknown format is a command-line error.
- `--reprint` leaves a member body the layout cannot read as written.
- `--strict` makes an error of each [warning](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#failures-and-warnings),
  and never of a note.
- No argument after `--` is read as an option, even one that starts with a dash,
  and a command the line has not named yet is still read after it.
- `-h`, `--help` and `--version` are honored whatever else the line says, unless the parser cannot read an option:
  that is reported instead, so a mistyped option cannot pass for a request for the usage text.
- A bare `-` is an unknown option, and so is any other option the parser does not know.
  A command line the parser cannot read exits with `2` and points at `--help`.

## Exit codes

`format` writes the files it changes, and `check` writes nothing.

| Exit code | Meaning |
| --- | --- |
| `0` | No file needs formatting, or `format` wrote the files that did. Without `--strict`, a run also exits `0` when its only problems are [warnings](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#failures-and-warnings). |
| `1` | `check` found a file that needs formatting. |
| `2` | The run failed, or the command line could not be read. [Failures and warnings](https://github.com/Nickelony/Stanza/wiki/Markup-behavior#failures-and-warnings) lists the problems that fail a run, and the warnings `--strict` raises to an error. |
| `130` | Ctrl+C or SIGTERM canceled the run. The tool prints `error: canceled.` to standard error and no summary line. |

## Output

- `format` prints `formatted <path>` for each file it writes, then `N file(s) formatted.`
- `check` prints `needs formatting: <path>` for each file, then `N file(s) need formatting.`
- Paths are relative to the working directory, with forward slashes; a file outside it shows its full path.
  A control, format or separator character in a name, such as a line break or U+2028, is written as its code, `\x0A` or `\x2028`,
  so each report stays on one line.
- A message joins its lines with a space and writes any other control, format or separator character as its code,
  so text it quotes from a file cannot send an escape sequence to the terminal.
- Notes, warnings and errors go to standard error, as `note: <message>`, `warning: <message>` or `error: <message>` lines.
  A problem on a line of an `.editorconfig` reads `warning: <path>(<line>): <message>`.
- Under `--format msbuild` they go to standard output with the diagnostics, `ZA0100` included.
  A warning reads `stanza-markup : warning : <message>`,
  or `<path> : warning : <message>` when it is about a file or a directory,
  and `<path>(<line>,1): warning : <message>` for a line of an `.editorconfig`.
- An error reads the same with `error`, and a note stays a `note: <message>` line, which a build logs as a message.
- `--format msbuild` replaces the per-file lines with [one diagnostic per finding](#build-output) and prints no summary line.
- `--version` prints the tool version alone, so a script can read the line as it is.

## Build output

`check --format msbuild` reports each finding as an MSBuild diagnostic, so the Error List navigates to its file,
line and column. The output has this shape, for example:

```text
src/Parts/Layout.xml(12,1): warning ZA0101: Add a blank line before this element
```

The ID is that of the [markup rule](https://github.com/Nickelony/Stanza/blob/main/src/Nickelony.Stanza.Analyzers/README.md#markup-in-raw-strings) that owns the finding,
and every finding is a `warning` whatever severity the file sets for that rule.
A file that could not be read or parsed is reported as [`ZA0100`](https://github.com/Nickelony/Stanza/wiki/ZA0100-Report-a-file-that-could-not-be-read-or-parsed),
which is reported even when the spacing rules are disabled. The [reprint](https://github.com/Nickelony/Stanza/wiki/Terms) of a member body is [`ZA0106`](https://github.com/Nickelony/Stanza/wiki/ZA0106-Reprint-the-body-of-a-documentation-member).

## Running it in a build

To fail a build on a finding, call the tool from an `Exec` task:

```xml
<Target Name="CheckMarkup" BeforeTargets="Build">
  <Exec Command="dotnet stanza-markup check . --format msbuild --strict" />
</Target>
```

`Exec` fails the build when the tool exits with a nonzero code, and `check` exits with `1` when a file needs formatting.
This example assumes the tool is in the repository's tool manifest.
To keep the build passing, set `IgnoreExitCode="true"` on the `Exec` task.
