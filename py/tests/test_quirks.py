"""The quirks of Python syntax: each case is a place where a blank line would split a unit, move a comment or touch content."""

from __future__ import annotations

from dataclasses import dataclass

import pytest

from nickelony_stanza import Config, Options, fix, lint
from tests.helpers import same_tree


@dataclass(frozen=True)
class Quirk:
    """A text the strong level fixes, or leaves alone when `after` is None."""

    id: str
    before: str
    after: str | None = None
    options: Options | None = None


QUIRKS = [
    # Comments that stay adjacent to the statement below them.
    Quirk(
        "a type: ignore comment travels with its statement",
        "a = 1\nb = 2\n# type: ignore\nc = foo(\n    1\n)\n",
        "a = 1\nb = 2\n\n# type: ignore\nc = foo(\n    1\n)\n",
    ),
    Quirk(
        "a pylint comment travels with its statement",
        "a = 1\nb = 2\n# pylint: disable=invalid-name\nC = foo(\n    1\n)\n",
        "a = 1\nb = 2\n\n# pylint: disable=invalid-name\nC = foo(\n    1\n)\n",
    ),
    Quirk(
        "a pragma comment travels with its statement",
        "a = 1\nb = 2\n# pragma: no cover\nraise E\n",
        "a = 1\nb = 2\n\n# pragma: no cover\nraise E\n",
    ),
    Quirk("a noqa comment does not lead a group", "a = 1\n# noqa: E501\nb = foo\nc = bar\n"),
    Quirk("a trailing pragma stays on its line", "a = 1  # pragma: no cover\nb = 2\nc = 3\n"),
    Quirk("a trailing fmt: skip stays on its line", "a = 1  # fmt: skip\nb = 2\nc = 3\n"),
    Quirk(
        "the comments of a block stay with the block",
        "x = 1\ny = 2\n# explain\n# the loop\nfor i in y:\n    pass\n",
        "x = 1\ny = 2\n\n# explain\n# the loop\nfor i in y:\n    pass\n",
    ),
    Quirk(
        "a deeper comment trails the block above",
        "if x:\n    a()\n    # trails\nb = foo(\n    1\n)\n",
        "if x:\n    a()\n    # trails\n\nb = foo(\n    1\n)\n",
    ),
    Quirk(
        "a comment at a lower indent is left alone",
        "def f():\n    a = 1\n# odd\n    b = foo(\n        1\n    )\n",
    ),
    # Docstrings.
    Quirk(
        "a function docstring keeps the line below it",
        'def f():\n    """Doc."""\n    for x in y:\n        pass\n    return 1\n',
        'def f():\n    """Doc."""\n    for x in y:\n        pass\n\n    return 1\n',
    ),
    Quirk(
        "a class docstring gets its blank line",
        'class A:\n    """Doc."""\n    x = 1\n    y = 2\n',
        'class A:\n    """Doc."""\n\n    x = 1\n    y = 2\n',
    ),
    Quirk("a module docstring gets its blank line", '"""Doc."""\nx = 1\ny = 2\n', '"""Doc."""\n\nx = 1\ny = 2\n'),
    Quirk(
        "a multi-line docstring is a unit of its own",
        '"""Doc.\n\nMore.\n"""\nimport os\n',
        '"""Doc.\n\nMore.\n"""\n\nimport os\n',
    ),
    # Definitions belong to the formatters.
    Quirk("a decorated function keeps its neighbors", "x = foo(\n    1\n)\n@dec\ndef f():\n    pass\n"),
    Quirk("a statement below a function is left alone", "def f():\n    pass\nx = foo(\n    1\n)\n"),
    Quirk(
        "a statement below a class is left alone",
        "class A:\n    pass\nx = 1\ny = 2\nrun()\n",
        "class A:\n    pass\nx = 1\ny = 2\n\nrun()\n",
    ),
    Quirk(
        "a statement below a block that ends in a function is left alone",
        "if a:\n    def f():\n        pass\nx = foo(\n    1\n)\n",
    ),
    Quirk(
        "overload stubs stay together",
        "@overload\ndef f(a: int) -> int: ...\n@overload\ndef f(a: str) -> str: ...\ndef f(a): return a\n",
    ),
    Quirk(
        "a method between attributes is left alone",
        "class A:\n    a = 1\n    def f(self):\n        pass\n    b = foo(\n        1\n    )\n",
    ),
    Quirk(
        "a nested function is left alone",
        "def f():\n    x = 1\n    def g():\n        pass\n    y = 2\n    z = 3\n",
    ),
    # Compound statements.
    Quirk(
        "if TYPE_CHECKING is a block",
        "import os\nif TYPE_CHECKING:\n    import sys\nx = 1\n",
        "import os\n\nif TYPE_CHECKING:\n    import sys\n\nx = 1\n",
    ),
    Quirk(
        "try except else finally is one statement",
        "x = 1\ny = 2\ntry:\n    a()\nexcept E:\n    b()\nelse:\n    c()\nfinally:\n    d()\nz = 3\n",
        "x = 1\ny = 2\n\ntry:\n    a()\nexcept E:\n    b()\nelse:\n    c()\nfinally:\n    d()\n\nz = 3\n",
    ),
    Quirk("an elif chain is one statement", "if a:\n    x()\nelif b:\n    y()\nelse:\n    z()\n"),
    Quirk(
        "a blank line before a clause keyword goes",
        "if a:\n    x()\n\nelif b:\n    y()\n\n\nelse:\n    z()\n",
        "if a:\n    x()\nelif b:\n    y()\nelse:\n    z()\n",
    ),
    Quirk(
        "a blank line before an except, else and finally goes",
        "try:\n    a()\n\nexcept E:\n    b()\n\nelse:\n    c()\n\nfinally:\n    d()\n",
        "try:\n    a()\nexcept E:\n    b()\nelse:\n    c()\nfinally:\n    d()\n",
    ),
    Quirk(
        "a clause keyword below a function keeps the blank line the formatter writes",
        "if a:\n\n    def f():\n        pass\n\nelse:\n    y()\n",
    ),
    Quirk(
        "the blank line above a comment above a clause keyword goes",
        "if a:\n    x()\n\n# otherwise\nelse:\n    y()\n",
        "if a:\n    x()\n# otherwise\nelse:\n    y()\n",
    ),
    Quirk(
        "an else that holds an if is no elif",
        "if a:\n    x()\n\nelse:\n    if b:\n        y()\n",
        "if a:\n    x()\nelse:\n    if b:\n        y()\n",
    ),
    Quirk(
        "the bodies of a try are separate containers",
        "try:\n    a = 1\n    b = 2\n    print(a)\nexcept E:\n    c = 1\n    d = 2\n    print(c)\n",
        "try:\n    a = 1\n    b = 2\n\n    print(a)\nexcept E:\n    c = 1\n    d = 2\n\n    print(c)\n",
    ),
    Quirk(
        "a with block is a multi-line statement",
        "a = 1\nwith open(p) as f:\n    f.read()\nb = 2\n",
        "a = 1\n\nwith open(p) as f:\n    f.read()\n\nb = 2\n",
    ),
    Quirk(
        "async with and async for are compound statements",
        "async def f():\n    x = 1\n    async with a:\n        pass\n    async for y in z:\n        pass\n",
        "async def f():\n    x = 1\n\n    async with a:\n        pass\n\n    async for y in z:\n        pass\n",
    ),
    Quirk(
        "global and nonlocal are declarations",
        "def f():\n    global a, b\n    nonlocal c\n    a = 1\n    b = 2\n    run()\n",
        "def f():\n    global a, b\n    nonlocal c\n\n    a = 1\n    b = 2\n\n    run()\n",
    ),
    # Joined lines.
    Quirk(
        "semicolon-joined statements share a line",
        "a = 1; b = 2\nc = 3\nrun()\n",
        "a = 1; b = 2\nc = 3\n\nrun()\n",
    ),
    Quirk(
        "a multi-line statement after a semicolon",
        "a = 1; b = foo(\n    2\n)\nc = 3\n",
        "a = 1; b = foo(\n    2\n)\n\nc = 3\n",
    ),
    Quirk("a single-line compound statement is one line", "a = 1\nif a: b = 3\nrun()\n"),
    Quirk(
        "a single-line guard",
        "def f(a):\n    if not a: raise ValueError\n    use(a)\n    stop(a)\n",
        "def f(a):\n    if not a: raise ValueError\n\n    use(a)\n    stop(a)\n",
    ),
    Quirk(
        "a backslash continuation spans lines",
        "x = 1\ny = 2 + \\\n    3\nz = 4\n",
        "x = 1\n\ny = 2 + \\\n    3\n\nz = 4\n",
    ),
    Quirk(
        "implicit joining inside brackets spans lines",
        "x = 1\ny = [\n    1,\n    2,\n]\nz = 4\n",
        "x = 1\n\ny = [\n    1,\n    2,\n]\n\nz = 4\n",
    ),
    # Content.
    Quirk(
        "blank lines inside a string are content",
        'x = """\n\nkept\n\n"""\ny = 2\n',
        'x = """\n\nkept\n\n"""\n\ny = 2\n',
    ),
    Quirk(
        "a multi-line f-string is one statement",
        'name = 1\nx = f"""\n{name}\n\ntext\n"""\ny = 2\n',
        'name = 1\n\nx = f"""\n{name}\n\ntext\n"""\n\ny = 2\n',
    ),
    Quirk("a hash inside a string is no comment", 'a = 1\nb = "# not a comment"\nc = 3\n'),
    Quirk(
        "__all__ is a declaration",
        'import os\n__all__ = ["os"]\nx = 1\ny = 2\n',
        'import os\n\n__all__ = ["os"]\nx = 1\ny = 2\n',
    ),
    Quirk(
        "the main guard is a multi-line statement",
        'x = 1\nif __name__ == "__main__":\n    main()\n',
        'x = 1\n\nif __name__ == "__main__":\n    main()\n',
    ),
    # Expressions inside statements.
    Quirk("yield statements are no run", "def g():\n    yield a\n    yield b\n    print(1)\n"),
    Quirk(
        "lambdas, comprehensions and walrus are plain declarations",
        "f = lambda: 1\ng = [x for x in y]\nrun()\n",
        "f = lambda: 1\ng = [x for x in y]\n\nrun()\n",
    ),
    Quirk(
        "an await in a declaration is a declaration",
        "async def f():\n    a = await x()\n    b = await y()\n    run()\n",
        "async def f():\n    a = await x()\n    b = await y()\n\n    run()\n",
    ),
    # Regions a formatter is told to leave alone.
    Quirk(
        "a fmt: off region keeps its blank lines",
        "# fmt: off\nx = foo(\n    1\n)\ny = bar(\n    2\n)\n# fmt: on\nz = 3\nw = 4\n",
    ),
    Quirk(
        "the gap above a fmt: off comment is fixed",
        "a = 1\nb = 2\n# fmt: off\nc = foo(\n    1\n)\n# fmt: on\n",
        "a = 1\nb = 2\n\n# fmt: off\nc = foo(\n    1\n)\n# fmt: on\n",
    ),
    # Whitespace and endings.
    Quirk(
        "tabs indent the same as spaces",
        "def f():\n\ta = 1\n\tb = 2\n\treturn a\n",
        "def f():\n\ta = 1\n\tb = 2\n\n\treturn a\n",
    ),
    Quirk("a CRLF file stays CRLF", "a = 1\r\nb = 2\r\nrun()\r\n", "a = 1\r\nb = 2\r\n\r\nrun()\r\n"),
    Quirk("a file without a final newline", "a = 1\nb = 2\nrun()", "a = 1\nb = 2\n\nrun()"),
    Quirk("an empty file", ""),
    Quirk("a file of comments", "# one\n\n# two\n"),
    Quirk("a form feed line is a blank line", "a = 1\nb = 2\n\x0c\nrun()\n"),
    Quirk("trailing whitespace on a blank line", "a = 1\nb = 2\n   \nrun()\n"),
]


@pytest.mark.parametrize("quirk", QUIRKS, ids=lambda quirk: quirk.id)
def test_quirk(quirk: Quirk) -> None:
    config = Config.create("strong", options=quirk.options)
    expected = quirk.before if quirk.after is None else quirk.after
    result = fix(quirk.before, config).text

    assert result == expected
    assert fix(result, config).text == result
    assert same_tree(quirk.before, result, config)
    assert lint(result, config) == []
