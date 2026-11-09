# Python Stanza

*Where code becomes poetry.*

Rules that lay out the blank lines of Python code and name the arguments of its calls, the sibling of the [Stanza C# analyzers](https://github.com/Nickelony/Stanza#readme)
and of [JS Stanza](https://github.com/Nickelony/Stanza/blob/main/js/README.md).
The rules set each [stanza](https://github.com/Nickelony/Stanza/wiki/Terms) apart from its neighbors.

Black and `ruff format` set the blank lines around functions and classes, cap the rest, and add none between two other statements.
Python Stanza adds the blank lines they leave out, removes a few kinds of padding,
and names the arguments of the calls whose callee it can read from source.
A fix inserts or removes blank lines between neighboring units, or writes a parameter name before an argument, and nothing else:
it never reorders code or touches a string.

The package is not published yet.

## Install

```text
pip install ./py
pip install "nickelony-stanza @ git+https://github.com/Nickelony/Stanza.git#subdirectory=py"
```

The distribution is `nickelony-stanza`, the module is `nickelony_stanza` and the command is `stanza-py`.
It needs Python 3.10 or later and has no dependency that compiles: it reads a file with the standard `ast` and `tokenize` modules.

## Use

```text
stanza-py check src tests
stanza-py fix src tests
stanza-py format src tests
```

`check` prints one line per finding and exits with 1 when there are any, `fix` writes the files, and all exit with 2 on an error.
A fix writes the same text in every order and a second run changes nothing.

`format` runs the formatter the project configures, Ruff or Black, then the fix,
then the formatter again to check that it keeps the result, and repeats the fix once if that run changed the text.
That is the order the rules want, and the [formatter page](https://github.com/Nickelony/Stanza/blob/main/py/docs/Working-with-formatters.md#the-format-command) says how it finds the formatter.

The fixes add or remove blank lines between neighboring units, as [`ZA3001`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3001-separate-multi-line-constructs.md) does here:

```python
# Before
order = load(order_id)
if order.closed:
    notify(order)
log(order)

# After
order = load(order_id)

if order.closed:
    notify(order)

log(order)
```

The example runs at the default `moderate` level, which `ZA3001` belongs to.
At the `strong` level, [`ZA3501`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3501-name-call-arguments.md) turns `draw(640, 480, True, "image.png")`
into `draw(width=640, height=480, smooth=True, path="image.png")` when it reads the definition of `draw` from source.

## Presets

| Preset | Rules |
| --- | --- |
| `none` | Enables no rule. |
| `minimal` | The file header and the imports, and the padding below a block header, before a clause keyword or between single-line `case` clauses. |
| `moderate` | `minimal`, plus the multi-line, run, member and two-unit rules. This is the default. |
| `strong` | `moderate`, plus the await, assignment, guard, jump, comment, type, subscription and base call rules, the rules that gather loose runs, and the rules that name call arguments. |

An opt-in rule belongs to no preset, and `enable` adds it.

## Rules

| Rule | Level | What it does | C# rule |
| --- | --- | --- | --- |
| [`ZA3017`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3017-separate-file-header.md) `separate-file-header` | minimal | Separates the shebang, the encoding line and the module docstring from the code. | `ZA0017` |
| [`ZA3015`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3015-separate-import-section.md) `separate-import-section` | minimal | Separates a run of imports from the statement below it. | `ZA0015` |
| [`ZA3113`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3113-keep-single-line-cases-together.md) `keep-single-line-cases-together` | minimal | Removes the blank line between two `case` clauses that each span one line. | `ZA0113` |
| [`ZA3202`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3202-keep-clause-keywords-adjacent.md) `keep-clause-keywords-adjacent` | minimal | Removes the blank line before an `elif`, `else`, `except` or `finally`. | `ZA0202` |
| [`ZA3203`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3203-remove-block-padding.md) `remove-block-padding` | minimal | Removes the blank lines between a block header and the first line of its body. | `ZA0203` |
| [`ZA3001`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3001-separate-multi-line-constructs.md) `separate-multi-line-constructs` | moderate | Separates a statement that spans several lines from its neighbors. | `ZA0001` |
| [`ZA3111`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3111-separate-multi-line-cases.md) `separate-multi-line-cases` | moderate | Separates two `case` clauses when either spans several lines. | `ZA0111` |
| [`ZA3002`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3002-separate-declaration-runs.md) `separate-declaration-runs` | moderate | Separates a run of declarations of one kind. | `ZA0002` |
| [`ZA3004`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3004-separate-invocation-runs.md) `separate-invocation-runs` | moderate | Separates a run of calls of one helper. | `ZA0004` |
| [`ZA3005`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3005-separate-assertion-runs.md) `separate-assertion-runs` | moderate | Separates a run of assertions. | `ZA0005` |
| [`ZA3013`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3013-separate-member-groups.md) `separate-member-groups` | moderate | Separates the class docstring and the runs of fields and attributes. | `ZA0013` |
| [`ZA3051`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3051-keep-two-units-together.md) `keep-two-units-together` | moderate | Removes the blank line between the only two single-line units of a container. | `ZA0051` |
| [`ZA3003`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3003-separate-same-type-declaration-runs.md) `separate-same-type-declaration-runs` | strong | Separates a run of declarations of one type. | `ZA0003` |
| [`ZA3006`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3006-separate-await-runs.md) `separate-await-runs` | strong | Separates a run of await statements. | `ZA0006` |
| [`ZA3007`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3007-separate-subscription-runs.md) `separate-subscription-runs` | strong | Separates a run of event subscriptions such as `signal.connect(...)`. | `ZA0007` |
| [`ZA3008`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3008-separate-assignment-runs.md) `separate-assignment-runs` | strong | Separates a run of attribute, item and augmented assignments. | `ZA0008` |
| [`ZA3009`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3009-separate-jump-statements.md) `separate-jump-statements` | strong | Separates a `return`, `raise`, `break` or `continue` from a stanza above it. | `ZA0009` |
| [`ZA3010`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3010-separate-base-call.md) `separate-base-call` | strong | Separates a `super()` call that opens or closes a function body. | `ZA0010` |
| [`ZA3011`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3011-separate-guard-clauses.md) `separate-guard-clauses` | strong | Separates the guard clauses that open a block. | `ZA0011` |
| [`ZA3012`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3012-separate-commented-siblings.md) `separate-commented-siblings` | strong | Separates a comment that leads a single-line statement. | `ZA0012` |
| [`ZA3052`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3052-gather-loose-statements.md) `gather-loose-statements` | strong | Removes the blank lines of a loose run of statements that is too long. | `ZA0052` |
| [`ZA3053`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3053-gather-loose-members.md) `gather-loose-members` | strong | Removes the blank lines of a loose run of class members that is too long. | `ZA0053` |
| [`ZA3501`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3501-name-call-arguments.md) `name-call-arguments` | strong | Writes the parameter name before the arguments of a call that passes at least `named-argument-min-arguments` arguments, when the callee is read from source. | `ZA0501` |
| [`ZA3502`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3502-name-literal-arguments.md) `name-literal-arguments` | strong | Writes the parameter name before a `True`, `False` or `None` argument of a call, when the callee is read from source. | `ZA0502` |
| [`ZA3054`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3054-keep-statement-runs-together.md) `keep-statement-runs-together` | opt-in | Removes the blank lines inside a run of statements that a grouping rule reads as one. | `ZA0054` |
| [`ZA3055`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3055-keep-member-runs-together.md) `keep-member-runs-together` | opt-in | Removes the blank lines inside a run of fields or attributes that `ZA3013` reads as one. | `ZA0055` |
| [`ZA3112`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3112-keep-multi-line-cases-together.md) `keep-multi-line-cases-together` | opt-in | Removes the blank line between two `case` clauses when either spans several lines. | `ZA0112` |
| [`ZA3201`](https://github.com/Nickelony/Stanza/blob/main/py/docs/rules/ZA3201-separate-clause-keywords.md) `separate-clause-keywords` | opt-in | Separates an `elif`, `else`, `except` or `finally` from the block above it. | `ZA0201` |

Each rule owns its gaps: when several rules ask for one blank line, the first enabled rule in the [owner order](https://github.com/Nickelony/Stanza/blob/main/py/docs/Rule-map.md#one-owner-per-gap) reports it.
A call goes to the first call rule that names it, as the [rule map](https://github.com/Nickelony/Stanza/blob/main/py/docs/Rule-map.md#one-owner-per-call) says.
The [rule map](https://github.com/Nickelony/Stanza/blob/main/py/docs/Rule-map.md) says which C# rules Python leaves to Black, Ruff and pycodestyle, and the [quirks](https://github.com/Nickelony/Stanza/blob/main/py/docs/Quirks.md) list the syntax the rules take care with.

## Configuration

`pyproject.toml` holds the settings, and `--level`, `--enable` and `--disable` change them for one run:

```toml
[tool.stanza]
level = "moderate"
disable = ["ZA3012"]
assertion-roots = ["assert*", "expect", "verify"]
named-argument-min-arguments = 3
exclude = ["migrations/**"]
```

Silence one finding with `# noqa: ZA3001` on the first line of the statement below the gap, or a file with `# ruff: noqa: ZA3001`.
A call finding takes its `# noqa` on the first line of the call's statement, on the line where the call starts or on the line of its `(`.
The [configuration page](https://github.com/Nickelony/Stanza/blob/main/py/docs/Configuration.md) owns every key, the command line and the pre-commit hook.

## Relation to Black and Ruff

Black and `ruff format` own the blank lines beside a function or class, after the imports and after a module or class docstring.
The rules leave those gaps alone, so a formatter never undoes a fix, and a fix never fights it.
A name can push a line past the width of the formatter, which then wraps the line and keeps the names.
The [formatter page](https://github.com/Nickelony/Stanza/blob/main/py/docs/Working-with-formatters.md) lists the gaps each tool forces and the ones left free.

## Development

```text
python -m venv .venv
source .venv/bin/activate        # Windows: .venv\Scripts\activate
python -m pip install -e ".[dev]"
python -m pytest
python -m ruff check .
python -m ruff format --check .
python -m mypy
stanza-py check src tests
```

The last command lints the package's own source with the `strong` preset and `ZA3055`,
minus the call rules, because the code passes its arguments by position.
The suites in `tests` fix a corpus in several rule orders, round-trip it through Black and Ruff, and run every example of the rule pages.
