# Rule map

Each C# rule of Stanza is mirrored, adapted or left out of Python Stanza, and this page says which and why.
The Python rules lay out blank lines, because Black and `ruff format` keep what a file has between statements
and add none, apart from the blank lines around a definition.
They also name the arguments of a call, which no Python formatter does.

Everything else in the C# families is a brace, a comma or a line break that Python's syntax, the formatters or pycodestyle own,
or a rule the tables below name.

## Naming

A Python rule keeps the number of its C# counterpart plus 3000, in the range `ZA3001` to `ZA3999`: `ZA0051` becomes `ZA3051`.
Python tools are configured and silenced by code, as in `# noqa: ZA3051` or `flake8 --select ZA3`, so the code is the identity.
The name is the kebab-case imperative of the JS rule where one exists, and the pages show both.

`ZA3017`, `ZA3111`, `ZA3112` and `ZA3113` take names that differ from their JS rules. Python has `match` and `case` but no `switch`,
so `ZA3111`, `ZA3112` and `ZA3113` say `cases` where the JS rules say `switch-cases`.
`ZA3017` is `separate-file-header`, not the JS `separate-directives`, because a Python header holds an encoding line and a docstring.

The call rules have no JS rule and take the imperative of their C# titles.

The codes `ZA3800` to `ZA3899` are reserved for rules that have no C# counterpart, and the C# side keeps `ZA0800` to `ZA0899` unused for that reason.

## Mirrored and adapted

| C# rule | Python rule | Level | Difference |
| --- | --- | --- | --- |
| `ZA0001` Separate multi-line constructs | [`ZA3001`](rules/ZA3001-separate-multi-line-constructs.md) `separate-multi-line-constructs` | moderate | A block whose body stands on its own lines is multi-line, so such an `if`, `for`, `with` or `try` stands apart. |
| `ZA0002` Separate declaration runs | [`ZA3002`](rules/ZA3002-separate-declaration-runs.md) `separate-declaration-runs` | moderate | Python has no declarations, so a declaration binds names: an assignment to names, an annotated assignment, `global`, `nonlocal` or `type`. |
| `ZA0003` Separate same type declaration runs | [`ZA3003`](rules/ZA3003-separate-same-type-declaration-runs.md) `separate-same-type-declaration-runs` | strong | The type is the annotation as written, and unannotated bindings always match, as `var` does. A class body belongs to `ZA3013`. |
| `ZA0004` Separate invocation runs | [`ZA3004`](rules/ZA3004-separate-invocation-runs.md) `separate-invocation-runs` | moderate | A call is one helper when its callee path is the same, such as `logger.info`. |
| `ZA0005` Separate assertion runs | [`ZA3005`](rules/ZA3005-separate-assertion-runs.md) `separate-assertion-runs` | moderate | An `assert` statement, a call that matches `assertion-roots` such as `self.assertEqual`, and a `with pytest.raises(...)` block. |
| `ZA0006` Separate await runs | [`ZA3006`](rules/ZA3006-separate-await-runs.md) `separate-await-runs` | strong | An await statement is `await call()`; a declaration that awaits belongs to the declaration rule. |
| `ZA0007` Separate event subscription runs | [`ZA3007`](rules/ZA3007-separate-subscription-runs.md) `separate-subscription-runs` | strong | Python has no event syntax and `ast` has no types, so a subscription is a call whose callee name matches `subscription-roots`, such as `signal.connect(...)`. |
| `ZA0008` Separate assignment runs | [`ZA3008`](rules/ZA3008-separate-assignment-runs.md) `separate-assignment-runs` | strong | An assignment to an attribute or an item, or an augmented assignment, such as the `self.x = x` lines of `__init__`. |
| `ZA0009` Separate a jump statement | [`ZA3009`](rules/ZA3009-separate-jump-statements.md) `separate-jump-statements` | strong | Judges `return`, `raise`, `break` and `continue`. |
| `ZA0010` Separate the base call | [`ZA3010`](rules/ZA3010-separate-base-call.md) `separate-base-call` | strong | A base call is a statement that calls through `super()`. Only function bodies are judged. |
| `ZA0011` Separate the guard clauses | [`ZA3011`](rules/ZA3011-separate-guard-clauses.md) `separate-guard-clauses` | strong | A guard is an `if` that only jumps, an `assert`, or a call of a helper that `guard-roots` lists. |
| `ZA0012` Separate a commented sibling | [`ZA3012`](rules/ZA3012-separate-commented-siblings.md) `separate-commented-siblings` | strong | A tool directive such as `# type: ignore` is not a heading and does not count. |
| `ZA0013` Separate member groups | [`ZA3013`](rules/ZA3013-separate-member-groups.md) `separate-member-groups` | moderate | Judges the class docstring, the fields and the attributes. The formatters own the gaps beside the methods. |
| `ZA0015` Separate the import section | [`ZA3015`](rules/ZA3015-separate-import-section.md) `separate-import-section` | minimal | Judges every run of imports, in a module or a body, as the formatters do. |
| `ZA0017` Separate a file-scoped namespace declaration from its first member | [`ZA3017`](rules/ZA3017-separate-file-header.md) `separate-file-header` | minimal | The header of a Python file is its shebang, its encoding line and its module docstring. |
| `ZA0051` Keep the two units of a container together | [`ZA3051`](rules/ZA3051-keep-two-units-together.md) `keep-two-units-together` | moderate | It never meets the rules that add a blank line, because they ask for nothing in its containers. |
| `ZA0052` Gather a run of loose statements | [`ZA3052`](rules/ZA3052-gather-loose-statements.md) `gather-loose-statements` | strong | Judges the statements of a module, a function and a block. A jump, an import and a definition are no part of a run. |
| `ZA0053` Gather a run of loose members | [`ZA3053`](rules/ZA3053-gather-loose-members.md) `gather-loose-members` | strong | Judges the single-line statements of a class body, because a method is a definition and the formatters own its gaps. |
| `ZA0054` Keep a run of statements together | [`ZA3054`](rules/ZA3054-keep-statement-runs-together.md) `keep-statement-runs-together` | opt-in | Judges the runs of the grouping rules that are enabled. |
| `ZA0055` Keep a run of members together | [`ZA3055`](rules/ZA3055-keep-member-runs-together.md) `keep-member-runs-together` | opt-in | Judges the fields and attributes that `ZA3013` reads as runs. |
| `ZA0111` Separate multi-line switch sections | [`ZA3111`](rules/ZA3111-separate-multi-line-cases.md) `separate-multi-line-cases` | moderate | Judges the `case` clauses of a `match` statement, and the statements around the `match` belong to `ZA3001`. The default half of the [pair](#pairs-and-their-default-halves) with `ZA3112`. |
| `ZA0112` Keep multi-line switch sections together | [`ZA3112`](rules/ZA3112-keep-multi-line-cases-together.md) `keep-multi-line-cases-together` | opt-in | The tight half of the pair with `ZA3111`. A case that ends in a function or class keeps the blank line Black writes. |
| `ZA0113` Keep single-line switch sections together | [`ZA3113`](rules/ZA3113-keep-single-line-cases-together.md) `keep-single-line-cases-together` | minimal | The formatters write a case body on its own line, so it matters for a project that runs neither. |
| `ZA0201` Separate a clause keyword from the brace before it | [`ZA3201`](rules/ZA3201-separate-clause-keywords.md) `separate-clause-keywords` | opt-in | The keywords are `elif`, `else`, `except` and `finally`, and the block above has no closing brace. |
| `ZA0202` Keep clause keywords adjacent | [`ZA3202`](rules/ZA3202-keep-clause-keywords-adjacent.md) `keep-clause-keywords-adjacent` | minimal | The default half of the [pair](#pairs-and-their-default-halves) with `ZA3201`. |
| `ZA0203` Remove brace padding | [`ZA3203`](rules/ZA3203-remove-block-padding.md) `remove-block-padding` | minimal | Judges the gap below a block header, since Python has no closing brace. `ruff format` keeps the blank line under `def`, and Black keeps it under every header. |
| `ZA0501` Name the arguments of a call | [`ZA3501`](rules/ZA3501-name-call-arguments.md) `name-call-arguments` | strong | Names a call only where the callee can be read from source, without importing or running code. A call through a value, a builtin or a callee whose decorator may change its signature stays as written. Python allows no positional argument after a name, so every argument after the first one named takes a name too. |
| `ZA0502` Name the literal arguments of a call | [`ZA3502`](rules/ZA3502-name-literal-arguments.md) `name-literal-arguments` | strong | Names `True`, `False` and `None` under the same reading of the callee, and the arguments after the first one it names. Ruff `FBT003` reports a `True` or `False` passed positionally and writes no name. |

## Covered by a rule above or by Python itself

| C# rule | Decision |
| --- | --- |
| `ZA0014` Separate modifier groups | Not needed, because Python has no modifiers; a decorated method is a definition. |
| `ZA0016` Separate the import directive groups | Not needed, because `isort` and Ruff `I001` own the groups of imports; `I001` separates an `import` line from a `from` line only when `lines-between-types` is `1`, and by default it joins them. |
| `ZA0056` Collapse extra blank lines | Not needed, because Black and Ruff cap the run at two blank lines at the top level and one in a body, and pycodestyle `E303` reports more. |
| `ZA0101` to `ZA0103` accessors | Not needed, because a property is a definition and the formatters own the gaps beside it. |
| `ZA0141`, `ZA0151`, `ZA0161` separate multi-line elements | Not needed, because `ruff format` and Black remove every blank line inside brackets, so a blank line between two multi-line elements would not survive. |
| `ZA0142`, `ZA0143`, `ZA0152`, `ZA0153`, `ZA0162`, `ZA0163` keep elements together | Not needed, because `ruff format` and Black remove every blank line inside brackets. |
| `ZA0121` to `ZA0123`, `ZA0131` to `ZA0133`, `ZA0171` to `ZA0173` | No such construct: Python has no switch expression, object initializer or declarator list. |

## Declined

These rules stay out because a formatter undoes their fix, their trigger has no reliable definition, or their finding has no edit at all.

| C# rule | Reason |
| --- | --- |
| `ZA0199` Remove the trailing comma | Black writes the comma back whenever it splits a collection one item per line, and Ruff keeps the comma a file has, so a removal fights both formatters. |
| JS `separate-test-blocks` | The Python counterpart has no reliable definition of the line that acts in a test. The formatters already separate top-level test functions, and `ZA3005` separates the run of assertions. |
| `ZA1005` Shorten a long condition, `ZA1006` Break up a long else-if chain, `ZA1007` Break up a long ternary chain | The three only count operators or branches and have no fix, and a finding of Python Stanza is always an edit. `check` exits with 1 for a finding, so it would report something `fix` cannot settle. Ruff `PLR0916`, a preview rule, counts the operators of an `if` condition, and `PLR0912` counts the branches of a whole function. No Ruff rule counts a chain of conditional expressions. |

## Not needed

| C# rule | Owner in the Python toolchain |
| --- | --- |
| `ZA0198` Add a trailing comma | Black and `ruff format` with the magic trailing comma, and Ruff `COM812`. |
| `ZA0204` Empty blocks | No such construct: an empty block is a `pass` statement, so no pair of braces exists to join. |
| `ZA0205`, `ZA0206` Braces | No braces. |
| `ZA0301` to `ZA0307` Member bodies | No expression bodies: a `def` has a block body, and `ruff format` writes a single-line `def` on separate lines, apart from a `...` stub. |
| `ZA0401` to `ZA0405` Regions and conditional blocks | No Python counterpart. |
| `ZA0503` Remove a redundant argument name | Removing a keyword makes the argument positional, which fails for a keyword-only parameter and rewrites a name the author chose. `named-argument-match` already lets `ZA3501` leave a leading identifier of its parameter's name unnamed. |
| `ZA0601` Remove the else after a jump | Ruff `RET505`, `RET506`, `RET507` and `RET508`. |
| `ZA0602` Merge nested if statements | Ruff `SIM102`. |
| `ZA0651`, `ZA0652` Null checks | Ruff `E711`, which reports `== None` and `!= None` and asks for `is None`. `E712` checks `True` and `False`, not `None`. |
| `ZA0901` to `ZA0914` Documentation comments | Ruff `D` rules and `ruff format` own the layout and punctuation of a docstring; `pydoclint` is not run here. Python docstrings have no XML tags, so `ZA0902`, `ZA0904`, `ZA0906` to `ZA0909` and `ZA0912` to `ZA0914` have no counterpart. |
| `ZA0951`, `ZA0952` Comment markers | pycodestyle `E262`, `E265` and `E266` for `ZA0951`. `ZA0952` has no counterpart, since Python has no `/* */` comment. |
| `ZA0953` Trailing comment | pycodestyle `E261`, and the formatters. |
| `ZA1001` to `ZA1003` Size | Ruff `E501` for the line length, `PLR0915` for the statements of a member and `PLR0913` for the parameters. |
| `ZA1004` Flatten deeply nested code | Ruff `PLR1702`, a preview rule that needs `--preview`, and pylint. |
| `ZA2001` to `ZA2010` Markup | No markup in Python code. |

## One owner per gap

A gap is the space between two neighboring units of a container, such as two statements of a function body.
The call rules own no gap, so the [list for calls](#one-owner-per-call) is separate.
Several rules can ask for a blank line at one gap, so a [shared analysis](Architecture.md#shared-analysis) assigns the gap to the first enabled rule in this order:

1. `ZA3017` `separate-file-header`
2. `ZA3015` `separate-import-section`
3. `ZA3001` `separate-multi-line-constructs`
4. `ZA3111` `separate-multi-line-cases`
5. `ZA3012` `separate-commented-siblings`
6. `ZA3201` `separate-clause-keywords`
7. `ZA3010` `separate-base-call`
8. `ZA3011` `separate-guard-clauses`
9. `ZA3009` `separate-jump-statements`
10. `ZA3013` `separate-member-groups`
11. `ZA3002` `separate-declaration-runs`
12. `ZA3003` `separate-same-type-declaration-runs`
13. `ZA3008` `separate-assignment-runs`
14. `ZA3005` `separate-assertion-runs`
15. `ZA3006` `separate-await-runs`
16. `ZA3007` `separate-subscription-runs`
17. `ZA3004` `separate-invocation-runs`

A rule that is off hands its gaps to the next rule that asks for them, as in the C# analyzers.
Only the report moves, because the rules of this list write the same blank line, so the order never changes the fixed text.
`ZA3051` is outside the list: the rules above ask for nothing in a container it judges.

## Rules that remove a blank line

A rule that removes a blank line owns a gap that holds blank lines and nothing else, and the rules above own the gaps that hold none.
The first enabled rule of this list that asks for a gap reports it, and `ZA3051` comes first:

- `ZA3051` `keep-two-units-together`, `ZA3202` `keep-clause-keywords-adjacent`,
  `ZA3112` `keep-multi-line-cases-together` and `ZA3113` `keep-single-line-cases-together`
  keep two units apart from the rules that write a blank line.
  A gap they ask for is no gap a rule above may ask for, so the gap settles on the tight text.
- `ZA3203` `remove-block-padding` judges the gap below a block header, which is no gap between two statements,
  so no other rule asks for it.
- `ZA3054` `keep-statement-runs-together`, `ZA3055` `keep-member-runs-together`,
  `ZA3052` `gather-loose-statements` and `ZA3053` `gather-loose-members` keep a run.
  They yield every gap that a rule above asks for.

## Pairs and their default halves

A pair is two rules that judge one gap in opposite ways, a separating rule and its tight rule, and a gap has one owner.
The default half owns the gap while `enable` names neither half, if the level holds that half.

| Gap | Halves | Default half | Owner when both are enabled |
| --- | --- | --- | --- |
| The blank line before a clause keyword | [`ZA3201`](rules/ZA3201-separate-clause-keywords.md) and [`ZA3202`](rules/ZA3202-keep-clause-keywords-adjacent.md) | `ZA3202` | `ZA3202` |
| The blank line between two `case` clauses when either spans several lines | [`ZA3111`](rules/ZA3111-separate-multi-line-cases.md) and [`ZA3112`](rules/ZA3112-keep-multi-line-cases-together.md) | `ZA3111` | `ZA3112` |

- Naming a rule of a pair in `enable` or `--enable` removes the other one from the level,
  so naming `ZA3201` turns `ZA3202` off and naming `ZA3112` turns `ZA3111` off.
- Naming both rules of a pair keeps both, and the tight half owns the gap: the separating half writes nothing there and the file ends up tight.
- The default halves come from the levels: `ZA3202` from `minimal` and `ZA3111` from `moderate`, so the `none` level leaves both gaps to no rule.
- `disable = ["ZA3111"]` leaves the gaps between two multi-line `case` clauses as written, and `ZA3112` and `ZA3113` keep judging theirs.
- `check`, `fix`, `format` and the flake8 plugin report no conflict when both halves are enabled, because they apply the owner and say nothing else.
- A `# noqa` that silences the owner leaves the gap as written, and the other half does not take it over.
- `ZA3113` and `ZA3051` are in no pair: no separating rule asks for the gaps they judge.

## One owner per call

A call has one owner, the first enabled call rule that plans names for it.
`ZA3501` `name-call-arguments` comes first and plans the names of a call that passes at least `named-argument-min-arguments` arguments.
`ZA3502` `name-literal-arguments` plans the names from the first literal of a call that `ZA3501` leaves.

- A rule that is off plans nothing, so the next rule can own the call.
- A `# noqa` that silences the owner leaves the call unnamed, and the other rule does not take it over.
- `named-argument-ignore` removes a call before any rule plans it.
- The call rules write no blank line and own no gap, so they never meet the rules above.
