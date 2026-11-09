# Quirks

Python syntax leaves a few places where a blank line would change a meaning, split a unit, move a comment or touch content,
and where a call cannot take a parameter name.
This page lists each place and the decision the rules take there.

## One owner per gap

A fix writes or removes blank lines, apart from the [parameter names](#calls) the call rules write, and never moves code.
The [shared analysis](Architecture.md#shared-analysis) reads each file once and gives each gap to one rule.

- A rule asks for a gap from the shape of the syntax tree and the comments alone, never from a blank line that is already there.
  Three rules read one: `ZA3009` ends its stanza at it, and `ZA3052` and `ZA3053` find a loose run by it.
- A rule that removes a blank line acts only on one that stands.
- A blank line another rule's fix writes stands where `ZA3009` already ends its stanza, so any fix order reaches one text.
- The first enabled rule in the [owner order](Rule-map.md#one-owner-per-gap) that asks for a gap reports it, and the others stay silent.
- A rule that is off asks for nothing, so the next rule that asks for the gap reports it.
- The one rule that reads other rules, `ZA3009`, ends its stanza at each gap that another enabled rule asks for.
- A container of exactly two single-line units belongs to `ZA3051` alone, as the [next section](#two-single-line-units) says.
  A function docstring above them does not count as a unit.
- A rule that keeps a pair apart wins a gap over the rules that write a blank line.
- A rule that keeps a run yields every gap that a rule which writes a blank line asks for, as the [architecture](Architecture.md#roles) says.
- A gap is satisfied when a blank line stands anywhere between the previous unit and the first line of the next unit's leading comments.

## Two single-line units

`ZA3051` removes the blank line between the only two units of a container when each spans one line.
The rules that add a blank line ask for nothing in such a container, so the two kinds of rule never meet.
A function docstring is documentation and no step of the code, so it does not count as a unit.

| Quirk | Decision |
| --- | --- |
| A container | A module, a class body, a function body, or the body of any compound statement. |
| A comment between the two units, a trailing one included | Keeps the blank line. |
| A multi-line unit, including a statement that continues with a backslash | Keeps the blank line, and `ZA3001` asks for it. |
| A function, a class, an import, a shebang or a module or class docstring | The container is skipped, because a formatter or another rule owns the gap. |
| A function docstring above two single-line statements | The docstring is no unit, so the two statements are the pair and the blank line between them goes. |
| A single-line function docstring above one statement | Judged: the blank line goes, as pydocstyle `D202` asks. |
| Two statements on one line | Joined, so the rule leaves them. |
| A guard above one statement, or a declaration above a `return` | Tight. The guard and jump rules judge a stanza of at least two units. |

## Comments

| Quirk | Decision |
| --- | --- |
| A comment directly above a statement, such as `# type: ignore`, `# noqa`, `# pragma: no cover`, `# pylint: disable=...` or `# fmt: skip` | It belongs to the statement. The blank line goes above the comment, so the comment stays adjacent. |
| A comment that trails the previous statement, as in `a = 1  # note` then `b = 2` | It belongs to the previous statement. Only a comment on a later line can lead the next one. |
| A comment separated from the statement below by a blank line | It is detached, and a blank line that stands between it and the statement satisfies the gap. |
| A tool directive, a comment that opens with `type:`, `noqa`, `pragma`, `fmt:`, `yapf:`, `pylint:`, `pyright:`, `pyre-`, `mypy:`, `isort:`, `flake8:`, `ruff:`, `nosec`, `noinspection` or `pytype:` in any case | `ZA3012` and the clause rules read this one definition. |
| A tool directive above a statement | It stays attached, but `ZA3012` ignores it, because it annotates a line and does not introduce a group. |
| A comment indented deeper than the statement below, as at the end of an `if` body | It trails the block above, so the blank line goes below it. |
| A comment indented less than the statement below | The gap is left as written, because the owner of the comment is unclear. |
| A comment inside a unit | Never read. Only the lines between two units count. |
| A region between `# fmt: off` and `# fmt: on`, or `# yapf: disable` and `# yapf: enable` | Every gap inside keeps its lines, and the gap above the `# fmt: off` comment is judged as usual. A call inside gets no name. |
| A `# fmt: skip` comment that trails code | Every gap is judged as usual. A call gets no name when the comment trails one of its lines or the last line of the simple statement that holds it. |

## Docstrings

| Quirk | Decision |
| --- | --- |
| The module docstring | A unit that opens the file, or follows the shebang and encoding lines. `ZA3017` asks for the blank line below it. |
| The class docstring | `ZA3013` asks for the blank line below it, as PEP 257 and both formatters do. |
| The function docstring | No rule asks for a blank line below it, because pydocstyle `D202` forbids one. `ZA3051` does not count it as a unit. `ZA3203` removes the blank line above it, as `D201` asks. |
| A string below the first statement | Not a docstring. It is a statement like any other. |
| The shebang and the encoding line | One unit, the preamble. `ZA3017` asks for the blank line below it. The encoding line counts on line one, or on line two under a comment. |

## Definitions

| Quirk | Decision |
| --- | --- |
| A function or class, with its decorators | One unit that starts at the first decorator. The gaps beside it belong to the formatter and are left as written. |
| A statement below a function or class, or below a block that ends in one | Left alone, because the formatter forces the blank lines there. |
| `@overload` stubs and a `def f(): ...` on one line | Definitions, so the gaps stay as written. |
| `if TYPE_CHECKING:` | A block like any other. The imports inside it form a section of their own. |

## Statements

| Quirk | Decision |
| --- | --- |
| `if`, `elif` and `else` | One statement. The gap before a keyword between two blocks is judged by `ZA3201` and `ZA3202` alone, as the [next section](#clause-keywords) says. |
| `try`, `except`, `else` and `finally` | One statement, and each clause body is a container of its own. |
| `with` and `async with`, `for` and `async for`, `while` | Multi-line statements, unless written on one line. |
| `match` | The statement is one unit. The gap between two `case` clauses is judged by `ZA3111`, `ZA3112` and `ZA3113`, and the statements around it by `ZA3001`. |
| A call through `super()` | A base call, which `ZA3010` sets apart when it opens or closes a function body. Any other call through `super()` is an invocation. |
| `signal.connect(handler)` | A subscription while `ZA3007` is enabled and `subscription-roots` matches the callee, else an invocation. |
| `global` and `nonlocal` | Declarations of a kind each, so a run of `global` names stands apart from a run of assignments. |
| `yield` and `yield from` | Plain statements, not a run. |
| A walrus, a lambda or a comprehension inside a statement | Part of the statement. They never change which run it joins. |
| `__all__ = [...]` | A declaration like any other. |
| `if __name__ == "__main__":` | A multi-line statement like any other. |
| `import` and `from ... import` | A run of imports ends at the first statement below it. Two multi-line imports in a row stay together. |

## Clause keywords

The gap before an `elif`, `else`, `except`, `except*` or `finally` is a gap of its own,
between the last statement of the block above and the line of the keyword.

| Quirk | Decision |
| --- | --- |
| A keyword on a line of its own, as the formatters write it | The gap lies between the last line of the block and the keyword. |
| A comment at the indent of the keyword | It leads the keyword, so `ZA3201` writes the blank line above the comment. |
| A comment indented deeper than the keyword | It trails the block above, so `ZA3201` writes the blank line below it and `ZA3202` removes the blank lines below it. |
| A comment in the gap | `ZA3202` removes the blank lines above the first comment and keeps the ones below it, as `ZA0202` does. |
| A tool directive in the gap, such as `# noqa` or `# fmt: off` | The gap stays as written, as a preprocessor directive keeps a gap in C#. Any other comment follows the rows above. |
| A block that ends in a function or class | The gap belongs to the formatter, which writes a blank line there, and no rule asks for it. |
| An `else:` that holds an `if` | An `else` clause and no `elif`, because the keyword stands on its own line. |
| A `case` clause | No clause keyword. `ZA3111`, `ZA3112` and `ZA3113` judge the gap between two cases. |

## Block headers

The gap below a header is between its last line, the one that holds the colon, and the first unit of the body.
`ZA3203` alone judges it.

| Quirk | Decision |
| --- | --- |
| A header on several lines | The gap starts below the line that holds the colon. |
| A body on the line of its header, as in `if ok: start()` | One line, so there is no gap. |
| A header with a trailing comment | The comment belongs to the header, and the blank lines below it go. |
| A comment in the gap | The blank lines above the first comment go, and the ones between comments or below the last comment stay. |
| A tool directive in the gap, or a `# fmt: off` region | The gap stays as written. |
| A comment indented less than the body | The gap stays as written. |
| A function or class docstring | The first unit of the body, so the blank lines above it go. The ones below it are another rule's gap. |
| `elif` and `else` | Headers of their own. The line of the keyword is the end of the header. |
| `match` | The gap lies between the `match` line and the first `case`, and between each `case` line and its body. |
| A function or class below an `if`, `else`, `try`, `with`, `case` or loop header | Left alone, because the formatters write a blank line there. A comment above the definition frees the gap. |
| A function or class below a `def` or `class` header | Free, so the blank lines go. |

## Gaps no rule owns

A gap is joined, and the rules leave it as written, when a blank line would split a pair or change a meaning.

| Quirk | Decision |
| --- | --- |
| Two statements on one line, as in `a = 1; b = 2` | Joined. |
| A single-line compound statement, as in `if ok: return` | One line. The statement is a guard when it jumps. |
| A backslash continuation | The statement spans the lines it joins, so no gap lies inside it. |
| A line inside brackets | Part of the statement. A blank line there is never read. |
| A form feed or a line of spaces | A blank line. |

## Content a rule never touches

- A string, a triple-quoted string and an f-string are inside a unit, so no gap reaches them.
- The fix inserts a line break at the start of the line of the next unit, so it never lands inside a string.
- A name goes before the first token of an argument, so it never lands inside a string either.
  A call inside an f-string gets names on Python 3.12 and later, and is skipped before, because the tokenizer reads the whole f-string as one token.
- A line break uses the line ending the file already uses, so a CRLF file stays CRLF, and the encoding and the byte order mark stay as read.
- After a fix, the tool compares the syntax tree with the expected one, as the [architecture](Architecture.md#engine) says,
  and refuses to write a file whose tree differs.
- A file whose tree is too deeply nested to compare, such as an expression of 1,200 terms, is an error after a fix, so the file is left as written.
  `check` still reports its findings.

## Loose and kept runs

| Quirk | Decision |
| --- | --- |
| A loose run | A chain of single-line statements, or of class members, with blank lines alone between neighbors. |
| A comment, a trailing comment or a multi-line unit | Ends the run, and `ZA3052` to `ZA3055` keep its blank lines. |
| A jump, an import, a function or a class | It is no part of a loose run. |
| A unit tight against its other neighbor | It stays out of the run, unless a rule that writes a blank line asks for that gap. |
| A gap that a rule which writes a blank line asks for | It stays blank, and it ends the run. |
| Two declarations of different types | One run for `ZA3054` while `ZA3003` is off, and two runs while it is enabled. |
| A blank line between two sections of a run | `ZA3054` and `ZA3055` keep it when each side holds at least two units tight against each other, and a lone unit between two sections is a section. |

## Calls

A call rule names an argument only when it can tell which parameter the argument fills, so it leaves a call alone at the first doubt.
The [rule page](rules/ZA3501-name-call-arguments.md#what-it-names) lists the callees it reads, and this table lists the places it stops.

| Quirk | Decision |
| --- | --- |
| A builtin or a function of a compiled module, such as `print` or `math.floor` | Left alone, because it has no source. |
| A call through a value: a local name or parameter that hides the callee, a method of a local variable or an attribute, a call result or a subscript, or a name assigned a value such as a lambda | Left alone, because the class of the value is unknown. |
| A name bound more than once, inside an `if`, `try`, `with`, loop or `match`, declared `global` elsewhere, or bound by no statement the rule reads | Left alone, because the binding that runs is unknown. |
| A decorator that may change the signature, such as `functools.lru_cache` or a decorator of the project | Left alone. A few that keep the signature, such as `staticmethod`, `classmethod` and `abc.abstractmethod`, are read through. |
| A module that is missing, compiled, sourceless, unparsable or shadowed, or beside which a `.pyi` stub stands | Left alone, because the rule cannot read the one signature that applies. |
| A callee from a source that `named-argument-sources` leaves out, or a method that `named-argument-methods` excludes | Left alone, as the [options](rules/ZA3501-name-call-arguments.md#options) say. |
| A class shape the rule does not model, such as a custom `__new__`, a metaclass or a base it cannot read | Left alone. A dataclass and a named tuple take their fields in declaration order. |
| An exception class whose `__init__` does not call `super().__init__(...)` as a statement of its own | Left alone, because `BaseException` keeps the arguments it is given. |
| `Class.method(obj, ...)` | The receiver stays positional and the names start at the next argument. |
| A positional-only or mangled parameter | Stays positional. A call where one follows an argument that needs a name is left alone. |
| A callee that a test double replaces, a form a type checker reads positionally, or a dunder method | Left alone or kept positional, as the [rule page](rules/ZA3501-name-call-arguments.md#what-it-names) lists. |
| A call with `*args` or `**kwargs`, or with more positional arguments than the callee takes | Left alone. |
| An argument that is a generator or a `:=` expression without parentheses of its own | The call is left alone, because a name cannot stand in front of it. |
| A region between `# fmt: off` and `# fmt: on`, or a call that a trailing `# fmt: skip` comment covers | Left alone, as the [comments table](#comments) says. |
| A call inside an f-string before Python 3.12 | Left alone, as the [content section](#content-a-rule-never-touches) says. |
| A call through `self`, `cls` or `super()` | Named only when no project class that inherits the method reaches another definition. |
| Any call, when the project passes the size caps of its scan | Left alone, because the checks for test doubles and subclasses read every module, as the [architecture](Architecture.md#the-project-index) says. |

## Out of scope

- Stub files, `.pyi`, are skipped, because Black and Ruff lay stubs out with other blank-line rules.
- Notebooks are not read.
- A file that does not parse with the running interpreter is an error, and the other files are still handled.
