# ZA3501 - name-call-arguments

**Level: strong.**
Writes the name of the bound parameter before the positional arguments of a call that passes at least `named-argument-min-arguments` arguments,
from the first argument that needs a name to the last.

```python
# Before
def draw(width, height, smooth, path): ...


draw(640, 480, True, "image.png")

# After
def draw(width, height, smooth, path): ...


draw(width=640, height=480, smooth=True, path="image.png")
```

```python
# Before
def draw(width, height, smooth, path): ...


def frame(width, height):
    draw(width, height, True, "frame.png")

# After
def draw(width, height, smooth, path): ...


def frame(width, height):
    draw(width, height, smooth=True, path="frame.png")
```

- The count covers every argument the call writes, a named one included, and `0` turns the rule off.
- Python allows no positional argument after a named one, so the rule names every argument after the first one it names.
- Under `named-argument-match = "allow"`, the leading arguments that are an identifier of their parameter's name stay unnamed,
  as `width` and `height` do above. Under `"require"` they take the name too.
- An argument that already has a name is left as it is.
- An argument that fills a positional-only parameter, a mangled one such as `__key`,
  or the receiver of a method read from its class, as `Canvas.line(canvas, 0, 1)`, stays positional.
  A call where such an argument comes after one that needs a name is left alone, because no fix can finish it.
- The rule reports once per call, at the `(` of its argument list.
  A `# noqa: ZA3501` on that line, on the line where the call starts or on the first line of its statement silences it.
- [`ZA3502`](ZA3502-name-literal-arguments.md) names the literals of a call this rule leaves alone,
  and never takes a call this rule reports, a silenced one included.
- The fix moves each named argument into a keyword of the same call,
  and the tool refuses to write a file whose syntax tree changed in any other way.

## What it names

The rule names a call only when its callee resolves from source to one definition, read without importing or running any code:

- A function, a class, a dataclass or a named tuple of the file, of the project, of the standard library or of an installed package.
- A method called through `self`, `cls` or `super()`, when no class of the project that inherits it reaches another definition.
- A method or a class read from a class or from an imported module, such as `Canvas.line` or `shapes.draw`.

The rule leaves these calls alone, because it cannot tell which parameter each argument fills:

- A builtin or a function of a compiled module, such as `print` or `math.floor`, because it has no source.
- A call through a value: a local name or parameter that hides the callee, a method of a local variable or an attribute,
  a call result or a subscript, or a name assigned a value such as a lambda.
- A name bound more than once, bound inside an `if`, `try`, `with`, loop or `match`, declared `global` elsewhere,
  or bound by no statement the rule reads.
- A callee wrapped by a decorator the rule does not see through, such as `functools.lru_cache` or a decorator of the project,
  because the wrapper may take other arguments.
- A module that is missing, compiled, sourceless, unparsable or shadowed, or that a `.pyi` stub stands beside.
- A class whose constructor or metaclass the rule does not model, and a method that a class of the project which inherits it overrides.
- A callee that a project module replaces with a test double, as in `lib.f = other`, `setattr(lib, "f", fake)`,
  `monkeypatch.setattr(lib, "f", fake)`, `monkeypatch.setattr("lib.f", fake)`, `mock.patch("lib.f")` or `mock.patch.object(C, "m")`,
  because the double may take other names.
- Every callee with the same last name as a double, because the rule matches the last name of the callee against the names any project module patches.
- `typing.cast`, `NewType`, `TypeVar`, `ParamSpec`, `TypeVarTuple`, `TypeAliasType`, `NamedTuple` and `TypedDict`, also from `typing_extensions`,
  and `collections.namedtuple`, because a type checker reads their arguments positionally.
- A dunder method other than `__init__`, `__new__`, `__call__`, `__post_init__` and `__init_subclass__`, such as `self.__eq__(a)`,
  because the rule reads its parameters as positional-only, as a type checker does, so its arguments stay positional.
- A call that unpacks `*args` or `**kwargs`, or that passes more positional arguments than the callee takes.
- A call inside a `# fmt: off` region, or on a line that a trailing `# fmt: skip` comment trails,
  or in a simple statement whose last line it trails, because the formatter keeps that text as written.
- A call inside an f-string before Python 3.12, or with an argument that is a generator or a `:=` expression without parentheses of its own.
- Any call, when the project holds more than 20,000 files or 64 MB of source, because the checks for test doubles and subclasses need every module.

The first call this rule resolves makes a run read the whole project up front, so a run at `strong` pays that cost before it names a call.

A text read from standard input resolves against itself, the standard library and the installed packages.
Give its path with `--stdin-filename` so it resolves against the other modules of its project too.

## Options

| Key | Default | Effect |
| --- | --- | --- |
| `named-argument-min-arguments` | `4` | The fewest arguments a call writes before the rule names them, from 0, which turns the rule off, to 1000. |
| `named-argument-match` | `"allow"` | `"allow"` keeps a leading identifier of its parameter's name unnamed, and `"require"` names it too. |
| `named-argument-sources` | `["project", "stdlib", "installed"]` | Where a callee may be defined for its call to be named: the project, the standard library or an installed package. |
| `named-argument-methods` | `"all"` | The methods a call through `self`, `cls` or `super()` may name: `"all"`, the `"private"` ones that start with `_` and are no dunder, or `"none"`. |
| `named-argument-ignore` | `[]` | Shell-style globs of callees left alone, matched against the callee as written and its resolved dotted name, such as `"log.*"`. |
| `source-roots` | `[]` | The directories read as the project, relative to the configuration file, each as a whole tree. Empty detects them from each file. A filesystem root, a drive root or the home directory is a configuration error. |

`named-argument-ignore` takes a call away from both call rules before either judges it, while `# noqa` silences only the rule that owns it.

## C# counterpart

[ZA0501 - Name the arguments of a call](https://github.com/Nickelony/Stanza/wiki/ZA0501-Name-the-arguments-of-a-call),
whose options are [`stanza_named_argument_min_arguments`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_min_arguments)
and [`stanza_named_argument_match`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_match).
Python compares names case-sensitively, so it has no counterpart of
[`stanza_named_argument_case`](https://github.com/Nickelony/Stanza/wiki/stanza_named_argument_case).

## JS counterpart

None.
