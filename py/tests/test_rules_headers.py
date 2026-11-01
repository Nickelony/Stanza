"""One case for each behavior of the rule that removes the blank lines below a block header."""

from __future__ import annotations

import pytest

from nickelony_stanza import Config, fix
from tests.helpers import Case, case, check_case, findings_with, fix_with

RULE = "ZA3203"

CASES = [
    # One case for each kind of header.
    case("headers: def", RULE, "def f():\n\n    x = 1\n", "def f():\n    x = 1\n"),
    case("headers: async def", RULE, "async def f():\n\n    x = 1\n", "async def f():\n    x = 1\n"),
    case("headers: class", RULE, "class K:\n\n    x = 1\n", "class K:\n    x = 1\n"),
    case("headers: if", RULE, "if a:\n\n    x = 1\n", "if a:\n    x = 1\n"),
    case(
        "headers: elif and else",
        RULE,
        "if a:\n    x = 1\nelif b:\n\n    x = 2\nelse:\n\n    x = 3\n",
        "if a:\n    x = 1\nelif b:\n    x = 2\nelse:\n    x = 3\n",
    ),
    case("headers: for", RULE, "for i in y:\n\n    x = 1\n", "for i in y:\n    x = 1\n"),
    case(
        "headers: async for",
        RULE,
        "async def f():\n    async for i in y:\n\n        x = 1\n",
        "async def f():\n    async for i in y:\n        x = 1\n",
    ),
    case("headers: while", RULE, "while a:\n\n    x = 1\n", "while a:\n    x = 1\n"),
    case("headers: with", RULE, "with a as b:\n\n    x = 1\n", "with a as b:\n    x = 1\n"),
    case(
        "headers: async with",
        RULE,
        "async def f():\n    async with a:\n\n        x = 1\n",
        "async def f():\n    async with a:\n        x = 1\n",
    ),
    case(
        "headers: the else of a loop",
        RULE,
        "for i in y:\n    x = 1\nelse:\n\n    x = 2\n",
        "for i in y:\n    x = 1\nelse:\n    x = 2\n",
    ),
    case(
        "headers: try, except, else and finally",
        RULE,
        "try:\n\n    a()\nexcept E:\n\n    b()\nelse:\n\n    c()\nfinally:\n\n    d()\n",
        "try:\n    a()\nexcept E:\n    b()\nelse:\n    c()\nfinally:\n    d()\n",
    ),
    case(
        "headers: except*",
        RULE,
        "try:\n\n    a()\nexcept* E:\n\n    b()\n",
        "try:\n    a()\nexcept* E:\n    b()\n",
    ),
    case(
        "headers: match and case",
        RULE,
        "match v:\n\n    case 1:\n\n        a()\n    case 2:\n\n        b()\n",
        "match v:\n    case 1:\n        a()\n    case 2:\n        b()\n",
    ),
    case(
        "headers: a case with a guard",
        RULE,
        "match v:\n    case 1 if a:\n\n        b()\n",
        "match v:\n    case 1 if a:\n        b()\n",
    ),
    # The size of the gap.
    case("headers: several blank lines", RULE, "if a:\n\n\n\n    x = 1\n", "if a:\n    x = 1\n"),
    case("headers: a line of spaces is a blank line", RULE, "if a:\n   \n    x = 1\n", "if a:\n    x = 1\n"),
    case("headers: a body that already touches the header", RULE, "if a:\n    x = 1\n"),
    case("headers: a single statement body", RULE, "def f():\n\n    return 1\n", "def f():\n    return 1\n"),
    case(
        "headers: a header of several lines",
        RULE,
        "def f(\n    a,\n    b,\n) -> int:\n\n    return a\n",
        "def f(\n    a,\n    b,\n) -> int:\n    return a\n",
    ),
    case(
        "headers: a trailing comment on the header",
        RULE,
        "def f():  # note\n\n    x = 1\n",
        "def f():  # note\n    x = 1\n",
    ),
    case(
        "headers: a colon inside a string of the header",
        RULE,
        "if a == ':':\n\n    x = 1\n",
        "if a == ':':\n    x = 1\n",
    ),
    case(
        "headers: nested blocks",
        RULE,
        "def f():\n\n    if a:\n\n        for i in y:\n\n            x = 1\n",
        "def f():\n    if a:\n        for i in y:\n            x = 1\n",
    ),
    case(
        "headers: a class with methods",
        RULE,
        "class K:\n\n    def f(self):\n\n        return 1\n",
        "class K:\n    def f(self):\n        return 1\n",
    ),
    case(
        "headers: the header at the end of the file",
        RULE,
        "x = 1\nif a:\n\n    y = 1\n",
        "x = 1\nif a:\n    y = 1\n",
    ),
    case("headers: tabs", RULE, "if a:\n\n\tx = 1\n", "if a:\n\tx = 1\n"),
    case("headers: CRLF", RULE, "if a:\r\n\r\n    x = 1\r\n", "if a:\r\n    x = 1\r\n"),
    case("headers: CR", RULE, "if a:\r\r    x = 1\r", "if a:\r    x = 1\r"),
    # A single-line block has no gap.
    case("headers: a body on the header line", RULE, "if a: x = 1\n\ny = 2\n"),
    case("headers: a def on one line", RULE, "def f(): ...\n\n\nx = 1\n"),
    case("headers: a body behind a continuation", RULE, "if a: \\\n    x = 1\n"),
    # The first line of the body.
    case(
        "headers: the blank line before a function docstring goes",
        RULE,
        'def f():\n\n    """Doc."""\n    return 1\n',
        'def f():\n    """Doc."""\n    return 1\n',
    ),
    case(
        "headers: the blank line before a class docstring goes",
        RULE,
        'class K:\n\n    """Doc."""\n\n    x = 1\n',
        'class K:\n    """Doc."""\n\n    x = 1\n',
    ),
    case(
        "headers: the blank line below the docstring stays",
        RULE,
        'def f():\n    """Doc."""\n\n    return 1\n',
    ),
    case(
        "headers: the blank line before an import goes",
        RULE,
        "def f():\n\n    import os\n\n    return os\n",
        "def f():\n    import os\n\n    return os\n",
    ),
    case(
        "headers: a def inside a def is free",
        RULE,
        "def f():\n\n    def g():\n        pass\n",
        "def f():\n    def g():\n        pass\n",
    ),
    case(
        "headers: a decorated method of a class is free",
        RULE,
        "class K:\n\n    @property\n    def f(self):\n        pass\n",
        "class K:\n    @property\n    def f(self):\n        pass\n",
    ),
    case(
        "headers: a class inside a class is free",
        RULE,
        "class K:\n\n    class A:\n        pass\n",
        "class K:\n    class A:\n        pass\n",
    ),
    case(
        "headers: a nested block that ends in a function is free",
        RULE,
        "def f():\n\n    if a:\n        def g():\n            pass\n",
        "def f():\n    if a:\n        def g():\n            pass\n",
    ),
    # A function or class that opens a block of another kind has the blank line the formatters write.
    case("headers: a def below an if keeps its blank line", RULE, "if a:\n\n    def g():\n        pass\n"),
    case(
        "headers: a class below an else keeps its blank line",
        RULE,
        "if a:\n    x = 1\nelse:\n\n    class A:\n        pass\n",
    ),
    case(
        "headers: a decorated def below a with keeps its blank line",
        RULE,
        "with a:\n\n    @d\n    def g():\n        pass\n",
    ),
    case(
        "headers: a def below a try keeps its blank line",
        RULE,
        "try:\n\n    def g():\n        pass\nexcept E:\n    pass\n",
    ),
    case(
        "headers: a def below a case keeps its blank line",
        RULE,
        "match v:\n    case 1:\n\n        def g():\n            pass\n",
    ),
    case(
        "headers: several blank lines before a def below an if are left to the formatters",
        RULE,
        "if a:\n\n\n    def g():\n        pass\n",
    ),
    case(
        "headers: a comment before a def below an if frees the gap",
        RULE,
        "if a:\n\n    # about g\n    def g():\n        pass\n",
        "if a:\n    # about g\n    def g():\n        pass\n",
    ),
    # Comments.
    case(
        "headers: the blank line above a comment goes",
        RULE,
        "def f():\n\n    # note\n    x = 1\n",
        "def f():\n    # note\n    x = 1\n",
    ),
    case("headers: a comment already tight under the header stays", RULE, "def f():\n    # note\n    x = 1\n"),
    case("headers: the blank line below the comment stays", RULE, "def f():\n    # note\n\n    x = 1\n"),
    case(
        "headers: only the blank lines above the first comment go",
        RULE,
        "if a:\n\n\n    # one\n\n    # two\n\n    x = 1\n",
        "if a:\n    # one\n\n    # two\n\n    x = 1\n",
    ),
    case(
        "headers: the blank line between a comment and the body stays",
        RULE,
        "if a:\n\n    # note\n\n    x = 1\n",
        "if a:\n    # note\n\n    x = 1\n",
    ),
    case(
        "headers: a comment above a docstring",
        RULE,
        'class K:\n\n    # note\n\n    """Doc."""\n',
        'class K:\n    # note\n\n    """Doc."""\n',
    ),
    case(
        "headers: an ordinary comment that names a tool is a comment",
        RULE,
        "if a:\n\n    # the type: ignore below\n    x = 1\n",
        "if a:\n    # the type: ignore below\n    x = 1\n",
    ),
    case("headers: a comment at a lower indent is left alone", RULE, "def f():\n\n# odd\n    x = 1\n"),
    case("headers: a comment at the indent of the header is left alone", RULE, "def f():\n# odd\n\n    x = 1\n"),
    case(
        "headers: a comment indented deeper than the body",
        RULE,
        "if a:\n\n        # deep\n    x = 1\n",
        "if a:\n        # deep\n    x = 1\n",
    ),
    # A tool directive in the gap keeps the gap as written.
    case("headers: a noqa comment in the gap keeps it", RULE, "def f():\n\n    # noqa\n    x = 1\n"),
    case("headers: a type comment in the gap keeps it", RULE, "def f():\n    # type: ignore\n\n    x = 1\n"),
    case(
        "headers: a directive below a comment keeps the gap",
        RULE,
        "if a:\n\n    # note\n    # pragma: no cover\n    x = 1\n",
    ),
    case("headers: a fmt: off region keeps its blank lines", RULE, "# fmt: off\nif a:\n\n    x = 1\n# fmt: on\n"),
    case(
        "headers: a fmt: off comment in the gap keeps it",
        RULE,
        "if a:\n\n    # fmt: off\n    x = 1\n    # fmt: on\n",
    ),
    case(
        "headers: the gap after a fmt: on comment is judged again",
        RULE,
        "# fmt: off\nx = 1\n# fmt: on\nif a:\n\n    y = 1\n",
        "# fmt: off\nx = 1\n# fmt: on\nif a:\n    y = 1\n",
    ),
]


@pytest.mark.parametrize("item", CASES, ids=lambda item: item.id)
def test_rule_case(item: Case) -> None:
    check_case(item)


DIRECTIVES = [
    "# type: ignore",
    "# noqa: E501",
    "# pragma: no cover",
    "# pylint: disable=unused-argument",
    "# pyright: ignore",
    "# mypy: ignore-errors",
    "# isort: skip",
    "# ruff: noqa",
    "# nosec",
    "# fmt: skip",
    "# fmt: off",
    "# yapf: disable",
]


@pytest.mark.parametrize("directive", DIRECTIVES)
def test_a_tool_directive_in_the_gap_keeps_the_gap_as_written(directive: str) -> None:
    for text in (
        f"def f():\n    {directive}\n    x = 1\n",
        f"def f():\n\n    {directive}\n    x = 1\n",
        f"def f():\n\n\n    {directive}\n\n    x = 1\n",
        f"def f():\n\n    # note\n\n    {directive}\n    x = 1\n",
    ):
        assert fix_with(text, RULE) == text


def test_a_noqa_comment_on_the_first_line_of_the_body_silences_the_blank_line() -> None:
    text = "def f():\n\n    x = 1  # noqa: ZA3203\n"
    assert fix_with(text, RULE) == text


def test_a_noqa_comment_names_the_family() -> None:
    text = "def f():\n\n    x = 1  # noqa: ZA3\n"
    assert fix_with(text, RULE) == text


def test_a_finding_points_at_the_first_blank_line() -> None:
    finding = findings_with("class K:\n    x = 1\n\n    def f(self):\n\n\n        return 1\n", RULE)[0]
    assert (finding.code, finding.line, finding.column) == (RULE, 5, 9)


def test_the_rule_is_in_the_minimal_level() -> None:
    assert RULE in {rule.code for rule in Config.create("minimal").rules}


def test_the_other_rules_never_ask_for_the_gap_below_a_header() -> None:
    text = "def f():\n\n    import os\n    x = 1\n\n    return x\n"
    assert fix(text, Config.create("strong")).text == "def f():\n    import os\n\n    x = 1\n\n    return x\n"


def test_the_gap_below_a_header_is_independent_of_the_gaps_between_the_statements() -> None:
    text = "def f():\n\n    a = 1\n\n    return a\n"

    assert fix_with(text, RULE) == "def f():\n    a = 1\n\n    return a\n"
    assert fix_with(text, RULE, "ZA3051") == "def f():\n    a = 1\n    return a\n"
    assert fix_with(text, "ZA3051") == "def f():\n\n    a = 1\n    return a\n"


def test_a_docstring_then_the_header_gap_and_the_pair_gap_settle_together() -> None:
    text = 'def f():\n\n    """Doc."""\n\n    a = 1\n\n    return a\n'
    assert fix_with(text, RULE, "ZA3051") == 'def f():\n    """Doc."""\n\n    a = 1\n    return a\n'


def test_a_class_docstring_keeps_the_blank_line_the_member_rule_asks_for() -> None:
    text = 'class K:\n\n    """Doc."""\n    x = 1\n    y = 2\n'
    assert fix_with(text, RULE, "ZA3013") == 'class K:\n    """Doc."""\n\n    x = 1\n    y = 2\n'
