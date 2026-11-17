"""The call rules: which arguments ZA3501 and ZA3502 name, where the names go, and the tree check that guards them."""

from __future__ import annotations

import subprocess
import sys
import textwrap
from dataclasses import replace
from pathlib import Path

import pytest

from nickelony_stanza import Config, FixError, Options, engine, fix, lint
from nickelony_stanza.analysis.projects import Projects
from nickelony_stanza.cli import run
from nickelony_stanza.engine import Finding, Insertion
from tests.helpers import SCRIPTS, findings_with, fix_with, run_module

DRAW = "def draw(width, height, smooth, path, *, scale=1.0): ...\n"
SAVE = "def save(path, overwrite, backup=None): ...\n"


def _name(call: str, *, options: Options | None = None, rules: tuple[str, ...] = ("ZA3501", "ZA3502")) -> str:
    """Fixes a call of `draw` or `save` and returns the line of the call."""
    return fix_with(DRAW + SAVE + call + "\n", *rules, options=options).splitlines()[-1]


@pytest.mark.parametrize(
    ("call", "expected"),
    [
        ('draw(640, 480, True, "a.png")', 'draw(width=640, height=480, smooth=True, path="a.png")'),
        ('draw(640, 480, True, path="a.png")', 'draw(width=640, height=480, smooth=True, path="a.png")'),
        (
            'draw(640, 480, True, "a.png", scale=2.0)',
            'draw(width=640, height=480, smooth=True, path="a.png", scale=2.0)',
        ),
        ('draw(width, height, True, "a.png")', 'draw(width, height, smooth=True, path="a.png")'),
        ('draw(width, 480, height, "a.png")', 'draw(width, height=480, smooth=height, path="a.png")'),
        ('draw(height, width, True, "a.png")', 'draw(width=height, height=width, smooth=True, path="a.png")'),
        ("draw(width, height, smooth, path)", "draw(width, height, smooth, path)"),
        (
            'draw(width=640, height=480, smooth=True, path="a.png")',
            'draw(width=640, height=480, smooth=True, path="a.png")',
        ),
    ],
)
def test_the_arguments_are_named_from_the_first_one_that_needs_a_name(call: str, expected: str) -> None:
    assert _name(call, rules=("ZA3501",)) == expected


def test_require_names_an_identifier_that_carries_its_parameters_name() -> None:
    options = Options(named_argument_match="require")

    assert _name("draw(width, height, smooth, path)", options=options) == (
        "draw(width=width, height=height, smooth=smooth, path=path)"
    )


@pytest.mark.parametrize(
    ("limit", "call", "expected"),
    [
        (4, 'draw(640, 480, "a.png")', 'draw(640, 480, "a.png")'),
        (3, 'draw(640, 480, "a.png")', 'draw(width=640, height=480, smooth="a.png")'),
        (4, "draw(640, 480, smooth=1, path=2)", "draw(width=640, height=480, smooth=1, path=2)"),
        (0, 'draw(640, 480, True, "a.png")', 'draw(640, 480, True, "a.png")'),
        (1, "draw(640)", "draw(width=640)"),
    ],
)
def test_a_call_is_judged_from_the_number_of_arguments_it_writes(limit: int, call: str, expected: str) -> None:
    options = Options(named_argument_min_arguments=limit)
    assert _name(call, options=options, rules=("ZA3501",)) == expected


@pytest.mark.parametrize(
    ("definition", "call", "expected"),
    [
        ("def f(a, b, /, c, d): ...\n", "f(1, 2, 3, 4)", "f(1, 2, c=3, d=4)"),
        ("def f(a, b, c, d, /): ...\n", "f(1, 2, 3, 4)", "f(1, 2, 3, 4)"),
        ("def f(__a, b, c, d): ...\n", "f(1, 2, 3, 4)", "f(1, b=2, c=3, d=4)"),
        ("def f(a, __b, c, d): ...\n", "f(1, 2, 3, 4)", "f(1, 2, 3, 4)"),
        ("def f(a, __b, c, d): ...\n", "f(a, 2, 3, 4)", "f(a, 2, c=3, d=4)"),
        ("def f(a, b, *rest): ...\n", "f(1, 2, 3, 4)", "f(1, 2, 3, 4)"),
        ("def f(a, b, c, d): ...\n", "f(*items, 2, 3, 4)", "f(*items, 2, 3, 4)"),
        ("def f(a, b, c, d): ...\n", "f(1, 2, 3, **rest)", "f(1, 2, 3, **rest)"),
        ("class C:\n    def m(self, a, b, c): ...\n", "C.m(C(), 1, 2, 3)", "C.m(C(), a=1, b=2, c=3)"),
        (
            "class C:\n    @classmethod\n    def m(cls, a, b, c, d): ...\n",
            "C.m(1, 2, 3, 4)",
            "C.m(a=1, b=2, c=3, d=4)",
        ),
    ],
)
def test_an_argument_that_cannot_take_a_name_stays_positional(definition: str, call: str, expected: str) -> None:
    assert fix_with(definition + call + "\n", "ZA3501").splitlines()[-1] == expected


@pytest.mark.parametrize(
    ("call", "literals", "expected"),
    [
        ("save(p, True, None)", "keywords", "save(p, overwrite=True, backup=None)"),
        ("save(p, True, None)", "bool", "save(p, overwrite=True, backup=None)"),
        ("save(p, 1, None)", "bool", "save(p, 1, None)"),
        ("save(p, 1, None)", "keywords", "save(p, 1, backup=None)"),
        ("save(None, x)", "keywords", "save(path=None, overwrite=x)"),
        ("save(p, x)", "keywords", "save(p, x)"),
        ("save(True)", "keywords", "save(True)"),
        ("save(p, False, backup=b)", "keywords", "save(p, overwrite=False, backup=b)"),
        ('save("a", 0, ...)', "keywords", 'save("a", 0, ...)'),
    ],
)
def test_za3502_names_the_literals_from_the_first_one_on(call: str, literals: str, expected: str) -> None:
    options = Options(named_argument_literals=literals)  # type: ignore[arg-type]
    assert _name(call, options=options, rules=("ZA3502",)) == expected


def test_za3501_owns_a_call_it_reports_and_za3502_takes_the_calls_below_the_limit() -> None:
    text = DRAW + SAVE + 'draw(640, 480, True, "a.png")\nsave(p, True)\n'
    findings = findings_with(text, "ZA3501", "ZA3502")

    assert [(finding.line, finding.code) for finding in findings] == [(3, "ZA3501"), (4, "ZA3502")]


def test_a_noqa_over_za3501_does_not_hand_the_call_to_za3502() -> None:
    text = DRAW + 'draw(640, 480, True, "a.png")  # noqa: ZA3501\n'
    assert fix_with(text, "ZA3501", "ZA3502") == text


def test_za3502_names_the_call_za3501_cannot_finish() -> None:
    text = "def f(a, __b, c, d): ...\nf(1, 2, True, 4)\n"
    assert fix_with(text, "ZA3501", "ZA3502").splitlines()[-1] == "f(1, 2, c=True, d=4)"


@pytest.mark.parametrize(
    ("patterns", "named"),
    [
        ((), True),
        (("draw",), False),
        (("*.draw",), False),
        (("dra?",), False),
        (("<stdin>.draw",), False),
        (("x",), True),
    ],
)
def test_the_ignore_patterns_match_the_written_callee_and_its_resolved_name(
    patterns: tuple[str, ...], named: bool
) -> None:
    options = Options(named_argument_ignore=patterns)
    assert (_name('draw(640, 480, True, "a.png")', options=options) != 'draw(640, 480, True, "a.png")') is named


@pytest.mark.parametrize(
    ("text", "silenced"),
    [
        ('draw(640, 480, True, "a.png")  # noqa: ZA3501\n', True),
        ('draw(640, 480, True, "a.png")  # noqa: ZA3502\n', False),
        ('draw(  # noqa: ZA3501\n    640,\n    480,\n    True,\n    "a.png",\n)\n', True),
        ('x = draw(  # noqa\n    640,\n    480,\n    True,\n    "a.png",\n)\n', True),
        ('x = draw(\n    640,\n    480,  # noqa: ZA3501\n    True,\n    "a.png",\n)\n', False),
        ('x = [  # noqa: ZA3501\n    draw(640, 480, True, "a.png"),\n]\n', True),
        ('# ruff: noqa: ZA3501\ndraw(640, 480, True, "a.png")\n', True),
    ],
)
def test_a_noqa_comment_on_the_call_or_its_statement_silences_the_call(text: str, silenced: bool) -> None:
    assert (fix_with(DRAW + text, "ZA3501") == DRAW + text) is silenced


@pytest.mark.parametrize(
    ("call", "expected"),
    [
        ("draw((640), (480), True, x)", "draw(width=(640), height=(480), smooth=True, path=x)"),
        ("draw(640, 480, True, x,)", "draw(width=640, height=480, smooth=True, path=x,)"),
        ("draw (640, 480, True, x)", "draw (width=640, height=480, smooth=True, path=x)"),
        ("(draw)(640, 480, True, x)", "(draw)(width=640, height=480, smooth=True, path=x)"),
        ("draw(lambda: 1, 480, True, x)", "draw(width=lambda: 1, height=480, smooth=True, path=x)"),
        ("draw(a if b else c, 480, True, x)", "draw(width=a if b else c, height=480, smooth=True, path=x)"),
        ("draw((y := 1), 480, True, x)", "draw(width=(y := 1), height=480, smooth=True, path=x)"),
        ("draw(y := 1, 480, True, x)", "draw(y := 1, 480, True, x)"),
        ("draw((i for i in a), 480, True, x)", "draw(width=(i for i in a), height=480, smooth=True, path=x)"),
        (
            "draw(draw(1, 2, 3, 4), 480, True, x)",
            "draw(width=draw(width=1, height=2, smooth=3, path=4), height=480, smooth=True, path=x)",
        ),
        ('draw(640, 480, True, f"{x}")', 'draw(width=640, height=480, smooth=True, path=f"{x}")'),
    ],
)
def test_a_name_goes_before_the_first_token_of_its_argument(call: str, expected: str) -> None:
    assert _name(call, rules=("ZA3501",)) == expected


def test_a_multi_line_call_takes_its_names_on_each_line_and_keeps_its_comments() -> None:
    before = "x = draw(\n    640,  # wide\n    # tall\n    480,\n    True, 'a.png',\n)\n"
    after = "x = draw(\n    width=640,  # wide\n    # tall\n    height=480,\n    smooth=True, path='a.png',\n)\n"

    assert fix_with(DRAW + before, "ZA3501") == DRAW + after


def test_a_call_after_text_wider_than_one_byte_takes_its_names_at_the_right_columns() -> None:
    word = chr(0xE9) + chr(0x20AC) + chr(0x1F600)
    before = f'note = "{word}"; draw(640, 480, True, "{word}")\n'
    after = f'note = "{word}"; draw(width=640, height=480, smooth=True, path="{word}")\n'

    assert fix_with(DRAW + before, "ZA3501") == DRAW + after
    assert findings_with(DRAW + before, "ZA3501")[0].column == len(f'note = "{word}"; draw(')


@pytest.mark.parametrize("ending", ["\n", "\r\n", "\r"])
def test_tabs_and_every_line_ending_are_kept(ending: str) -> None:
    before = f"def f():{ending}\tdraw(640,{ending}\t\t480, True, x){ending}"
    after = f"def f():{ending}\tdraw(width=640,{ending}\t\theight=480, smooth=True, path=x){ending}"

    assert fix_with(DRAW.replace("\n", ending) + before, "ZA3501") == DRAW.replace("\n", ending) + after


def test_the_calls_of_decorators_defaults_lambdas_and_nested_calls_are_named_in_one_pass() -> None:
    before = textwrap.dedent(
        """\
        def deco(a, b, c, d): return lambda function: function
        @deco(1, 2, 3, 4)
        def g(value=draw(1, 2, 3, 4)):
            return lambda: draw(draw(1, 2, 3, 4), 2, 3, 4)
        """
    )

    after = textwrap.dedent(
        """\
        def deco(a, b, c, d): return lambda function: function
        @deco(a=1, b=2, c=3, d=4)
        def g(value=draw(width=1, height=2, smooth=3, path=4)):
            return lambda: draw(width=draw(width=1, height=2, smooth=3, path=4), height=2, smooth=3, path=4)
        """
    )

    result = fix(DRAW + before, Config.create("none", enable=["ZA3501"]))

    assert (result.text, result.passes) == (DRAW + after, 1)


@pytest.mark.skipif(sys.version_info < (3, 12), reason="the tokenizer reads an f-string as one token before 3.12")
def test_a_call_inside_an_f_string_is_named_from_python_3_12() -> None:
    before = 'label = f"{x!r:>{draw(1, 2, 3, 4)}} and {draw(5, 6, 7, 8)}"\n'
    after = 'label = f"{x!r:>{draw(width=1, height=2, smooth=3, path=4)}} and {draw(width=5, height=6, smooth=7, path=8)}"\n'

    assert fix_with(DRAW + before, "ZA3501") == DRAW + after


@pytest.mark.skipif(sys.version_info >= (3, 12), reason="the tokenizer reads the inside of an f-string from 3.12")
def test_a_call_inside_an_f_string_is_left_alone_before_python_3_12() -> None:
    text = DRAW + 'label = f"{draw(1, 2, 3, 4)}"\n'
    assert fix_with(text, "ZA3501") == text


_NEEDS_FSTRING_TOKENS = pytest.mark.skipif(
    sys.version_info < (3, 12), reason="the tokenizer reads an f-string as one token before 3.12"
)

_PAIR = "def f(a, b):\n    return a, b\n\n\ndef g(a):\n    return a\n\n\n"


@_NEEDS_FSTRING_TOKENS
@pytest.mark.parametrize(
    "field",
    [
        "f(True, 2)=",
        " f(True, 2) = ",
        "(f(True, 2))=",
        "f(True, 2)=!s",
        "f(True, 2)=!s:>20",
        "g(f(True, 2))=",
        "not f(True, 2)=",
    ],
)
def test_a_call_inside_a_self_documenting_f_string_field_is_left_alone_and_blocks_no_other_fix(field: str) -> None:
    before = _PAIR + 'x = f"{' + field + '}"\ny = f(True, 2)\n'
    expected = before.replace("y = f(True, 2)", "y = f(a=True, b=2)")

    assert fix_with(before, "ZA3502") == expected
    assert run_module(expected)["x"] == run_module(before)["x"]


@_NEEDS_FSTRING_TOKENS
def test_a_call_in_an_ordinary_field_next_to_a_self_documenting_one_is_still_named() -> None:
    before = _PAIR + 'x = f"{f(True, 2)=} {f(True, 2)} {f(True, 4)!r:>{f(True, 6)}}"\n'
    expected = before.replace("{f(True, 2)} ", "{f(a=True, b=2)} ").replace("f(True, 4)", "f(a=True, b=4)")
    expected = expected.replace("f(True, 6)", "f(a=True, b=6)")

    assert fix_with(before, "ZA3502") == expected


def test_a_call_inside_a_fmt_off_region_is_left_alone() -> None:
    text = DRAW + "# fmt: off\ndraw(1, 2, 3, 4)\n# fmt: on\ndraw(1, 2, 3, 4)\n"

    assert fix_with(text, "ZA3501").splitlines()[-1] == "draw(width=1, height=2, smooth=3, path=4)"
    assert fix_with(text, "ZA3501").splitlines()[-3] == "draw(1, 2, 3, 4)"


@pytest.mark.parametrize(
    "statement",
    [
        "draw(1, 2, 3, 4)  # fmt: skip",
        "x = [draw(1, 2, 3, 4)]  # noqa: E501 # fmt: skip",
        "x = (\n    draw(1, 2, 3, 4),\n)  # fmt:skip",
    ],
)
def test_a_call_a_fmt_skip_comment_trails_is_left_alone(statement: str) -> None:
    text = DRAW + statement + "\ndraw(1, 2, 3, 4)\n"
    fixed = fix_with(text, "ZA3501")

    assert fixed.splitlines()[-1] == "draw(width=1, height=2, smooth=3, path=4)"
    assert fixed.splitlines()[1:-1] == statement.splitlines()


def test_a_fmt_skip_on_the_last_line_of_a_match_leaves_only_that_statement_alone() -> None:
    text = DRAW + "match x:\n    case 1:\n        draw(1, 2, 3, 4)\n    case 2:\n        pass  # fmt: skip\n"
    fixed = fix_with(text, "ZA3501")

    assert "draw(width=1, height=2, smooth=3, path=4)" in fixed


def test_a_tree_too_deep_to_check_refuses_the_fix() -> None:
    text = DRAW + "def frame():\n    x = " + " + ".join(["1"] * 1200) + "\n    draw(1, 2, 3, 4)\n"

    with pytest.raises(FixError, match="too deeply nested"):
        fix(text, Config.create("none", enable=["ZA3501"]))


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("class C:\n    def _m(self, a, b, c, d): ...\n    def run(self):\n        self._m(1, 2, 3, 4)\n", "all"),
        ("class C:\n    def m(self, a, b, c, d): ...\n    def run(self):\n        self.m(1, 2, 3, 4)\n", "public"),
    ],
)
@pytest.mark.parametrize("methods", ["all", "private", "none"])
def test_the_methods_setting_picks_the_calls_through_self_that_are_named(
    text: str, expected: str, methods: str
) -> None:
    options = Options(named_argument_methods=methods)  # type: ignore[arg-type]
    named = fix_with(text, "ZA3501", options=options) != text

    assert named is (methods == "all" or (methods == "private" and expected == "all"))


@pytest.mark.parametrize(("sources", "named"), [(("project", "stdlib", "installed"), True), (("project",), False)])
def test_the_sources_setting_picks_where_a_named_callee_may_be_defined(sources: tuple[str, ...], named: bool) -> None:
    text = "import os\nos.makedirs(path, 0o755, True)\n"
    options = Options(named_argument_sources=sources)

    assert (fix_with(text, "ZA3502", options=options) != text) is named


def test_a_call_of_a_builtin_a_value_or_a_rebound_name_is_left_alone() -> None:
    text = DRAW + "print(1, 2, 3, 4)\nitems.insert(1, 2, 3, 4)\nf = draw\nf = g\nf(1, 2, 3, 4)\n"
    assert fix_with(text, "ZA3501") == text


def _write_project(root: Path) -> Path:
    root.mkdir(exist_ok=True)
    (root / "pyproject.toml").write_text("[project]\nname = 'demo'\n", encoding="utf-8")
    (root / "shapes.py").write_text(DRAW, encoding="utf-8")

    main = root / "main.py"
    main.write_text("from shapes import draw\ndraw(1, 2, 3, 4)\n", encoding="utf-8")

    return main


def test_a_call_resolves_against_the_project_of_its_path(tmp_path: Path) -> None:
    main = _write_project(tmp_path)
    text = main.read_text(encoding="utf-8")
    config = Config.create("none", enable=["ZA3501"])
    projects = Projects()

    assert fix(text, config).text == text
    assert fix(text, config, path=main, projects=projects).text.endswith("draw(width=1, height=2, smooth=3, path=4)\n")


def test_the_source_roots_setting_replaces_the_detected_roots(tmp_path: Path) -> None:
    main = _write_project(tmp_path / "app")
    library = tmp_path / "lib"

    library.mkdir()
    (library / "shapes.py").write_text("def draw(a, b, c, d): ...\n", encoding="utf-8")

    text = main.read_text(encoding="utf-8")
    config = Config.create("none", enable=["ZA3501"])
    fixed = fix(text, config, path=main, projects=Projects([library])).text

    assert fixed.endswith("draw(a=1, b=2, c=3, d=4)\n")


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("from collections import namedtuple\nP = namedtuple('P', 'x y')\nP(1, 2)\n", "P(x=1, y=2)"),
        ("from typing import cast\nx = cast(int, None)\n", "x = cast(int, None)"),
        ("from typing import NewType\nUserId = NewType('UserId', int)\n", "UserId = NewType('UserId', int)"),
    ],
)
def test_a_special_form_a_type_checker_reads_positionally_keeps_its_arguments(text: str, expected: str) -> None:
    fixed = fix_with(text, "ZA3501", "ZA3502", options=Options(named_argument_min_arguments=2))
    assert fixed.splitlines()[1:] == [*text.splitlines()[1:-1], expected]


def test_a_dunder_method_other_than_a_constructor_keeps_its_arguments_positional() -> None:
    text = (
        "class Box:\n"
        "    def __init__(self, a, b): ...\n\n"
        "    def __eq__(self, other): ...\n\n"
        "    def __exit__(self, kind, value, trace): ...\n\n"
        "    def copy(self, other):\n"
        "        self.__init__(1, 2)\n"
        "        self.__eq__(other)\n"
        "        self.__exit__(None, None, None)\n"
    )

    fixed = fix_with(text, "ZA3501", "ZA3502", options=Options(named_argument_min_arguments=1))

    assert fixed.splitlines()[-3:] == [
        "        self.__init__(a=1, b=2)",
        "        self.__eq__(other)",
        "        self.__exit__(None, None, None)",
    ]


@pytest.mark.parametrize(
    "double",
    [
        "import shapes\nshapes.draw = lambda *args: None\n",
        "import shapes\nsetattr(shapes, 'draw', lambda *args: None)\n",
        "import shapes\ndef test(monkeypatch):\n    monkeypatch.setattr(shapes, 'draw', lambda *args: None)\n",
        "def test(monkeypatch):\n    monkeypatch.setattr('shapes.draw', lambda *args: None)\n",
        "from unittest import mock\n@mock.patch('shapes.draw')\ndef test(draw): ...\n",
        "from unittest import mock\nimport shapes\ndef test():\n    with mock.patch.object(shapes, 'draw'): ...\n",
    ],
)
def test_a_callee_a_project_module_replaces_with_a_double_is_left_alone(tmp_path: Path, double: str) -> None:
    main = _write_project(tmp_path)
    text = main.read_text(encoding="utf-8")
    config = Config.create("none", enable=["ZA3501"])

    assert fix(text, config, path=main, projects=Projects()).text != text

    (tmp_path / "test_shapes.py").write_text(double, encoding="utf-8")

    assert fix(text, config, path=main, projects=Projects()).text == text


def test_a_method_a_project_module_patches_on_its_class_is_left_alone(tmp_path: Path) -> None:
    (tmp_path / "pyproject.toml").write_text("", encoding="utf-8")

    (tmp_path / "canvas.py").write_text(
        "class Canvas:\n    def line(self, a, b, c, d): ...\n\n    def frame(self):\n        self.line(1, 2, 3, 4)\n",
        encoding="utf-8",
    )

    double = "from canvas import Canvas\ndef test(monkeypatch):\n    monkeypatch.setattr(Canvas, 'line', print)\n"
    (tmp_path / "test_canvas.py").write_text(double, encoding="utf-8")

    path = tmp_path / "canvas.py"
    text = path.read_text(encoding="utf-8")

    assert fix(text, Config.create("none", enable=["ZA3501"]), path=path, projects=Projects()).text == text


def test_a_text_that_changes_between_passes_is_read_from_the_engine_not_the_disk(tmp_path: Path) -> None:
    path = tmp_path / "main.py"
    text = DRAW + "a = 1\nb = 2\ndraw(1, 2, 3, 4)\n"

    path.write_text(text, encoding="utf-8")

    config = Config.create("none", enable=["ZA3501", "ZA3002"])
    projects = Projects()
    fixed = fix(text, config, path=path, projects=projects).text

    assert fixed == DRAW + "a = 1\nb = 2\n\ndraw(width=1, height=2, smooth=3, path=4)\n"
    assert lint(fixed, config, path=path, projects=projects) == []
    assert lint(text, config, path=path, projects=projects) != []


def test_one_pass_writes_a_blank_line_and_a_name_on_the_same_line() -> None:
    text = DRAW + "a = 1\nb = 2\ndraw(1, 2, 3, 4)\n"
    config = Config.create("none", enable=["ZA3501", "ZA3002"])
    result = fix(text, config)

    assert (result.text, result.passes) == (DRAW + "a = 1\nb = 2\n\ndraw(width=1, height=2, smooth=3, path=4)\n", 1)


def test_the_tree_check_refuses_a_name_the_plan_did_not_promise(monkeypatch: pytest.MonkeyPatch) -> None:
    def misplace(finding: Finding) -> Finding:
        edits = tuple(
            Insertion(edit.line, edit.column, "other=") for edit in finding.edits if isinstance(edit, Insertion)
        )

        return replace(finding, edits=edits)

    original = engine.find_findings

    monkeypatch.setattr(
        engine, "find_findings", lambda *args, **kwargs: [misplace(f) for f in original(*args, **kwargs)]
    )

    with pytest.raises(FixError, match="syntax tree"):
        fix(DRAW + "draw(1, 2, 3, 4)\n", Config.create("none", enable=["ZA3501"]))


def test_the_tree_check_refuses_a_name_that_breaks_the_text(monkeypatch: pytest.MonkeyPatch) -> None:
    def shift(finding: Finding) -> Finding:
        edits = tuple(
            Insertion(edit.line, edit.column - 1, edit.text) for edit in finding.edits if isinstance(edit, Insertion)
        )

        return replace(finding, edits=edits)

    original = engine.find_findings
    monkeypatch.setattr(engine, "find_findings", lambda *args, **kwargs: [shift(f) for f in original(*args, **kwargs)])

    with pytest.raises(FixError, match="does not parse"):
        fix(DRAW + "draw(1, 2, 3, 4)\n", Config.create("none", enable=["ZA3501"]))


def test_the_tree_check_refuses_a_text_the_parser_accepts_and_the_compiler_refuses(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    def repeat(finding: Finding) -> Finding:
        # The plan and the edits agree on one name for every argument, which no program can take.
        assert finding.naming is not None

        edits = tuple(
            Insertion(edit.line, edit.column, "width=") if isinstance(edit, Insertion) else edit
            for edit in finding.edits
        )

        return replace(
            finding, edits=edits, naming=replace(finding.naming, names=("width",) * len(finding.naming.names))
        )

    original = engine.find_findings

    monkeypatch.setattr(engine, "find_findings", lambda *args, **kwargs: [repeat(f) for f in original(*args, **kwargs)])

    with pytest.raises(FixError, match="does not compile"):
        fix(DRAW + "draw(1, 2, 3, 4)\n", Config.create("none", enable=["ZA3501"]))


def test_a_file_the_compiler_already_refuses_still_takes_its_fixes() -> None:
    text = DRAW + "draw(1, 2, 3, 4)\nreturn 1\n"
    assert fix_with(text, "ZA3501") == DRAW + "draw(width=1, height=2, smooth=3, path=4)\nreturn 1\n"


def test_the_tree_check_still_refuses_a_blank_line_edit_that_changes_the_tree(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(engine, "apply_edits", lambda source, findings: source.text.replace("b = 2", "b = 3"))

    with pytest.raises(FixError, match="syntax tree"):
        fix("a = 1\nb = 2\nrun()\n", Config.create("moderate"))


def test_check_reports_a_call_at_its_argument_list(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    monkeypatch.chdir(tmp_path)
    path = tmp_path / "main.py"
    path.write_text(DRAW + "x = 1; draw(\n    1, 2, 3, 4)\n", encoding="utf-8")

    assert run(["check", "--level", "none", "--enable", "ZA3501", str(path)]) == 1
    assert f"{path}:2:12: ZA3501 Name the arguments of this call. (name-call-arguments)" in capsys.readouterr().out


@pytest.mark.parametrize("named", [False, True])
def test_standard_input_resolves_against_the_project_of_its_filename(tmp_path: Path, named: bool) -> None:
    main = _write_project(tmp_path)
    filename = ["--stdin-filename", str(main)] if named else []

    result = subprocess.run(
        [str(SCRIPTS / "stanza-py"), "fix", "--level", "none", "--enable", "ZA3501", *filename, "-"],
        input=main.read_bytes(),
        capture_output=True,
        check=False,
        cwd=tmp_path,
    )

    expected = "draw(width=1, height=2, smooth=3, path=4)" if named else "draw(1, 2, 3, 4)"

    assert result.returncode == 0
    assert result.stdout.decode().splitlines()[-1] == expected


_ANNOTATED_CLASSES = [
    "class C(NamedTuple):\n    a: int\n    a: str\n    b: int\n",
    "class C(NamedTuple):\n    a: int\n    b: int\n    a: str\n",
    "class C(NamedTuple):\n    a: int = 1\n    b: int = 2\n    a: str = 'x'\n",
    "@dataclass\nclass C:\n    a: int\n    a: str\n    b: int\n",
    "@dataclass\nclass C:\n    a: int\n    b: int\n    __annotations__ = {'c': int, 'd': int}\n",
    "@dataclass\nclass C:\n    a: int\n    b: int\n    __annotations__.update(c=int, d=int)\n",
    "@dataclass\nclass C:\n    a: int\n    b: int\n    __annotations__ |= {'c': int}\n",
    "@dataclass\nclass C:\n    a: int\n    b: int\n    del __annotations__\n",
    "class C(NamedTuple):\n    a: int\n    b: int\n    __annotations__ = {'c': int, 'd': int}\n",
]


@pytest.mark.parametrize("declaration", _ANNOTATED_CLASSES, ids=range(len(_ANNOTATED_CLASSES)))
def test_a_class_whose_annotation_mapping_differs_from_its_statements_binds_as_it_runs(declaration: str) -> None:
    before = f"from dataclasses import dataclass\nfrom typing import NamedTuple\n\n{declaration}\nx = C(True, 2)\n"

    fixed = fix_with(before, "ZA3502")

    # The fix either names what the class takes, or leaves the call; it never writes a name the class lacks or twice.
    try:
        expected = run_module(before)["x"]
    except TypeError:
        assert fixed == before
    else:
        assert repr(run_module(fixed)["x"]) == repr(expected)


_REBINDING_HEADERS = {
    "a default": "def install(value=(target := lambda w, x, y, z: w + x + y + z)):\n    pass\n",
    "a keyword-only default": "def install(*, value=(target := lambda w, x, y, z: w + x + y + z)):\n    pass\n",
    "a decorator": "@(target := lambda w, x=0, y=0, z=0: w)\ndef install():\n    pass\n",
    "an annotation": "def install(value: (target := lambda w, x, y, z: w + x + y + z) = None):\n    pass\n",
    "a return annotation": "def install() -> (target := lambda w, x, y, z: w + x + y + z):\n    pass\n",
    "a lambda default": "install = lambda value=(target := lambda w, x, y, z: w + x + y + z): value\n",
    "a class base": "class Install((target := lambda w, x, y, z: w + x + y + z) and object):\n    pass\n",
    "a class keyword": (
        "class Base:\n    def __init_subclass__(cls, **options):\n        pass\n\n\n"
        "class Install(Base, value=(target := lambda w, x, y, z: w + x + y + z)):\n    pass\n"
    ),
    "a class decorator": "@(target := lambda w, x=0, y=0, z=0: w)\nclass Install:\n    pass\n",
}

_TARGET = "def target(a, b, c, d):\n    return a + b + c + d\n\n\n"


@pytest.mark.parametrize("header", _REBINDING_HEADERS.values(), ids=_REBINDING_HEADERS)
def test_a_name_a_definition_header_rebinds_is_not_resolved_to_the_earlier_definition(header: str) -> None:
    before = f"{_TARGET}{header}\nresult = target(1, 2, 3, 4)\n"
    expected = run_module(before)["result"]

    fixed = fix_with(before, "ZA3501")

    assert run_module(fixed)["result"] == expected
    assert fixed == before


_UNRELATED_HEADERS = [
    "def install(value=lambda w, x, y, z: 0):\n    pass\n",
    "class Install(object):\n    pass\n",
    "class Install:\n    def run(self, value=(target := lambda w, x, y, z: 0)):\n        pass\n",
    "install = lambda: lambda value=(target := lambda w, x, y, z: 0): value\n",
    "def install():\n    value = (target := lambda w, x, y, z: 0)\n",
]


@pytest.mark.parametrize("header", _UNRELATED_HEADERS, ids=range(len(_UNRELATED_HEADERS)))
def test_a_header_whose_binding_stays_in_another_scope_leaves_the_earlier_definition_resolved(header: str) -> None:
    before = f"{_TARGET}{header}\nresult = target(1, 2, 3, 4)\n"
    fixed = fix_with(before, "ZA3501")

    assert fixed.endswith("result = target(a=1, b=2, c=3, d=4)\n")
    assert run_module(fixed)["result"] == run_module(before)["result"]
