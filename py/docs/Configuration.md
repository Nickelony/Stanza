# Configuration

`stanza-py` reads `[tool.stanza]` from the nearest `pyproject.toml` that has the table, searching from the working directory upward.
`--config <file>` names a file instead. `--level` replaces the level for that run,
and `--enable` and `--disable` add to the `enable` and `disable` keys.
A rule that both name stays off, because `disable` applies last.

```toml
[tool.stanza]
level = "strong"
enable = ["ZA3012"]
disable = ["separate-guard-clauses"]
exclude = ["migrations/**", "build/"]
formatter = "ruff"
assertion-roots = ["assert*", "expect", "pytest.raises"]
guard-roots = ["check_argument"]
subscription-roots = ["connect", "subscribe"]
max-loose-run-size = 3
named-argument-min-arguments = 3
named-argument-ignore = ["log.*"]
source-roots = ["src"]
```

A key the table does not know is an error, and so is a value of the wrong type,
a number out of its bounds, a word the key does not list and a source that `named-argument-sources` does not know.
Each stops the run with an error that names the key, and the command exits with code 2.

A `source-roots` entry must name an existing directory, read from the configuration file's directory.
A missing path, a file and `~`, which is not expanded, are errors that read
`'source-roots' in [tool.stanza] names <path>, which is not a directory.`

An entry that is a filesystem root, a drive root or the home directory is an error too, and it reads
`'source-roots' in [tool.stanza] names <path>, a filesystem root or the home directory.`
The [`ZA3501` page](rules/ZA3501-name-call-arguments.md#options) owns the effect of the call keys.

## Keys

| Key | Default | Effect |
| --- | --- | --- |
| `level` | `"moderate"` | The preset to start from: `none`, `minimal`, `moderate` or `strong`. |
| `enable` | `[]` | Rules to add to the preset, each by code or by name. An opt-in rule is in no preset, so only this key enables it. |
| `disable` | `[]` | Rules to remove from the preset, each by code or by name. |
| `exclude` | `[]` | Globs for the paths to skip, read from the working directory. `**` crosses directories and `dir/**` skips a tree. |
| `formatter` | `"auto"` | The formatter that `stanza-py format` runs first: `auto` detects it, and `ruff`, `black` or `none` name it. The other commands ignore it. |
| `assertion-roots` | see below | The patterns that make a call an assertion. |
| `guard-roots` | `[]` | The patterns that make a bare call a guard, for `ZA3011` and for the guards `ZA3010` skips. |
| `subscription-roots` | see below | The patterns that make a call a subscription. |
| `max-loose-run-size` | `3` | The most statements or members a loose run holds before `ZA3052` and `ZA3053` join it. `0` turns both off. |
| `named-argument-min-arguments` | `4` | The fewest arguments a call writes before `ZA3501` names them, from 0 to 1000. `0` turns the rule off. |
| `named-argument-match` | `"allow"` | Whether `ZA3501` leaves a leading identifier of its parameter's name unnamed: `allow` or `require`. |
| `named-argument-literals` | `"keywords"` | The literals that start the names `ZA3502` writes: `keywords` for `True`, `False` and `None`, or `bool` for the first two. |
| `named-argument-sources` | `["project", "stdlib", "installed"]` | Where a callee may be defined for the call rules to name its call: `project`, `stdlib` or `installed`. |
| `named-argument-methods` | `"all"` | The methods a call through `self`, `cls` or `super()` may name: `all`, `private` or `none`. |
| `named-argument-ignore` | `[]` | Globs of callees that neither call rule names, matched against the callee as written and as resolved. |
| `source-roots` | `[]` | The directories read as the project, each relative to the configuration file and read as a whole tree. Empty detects them from each file. |

## Patterns

A pattern is a shell-style glob, and it matches a call when it matches the dotted path of the callee or any single name along it.
The path of `self.assertEqual(a, b)` is `self.assertEqual`, so `assert*` and `self.assert*` both match it.

The default `assertion-roots` are `assert*`, `expect`, `pytest.raises`, `pytest.warns` and `pytest.deprecated_call`.
An `assert` statement is an assertion whatever the list says, and a `with` block is one when all its items are calls that match.
The rules that group calls read the same list, so they agree on which statements are assertions.

The default `subscription-roots` are `connect`, `disconnect`, `subscribe`, `unsubscribe`, `add_listener`, `remove_listener` and `add_done_callback`.
A call that matches `assertion-roots` is an assertion before it is a subscription.

The default `guard-roots` list is empty, so only an `if` that jumps and an `assert` are guards.
`ZA3011` reads the list for the guards that open a block, and `ZA3010` for the guards it skips before it judges a base call.

The roots of the C# analyzers and JS Stanza are exact names, but these are globs on the dotted path or on any name along it.
Python test frameworks name assertions by family, such as `assertEqual`, `assert_called_once` and `pytest.raises`.

An explicit empty list means no roots,
so `assertion-roots = []` makes no call an assertion and `subscription-roots = []` makes no call a subscription.
The C# analyzers read an empty value as the defaults and spell no roots as `none`. A key left out keeps its default.

## Opt-in rules

A rule whose level is opt-in belongs to no preset, so `enable` or `--enable` turns it on.
`ZA3201` and `ZA3202`, and `ZA3111` and `ZA3112`, are pairs that do opposite things at one gap,
so naming one half replaces the other at the level.
[The pair table](Rule-map.md#pairs-and-their-default-halves) says which half owns the gap when both are named.
The rules that keep a run, `ZA3054` and `ZA3055`, judge the runs of the grouping rules that are enabled.

## Command line

```text
stanza-py check [paths] [--config FILE] [--level L] [--enable RULES] [--disable RULES] [--exclude GLOB]
                [--stdin-filename PATH] [--diff]
stanza-py fix   [paths] [--config FILE] [--level L] [--enable RULES] [--disable RULES] [--exclude GLOB]
                [--stdin-filename PATH]
stanza-py format [paths] [--config FILE] [--level L] [--enable RULES] [--disable RULES] [--exclude GLOB]
                 [--stdin-filename PATH] [--formatter F] [--check]
stanza-py rules
```

- A path is a file or a directory, and `-` reads standard input and writes the fixed text to standard output.
- `--stdin-filename` names the file that standard input holds, so the call rules resolve its calls against its project.
  Without it, the text resolves against itself, the standard library and the installed packages.
  The name does not change which configuration is read, and `format` gives it to the formatter.
- Without a path the command reads the working directory.
- A directory scan skips `.git`, `.hg`, `.svn`, `.venv`, `venv`, `__pycache__`, `node_modules`, `.tox`, `.nox`, `.mypy_cache`, `.ruff_cache`,
  `.pytest_cache`, `build`, `dist`, `.eggs` and every `*.egg-info` directory, and it skips every `.pyi` stub.
- `check` exits with 0 when the files are clean and 1 when it found a blank line to add or remove or an argument to name.
- Every command exits with 2 on a file that does not parse, a missing path or a configuration they cannot read,
  after they have handled the other files.
- `--enable` and `--disable` take rule codes or names, separated by commas, and may repeat.
- `format` formats each file with the `formatter`, fixes the result, and runs the formatter again to check that it keeps the fix,
  with one more fix and formatter run when it did not, as the [formatter page](Working-with-formatters.md#the-format-command) says.
  `--formatter` replaces the `formatter` key for the run, and `--check` writes nothing.
- `format` exits with 1 when `--check` finds a file it would change, or when the formatter still changes a file after the last fix.
  It exits with 2 when no formatter is found, a formatter is not installed or a formatter refuses a file.
- `stanza-py rules` lists the codes, levels and names.

## Silencing

- `# noqa` on the first line of the statement below a gap silences every finding there, and `# noqa: ZA3001, ZA3005` names the codes.
  A code prefix such as `ZA3` silences the family.
- A call finding takes its `# noqa` on the first line of the call's statement, on the line where the call starts or on the line of its `(`.
  `named-argument-ignore` takes a call away from both call rules, while a `# noqa` silences only the rule that owns the call.
- `# ruff: noqa: ZA3001` and `# flake8: noqa` on a line of their own silence the named codes, or every code, for the file.
- A silenced finding is not handed to another rule: the gap or the call stays as written.

## flake8 and pre-commit

The package registers a flake8 plugin for the codes `ZA3`, so `flake8 --select ZA3` reports what `check` reports.
`--stanza-level` sets its level and the plugin reads the rules and the options of `[tool.stanza]`,
while flake8 selects the files, so `exclude` and `formatter` have no effect there.
Ruff has no plugin system, so an editor that runs Ruff shows these findings through flake8 or through `stanza-py check`.

- The plugin takes the filename flake8 passes as the path of the file, so the call rules resolve its calls against its project.
- A text flake8 reads from standard input is named `stdin` unless `--stdin-display-name` gives a path.
  Without a path the text resolves nothing project-relative.

The file `.pre-commit-hooks.yaml` defines the hooks `stanza-py-check`, `stanza-py-fix` and `stanza-py-format`.
It sits in `py/`, and pre-commit reads it from the root of a repository,
so a project that installs the package from this repository declares them as local hooks:

```yaml
repos:
  - repo: local
    hooks:
      - id: stanza-py
        name: stanza-py
        language: python
        entry: stanza-py fix
        types: [python]
        additional_dependencies: ["nickelony-stanza @ git+https://github.com/Nickelony/Stanza.git#subdirectory=py"]
```

Run the `fix` hook after the formatter, as the [formatter page](Working-with-formatters.md#run-order) says,
and run the formatter again after it while a call rule is enabled, or use the `stanza-py-format` hook, which does both.
The `stanza-py-format` hook runs the formatter itself, in an environment that holds only what `additional_dependencies` lists.
Add the formatter the project configures, as in `["nickelony-stanza @ git+...", "ruff"]`,
because `auto` finds a configured formatter that is missing from that environment and fails with exit code 2.
