# Working with formatters

Black and `ruff format` own some blank lines and leave the rest as written,
and Python Stanza writes only the blank lines they leave free and the parameter names of calls, which they never write.
Both formatters agree on every gap below, so the blank-line rules never fight either of them.

A *formatter* here is Black or `ruff format`, the `formatter` key picks the one that `stanza-py format` runs,
and the C# analyzers make the same choice with [`StanzaFormatter`](https://github.com/Nickelony/Stanza/wiki/Presets)
and [`stanza-format --layout`](https://github.com/Nickelony/Stanza/wiki/Format-command-reference#layout).

## Run order

Only the round trip is order-free for the rules that lay out blank lines: a file that one tool has settled stays as written when the other runs.
The round trip suite fixes a corpus and formats the result, then formats the corpus and fixes the result, and expects no change in either case.

In a hook chain, run the formatter first and `stanza-py fix` second, so the last tool has seen the final layout.
When a call rule is enabled, the formatter runs last as well, as the [next section](#names-and-line-width) says.

The two orders can still end in different texts when a rule removes a blank line.
A formatter can join a statement onto one line, as Black does with a backslash continuation,
and a blank line that a rule wrote beside the multi-line statement then stands beside a single-line statement.
The next run of `ZA3054` or `ZA3052` removes it, so those rules want the formatter to run first.

## Names and line width

A name that a call rule writes can push a line past the width the formatter keeps, and the formatter then wraps the call and keeps the names.
A fix that runs after the formatter can leave such a line, so the formatter must run after the fix whenever `ZA3501` or `ZA3502` is enabled.
`stanza-py format` does it, and a hook chain that runs `stanza-py fix` runs the formatter once more after it.

## The format command

`stanza-py format` runs the formatter, the rules and the formatter again in that order,
and repeats the last two once if the formatter changed the text:

```text
stanza-py format src tests
stanza-py format --check src tests
```

1. It formats the text of each file with the formatter that [detection](#detecting-the-formatter) finds, or that `--formatter` or the `formatter` key names.
2. It fixes the formatted text with the enabled rules.
3. It formats the fixed text once more. The result is settled when that run changes nothing.
4. It fixes the formatted text and formats it a last time when step 3 changed the text, as when a name widened a line.
   The result is settled when that run changes nothing.
5. It writes the file when the result differs from the file.

- A settled file is stable under the formatter and under the rules, so a second `format` writes nothing.
- An unsettled file is one where the formatter changes what the rules wrote.
  The command still writes the fixed text, names the file on standard error, and exits with 1.
  Run the command once more, and disable the rule that fights the formatter if the file stays unsettled.
- `--check` runs the same steps in memory and writes nothing.
  It names each file it would change and each file that is unsettled, and exits with 1 for either.
- The formatter is found on the path, else as a module of the interpreter that runs `stanza-py`.
  A run that finds no formatter, or whose formatter is not installed, ends with 2 before it reads a file.
- A formatter that refuses a file exits with 2 after the other files are handled.
- The formatter reads its own configuration for the path of each file, such as `[tool.ruff]` or `[tool.black]`.
- `--formatter none` and `formatter = "none"` skip the formatter steps, so the command then behaves as `fix`.
- The command formats each file through standard input, at most three times: before the rules, after them and after the second fix.

## Detecting the formatter

The `formatter` key defaults to `auto`, which takes the first of these that holds:

1. Ruff, when the project configures it with a `[tool.ruff]` table in `pyproject.toml`, a `ruff.toml` or a `.ruff.toml`.
2. Black, when the project configures it with a `[tool.black]` table.
3. Ruff when it is installed, else Black when it is installed.
4. Nothing: the command exits with 2 and says to configure a formatter, install one, or pass `--formatter none`.

- The search starts at the directory of the Stanza configuration file, else at the directory of the first path,
  and climbs through the parents.
- The nearest directory that configures either formatter decides, and Ruff wins inside it.
- A configured formatter that is not installed ends the run with 2, because the project asked for it.
  It does not fall back to the other formatter.
- `ruff`, `black` and `none` skip the search, and `--formatter` replaces the key.

```text
$ stanza-py format src    # pyproject.toml holds a [tool.black] table, and Ruff is installed too
formatted src/app.py
1 file formatted, 0 unsettled.

$ stanza-py format src    # nothing configured, nothing installed
stanza-py: error: no formatter found: the project configures neither Ruff nor Black, and neither is installed. Configure one with [tool.ruff] or [tool.black], install ruff or black, or run with --formatter none.
```

## Gaps a formatter forces

The rules never ask for a blank line at these gaps and never remove one, because the formatter writes them whatever the file says.

| Gap | The formatter writes | Rule |
| --- | --- | --- |
| Above and below a `def` or `class`, a decorator and a block that ends in one | Two blank lines at the top level, one in a body | None. |
| Between the methods of a class, and between an attribute and a method | One blank line | None. |
| Below the module docstring | Exactly one blank line | [`ZA3017`](rules/ZA3017-separate-file-header.md) asks for one. |
| Below a class docstring | Exactly one blank line | [`ZA3013`](rules/ZA3013-separate-member-groups.md) asks for one. |
| Below a run of imports, when a statement follows at the same depth | One blank line, and Ruff keeps a second one | [`ZA3015`](rules/ZA3015-separate-import-section.md) asks for one. |
| Between a block that ends in a function or class and the `elif`, `else`, `except` or `finally` that continues it | One blank line | None. |
| Between a `case` clause that ends in a function or class and the next clause | One blank line from Black, and none from Ruff | [`ZA3111`](rules/ZA3111-separate-multi-line-cases.md) adds it where it is missing, and [`ZA3112`](rules/ZA3112-keep-multi-line-cases-together.md) leaves it. |
| Between the header of a block that is no function or class, such as an `if` or a `try`, and a function or class that opens it | One blank line | None. |
| Any run of blank lines | At most two at the top level and one in a body | None. |

- A gap beside a function or class includes the gap below a block whose last statement is one, such as an `if` that ends in a nested `def`.
- Overload stubs, as in `@overload` followed by `def f(a: int) -> int: ...`, keep the zero or two blank lines the file had.
  They are definitions, so the rules leave them.
- `ZA3017`, `ZA3013` and `ZA3015` ask for the blank line the formatter forces anyway, so they matter for a project that runs no formatter.

## Gaps the formatters leave free

Black and Ruff keep one blank line between statements in a body and add none, so every other gap belongs to the rules:

- between two statements of a function, a loop, a `with` block, a `try` clause, a `match` case or a module,
  none of which is a definition or an import;
- between the last statement of a block and the `elif`, `else`, `except` or `finally` that continues it,
  which [`ZA3201`](rules/ZA3201-separate-clause-keywords.md) may add and [`ZA3202`](rules/ZA3202-keep-clause-keywords-adjacent.md) may remove;
- between two `case` clauses of a `match` statement, which [`ZA3111`](rules/ZA3111-separate-multi-line-cases.md) may add
  and [`ZA3112`](rules/ZA3112-keep-multi-line-cases-together.md) and [`ZA3113`](rules/ZA3113-keep-single-line-cases-together.md) may remove;
- between a function docstring and the statement below it, and between the two statements that follow a docstring,
  which [`ZA3051`](rules/ZA3051-keep-two-units-together.md) may remove;
- between a block header and the first line of its body, which [`ZA3203`](rules/ZA3203-remove-block-padding.md) may remove:
  `ruff format` removes it under every header but `def`, Black keeps it under every header, and both remove it above a function or class docstring;
- between a comment and the statement it leads, which no rule touches.

## Gaps the rules leave for another reason

- A blank line below a function docstring is never added, because pydocstyle `D202` forbids it and Ruff enforces `D202` where the project enables it.
- A region between `# fmt: off` and `# fmt: on` keeps its blank lines, as the [quirks](Quirks.md#comments) say.
- `ZA3051` skips a container that holds an import, a definition, a shebang or a module or class docstring, because the formatter forces the gap there.
  Without that skip, the rule would remove a blank line the formatter writes back.

## Other tools

- pycodestyle `E301` to `E306` ask for the blank lines around definitions, which the formatters already write.
  `E303` rejects more than two blank lines at the top level and more than one in a body, and the rules never write more than one.
- `isort` and Ruff `I001` order and group the imports, and own the blank lines between import groups.
  `ZA3015` owns only the blank line below the last import of a run.
- Tools that rewrite code, such as `autoflake`, run before the rules, and the rules never move a statement.

The tests run Black 26 and Ruff 0.17 with their default style and a line length of 88.
