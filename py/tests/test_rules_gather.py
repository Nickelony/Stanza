"""One case for each behavior of the rules that gather loose statements and members, and how their findings settle."""

from __future__ import annotations

import pytest

from nickelony_stanza import Config, Options, fix, lint
from tests.helpers import Case, apply_each_finding, case, check_case, fix_in_steps, fix_with

CASES = [
    # ZA3052 gather-loose-statements
    case(
        "loose statements: a run past the limit",
        "ZA3052",
        "a = 1\n\nb = f()\n\nc = g()\n\nd = h()\n",
        "a = 1\nb = f()\nc = g()\nd = h()\n",
    ),
    case("loose statements: a run at the limit", "ZA3052", "a = 1\n\nb = f()\n\nc = g()\n"),
    case(
        "loose statements: a smaller limit",
        "ZA3052",
        "a = 1\n\nb = f()\n\nc = g()\n",
        "a = 1\nb = f()\nc = g()\n",
        Options(max_loose_run_size=2),
    ),
    case(
        "loose statements: a limit of zero gathers nothing",
        "ZA3052",
        "a = 1\n\nb = 2\n\nc = 3\n\nd = 4\n",
        None,
        Options(max_loose_run_size=0),
    ),
    case(
        "loose statements: a statement that sits tight against its other neighbor stays out",
        "ZA3052",
        "a = 1\n\nb = f()\n\nc = g()\n\nd = h()\ne = i()\n",
        "a = 1\nb = f()\nc = g()\n\nd = h()\ne = i()\n",
        Options(max_loose_run_size=2),
    ),
    case("loose statements: a comment ends the run", "ZA3052", "a = 1\n\nb = f()\n\n# note\nc = g()\n\nd = h()\n"),
    case(
        "loose statements: a trailing comment ends the run",
        "ZA3052",
        "a = 1\n\nb = f()  # note\n\nc = g()\n\nd = h()\n\ne = i()\n",
        "a = 1\n\nb = f()  # note\n\nc = g()\nd = h()\ne = i()\n",
        Options(max_loose_run_size=2),
    ),
    case(
        "loose statements: a multi-line statement ends the run",
        "ZA3052",
        "a = 1\n\nb = f()\n\nc = g(\n    1\n)\n\nd = h()\n\ne = i()\n",
    ),
    case(
        "loose statements: a jump is no part of a run",
        "ZA3052",
        "def f():\n    a = 1\n\n    b = f()\n\n    c = g()\n\n    d = h()\n\n    return d\n",
        "def f():\n    a = 1\n    b = f()\n    c = g()\n    d = h()\n\n    return d\n",
    ),
    case("loose statements: imports are no statements", "ZA3052", "import a\n\nimport b\n\nimport c\n\nimport d\n"),
    case(
        "loose statements: a function body",
        "ZA3052",
        "def f():\n    a = 1\n\n    b = 2\n\n    c = 3\n\n    d = 4\n",
        "def f():\n    a = 1\n    b = 2\n    c = 3\n    d = 4\n",
    ),
    case(
        "loose statements: a class body belongs to the member rule",
        "ZA3052",
        "class A:\n    a = 1\n\n    b = 2\n\n    c = 3\n\n    d = 4\n",
    ),
    case(
        "loose statements: a run below a function",
        "ZA3052",
        "def f():\n    pass\n\n\na = 1\n\nb = 2\n\nc = 3\n\nd = 4\n",
        "def f():\n    pass\n\n\na = 1\nb = 2\nc = 3\nd = 4\n",
    ),
    # ZA3053 gather-loose-members
    case(
        "loose members: fields past the limit",
        "ZA3053",
        "class A:\n    a: int\n\n    b: int\n\n    c: int\n\n    d: int\n",
        "class A:\n    a: int\n    b: int\n    c: int\n    d: int\n",
    ),
    case("loose members: fields at the limit", "ZA3053", "class A:\n    a: int\n\n    b: int\n\n    c: int\n"),
    case(
        "loose members: methods are definitions",
        "ZA3053",
        "class A:\n    a: int\n\n    def f(self): ...\n\n    b: int\n\n    c: int\n\n    d: int\n\n    e: int\n",
        "class A:\n    a: int\n\n    def f(self): ...\n\n    b: int\n    c: int\n    d: int\n    e: int\n",
    ),
    case(
        "loose members: the class docstring keeps its blank line",
        "ZA3053",
        'class A:\n    """Doc."""\n\n    a = 1\n\n    b = 2\n\n    c = 3\n\n    d = 4\n',
        'class A:\n    """Doc."""\n\n    a = 1\n    b = 2\n    c = 3\n    d = 4\n',
    ),
    case(
        "loose members: statements of a function belong to the statement rule",
        "ZA3053",
        "def f():\n    a = 1\n\n    b = 2\n\n    c = 3\n\n    d = 4\n",
    ),
]


@pytest.mark.parametrize("item", CASES, ids=lambda item: item.id)
def test_rule_case(item: Case) -> None:
    check_case(item)


def test_a_gather_rule_never_removes_a_blank_line_a_separating_rule_writes_back() -> None:
    text = "a = 1\n\nb = 2\n\nc = foo(\n    1\n)\n\nd = 4\n\ne = 5\n\nf = 6\n"
    config = Config.create("none", enable=["ZA3001", "ZA3052"], options=Options(max_loose_run_size=2))
    expected = "a = 1\n\nb = 2\n\nc = foo(\n    1\n)\n\nd = 4\ne = 5\nf = 6\n"

    assert fix(text, config).text == expected
    assert fix(expected, config).text == expected


def test_a_gather_rule_keeps_the_blank_line_above_a_jump_where_the_jump_rule_is_enabled() -> None:
    text = "def f():\n    a = 1\n\n    b = 2\n\n    c = 3\n\n    d = 4\n\n    return d\n"
    config = Config.create("none", enable=["ZA3013", "ZA3052"])
    expected = "def f():\n    a = 1\n    b = 2\n    c = 3\n    d = 4\n\n    return d\n"

    assert fix(text, config).text == expected
    assert fix(expected, config).text == expected


def test_a_gather_rule_and_a_kept_run_rule_remove_the_same_blank_line_once() -> None:
    text = "print(a)\n\nprint(b)\n\nprint(c)\n\nprint(d)\n"
    config = Config.create("none", enable=["ZA3004", "ZA3052", "ZA3055"])

    assert fix(text, config).text == "print(a)\nprint(b)\nprint(c)\nprint(d)\n"


def test_a_gather_rule_leaves_a_yield_run_apart_from_its_neighbors() -> None:
    source = "def g():\n    first()\n    second()\n    yield a\n    yield b\n    third()\n    fourth()\n"
    expected = "def g():\n    first()\n    second()\n\n    yield a\n    yield b\n\n    third()\n    fourth()\n"

    assert fix_with(source, "ZA3007", "ZA3052") == expected


_GATHER_SOURCES = {
    "statements": ("ZA3052", "a = 1\n\nb = 2\n\nc = 3\n\nd = 4\n", "a = 1\nb = 2\nc = 3\nd = 4\n"),
    "members": (
        "ZA3053",
        "class C:\n    a = 1\n\n    b = 2\n\n    c = 3\n\n    d = 4\n",
        "class C:\n    a = 1\n    b = 2\n    c = 3\n    d = 4\n",
    ),
    "two runs": (
        "ZA3052",
        "a = 1\n\nb = 2\n\nc = 3\n\nd = 4\n\nx = f()  # note\n\ne = 5\n\nf = 6\n\ng = 7\n\nh = 8\n",
        "a = 1\nb = 2\nc = 3\nd = 4\nx = f()  # note\n\ne = 5\nf = 6\ng = 7\nh = 8\n",
    ),
    "a silenced gap": (
        "ZA3052",
        "a = 1\n\nb = 2\n\nc = 3\n\nd = 4  # noqa: ZA3052\n\ne = 5\n\nf = 6\n",
        "a = 1\nb = 2\nc = 3\n\nd = 4  # noqa: ZA3052\n\ne = 5\n\nf = 6\n",
    ),
}


@pytest.mark.parametrize("name", [name for name in _GATHER_SOURCES if name != "two runs"])
def test_each_finding_of_a_loose_run_removes_the_blank_lines_of_the_whole_run(name: str) -> None:
    code, before, after = _GATHER_SOURCES[name]
    config = Config.create("none", enable=[code])
    findings = lint(before, config)

    assert len(findings) >= 2
    assert apply_each_finding(before, config) == [after] * len(findings)
    assert fix(before, config).text == after
    assert lint(after, config) == []


@pytest.mark.parametrize("mode", ["first", "last", "code", "batch"])
@pytest.mark.parametrize("name", _GATHER_SOURCES)
def test_a_loose_run_settles_to_one_text_in_any_application_order(name: str, mode: str) -> None:
    code, before, after = _GATHER_SOURCES[name]
    config = Config.create("none", enable=[code])
    current = fix_in_steps(before, config, mode)

    assert current == after
    assert lint(current, config) == []


def test_a_finding_removes_the_blank_lines_of_its_own_run_and_no_other() -> None:
    _, before, after = _GATHER_SOURCES["two runs"]
    head, _, tail = before.partition("x = f()  # note\n\n")
    gathered_head, _, gathered_tail = after.partition("x = f()  # note\n\n")
    config = Config.create("none", enable=["ZA3052"])

    first_run_only = gathered_head + "x = f()  # note\n\n" + tail
    second_run_only = head + "x = f()  # note\n\n" + gathered_tail

    assert apply_each_finding(before, config) == [first_run_only] * 4 + [second_run_only] * 3
