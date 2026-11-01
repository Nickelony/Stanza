"""The shared analysis: units, gaps, restrictions, and which rule owns a gap."""

from __future__ import annotations

import ast
import tokenize
from pathlib import Path

import pytest

from nickelony_stanza import Config
from nickelony_stanza.analysis.containers import collect_containers
from nickelony_stanza.analysis.model import Container
from nickelony_stanza.analysis.source import SourceFile, is_tool_directive
from nickelony_stanza.rules import ALL_RULES, CALL_RULES, KEEP_ORDER, OWNER_ORDER, PAIR_RULE, rules_of_level
from tests.helpers import config_for, findings_with, fix_with

ANALYSIS = Path(__file__).parent.parent / "src" / "nickelony_stanza" / "analysis"


def _containers(text: str) -> list[Container]:
    return collect_containers(SourceFile(text))


def _module(text: str) -> Container:
    return next(container for container in _containers(text) if container.kind == "module")


def test_a_decorated_definition_starts_at_its_first_decorator() -> None:
    module = _module("x = 1\n@a\n@b(\n    1\n)\ndef f():\n    pass\n")
    assert [(unit.first_line, unit.last_line) for unit in module.units] == [(1, 1), (2, 7)]


def test_a_gap_records_the_comments_that_lead_the_next_unit() -> None:
    gap = _module("a = 1\n\n# one\n# two\nb = 2\n").gaps[0]

    assert gap.is_blank
    assert gap.insert_line == 3
    assert [comment.text for comment in gap.lead_comments] == ["# one", "# two"]
    assert gap.has_comment


def test_a_detached_comment_does_not_lead() -> None:
    gap = _module("a = 1\n# note\n\nb = 2\n").gaps[0]

    assert gap.is_blank
    assert gap.insert_line == 4
    assert gap.lead_comments == ()


def test_a_trailing_comment_belongs_to_the_unit_above() -> None:
    gap = _module("a = 1  # note\nb = 2\n").gaps[0]

    assert not gap.is_blank
    assert gap.insert_line == 2
    assert gap.has_comment


def test_units_that_share_a_line_are_joined() -> None:
    assert all(gap.is_joined for gap in _module("a = 1; b = 2; c = 3\n").gaps)
    assert not _module("a = 1\nb = 2\n").gaps[0].is_joined


def test_a_comment_at_a_lower_indent_makes_the_gap_unreadable() -> None:
    function = next(c for c in _containers("def f():\n    a = 1\n# odd\n    b = 2\n") if c.kind == "function")
    assert function.gaps[0].is_joined


@pytest.mark.parametrize(
    ("text", "restriction"),
    [
        ("x = 1\ndef f():\n    pass\n", "definition"),
        ("def f():\n    pass\nx = 1\n", "definition"),
        ("if a:\n    def f():\n        pass\nx = 1\n", "definition"),
        ('"""Doc."""\nx = 1\n', "module-header"),
        ("#!/usr/bin/env python\nx = 1\n", "module-header"),
        ("x = 1\n'''about x'''\ny = 2\n", None),
        ("x = 1\ny = 2\n", None),
    ],
)
def test_a_gap_is_restricted_when_a_formatter_or_a_convention_owns_it(text: str, restriction: str | None) -> None:
    assert _module(text).gaps[0].restriction == restriction


def test_class_and_function_docstrings_restrict_the_gap_below_them() -> None:
    containers = _containers(
        'class A:\n    """Doc."""\n    x = 1\n    def f(self):\n        """Doc."""\n        return 1\n'
    )

    kinds = {container.kind: container.gaps[0].restriction for container in containers if container.kind != "module"}

    assert kinds["class"] == "class-docstring"
    assert kinds["function"] == "function-docstring"


def test_a_match_statement_has_a_container_of_its_cases() -> None:
    match = next(
        c for c in _containers("match v:\n    case 1:\n        a()\n    case 2:\n        b()\n") if c.kind == "match"
    )

    assert [(unit.first_line, unit.last_line) for unit in match.units] == [(2, 3), (4, 5)]


def _clauses(text: str) -> list[Container]:
    return [c for c in collect_containers(SourceFile(text), clauses=True) if c.kind == "clause"]


def test_a_clause_keyword_is_a_unit_below_the_last_statement_of_the_block_above() -> None:
    text = "if a:\n    x()\n    y()\n\n# note\nelif b:\n    z()\ntry:\n    w()\nexcept E:\n    v()\nfinally:\n    u()\n"
    clauses = _clauses(text)

    assert [[(unit.first_line, unit.last_line) for unit in c.units] for c in clauses] == [
        [(3, 3), (6, 6)],
        [(9, 9), (10, 10)],
        [(11, 11), (12, 12)],
    ]

    assert clauses[0].gaps[0].insert_line == 5
    assert clauses[0].gaps[0].is_blank


def test_the_keywords_of_every_statement_with_clauses_are_found() -> None:
    text = (
        "for x in y:\n    a()\nelse:\n    b()\n"
        "while z:\n    a()\nelse:\n    b()\n"
        "try:\n    a()\nexcept A:\n    b()\nexcept B:\n    c()\nelse:\n    d()\nfinally:\n    e()\n"
    )

    assert len(_clauses(text)) == 2 + 4


def test_only_a_statement_with_a_clause_keyword_has_a_clause_container() -> None:
    clauses = _clauses("if a:\n    x()\nwith b:\n    pass\nwhile c:\n    pass\ntry:\n    a()\nfinally:\n    b()\n")

    assert [[(unit.first_line, unit.last_line) for unit in c.units] for c in clauses] == [[(8, 8), (9, 9)]]

    assert len(_clauses("if a:\n    x()\nelse:\n    y()\n")) == 1


def test_a_clause_keyword_below_a_function_is_restricted() -> None:
    clause = _clauses("if a:\n    def f():\n        pass\nelse:\n    y()\n")[0]
    assert clause.gaps[0].restriction == "definition"


def test_clause_containers_are_collected_only_on_request() -> None:
    text = "if a:\n    x()\nelse:\n    y()\n"

    assert not [c for c in collect_containers(SourceFile(text)) if c.kind == "clause"]
    assert len(_clauses(text)) == 1


def test_the_codes_are_unique_and_in_the_python_range() -> None:
    codes = [rule.code for rule in ALL_RULES]

    assert len(set(codes)) == len(codes)
    assert all(3001 <= int(code[2:]) <= 3999 for code in codes)
    assert {rule.name for rule in ALL_RULES} == {rule.name for rule in (*OWNER_ORDER, *KEEP_ORDER, *CALL_RULES)}


def test_the_levels_hold_the_levels_below_them() -> None:
    minimal, moderate, strong = (set(rules_of_level(level)) for level in ("minimal", "moderate", "strong"))

    assert rules_of_level("none") == ()
    assert minimal < moderate < strong
    assert {rule.code for rule in rules_of_level("minimal")} == {"ZA3015", "ZA3017", "ZA3113", "ZA3202", "ZA3203"}
    assert PAIR_RULE in moderate

    assert {rule.code for rule in ALL_RULES if rule.level == "opt-in"} == {
        "ZA3054",
        "ZA3055",
        "ZA3112",
        "ZA3201",
    }

    assert {rule.code for rule in ALL_RULES if rule.level != "opt-in"} == {rule.code for rule in strong}


def test_the_default_level_is_moderate() -> None:
    assert set(Config.create().rules) == set(rules_of_level("moderate"))


def test_the_first_enabled_rule_in_the_owner_order_reports_a_gap() -> None:
    text = "x = 1\ny = 2\nif z:\n    pass\n"

    assert [f.code for f in findings_with(text, "ZA3001", "ZA3002")] == ["ZA3001"]
    assert [f.code for f in findings_with(text, "ZA3002")] == ["ZA3002"]
    assert fix_with(text, "ZA3001") == fix_with(text, "ZA3002") == "x = 1\ny = 2\n\nif z:\n    pass\n"


def test_a_rule_that_is_off_hands_its_gap_to_the_next_rule() -> None:
    text = "print(a)\n# note\nb = 2\nc = 3\n"

    assert [f.code for f in findings_with(text, "ZA3012", "ZA3002")] == ["ZA3012"]
    assert [f.code for f in findings_with(text, "ZA3002")] == ["ZA3002"]


def test_the_jump_rule_ends_its_stanza_at_a_gap_another_rule_asks_for() -> None:
    text = "def f():\n    a = 1\n    b = 2\n    print(a)\n    return b\n"

    assert fix_with(text, "ZA3009") == "def f():\n    a = 1\n    b = 2\n    print(a)\n\n    return b\n"
    assert fix_with(text, "ZA3009", "ZA3002") == "def f():\n    a = 1\n    b = 2\n\n    print(a)\n    return b\n"


def test_a_configuration_with_no_rule_finds_nothing() -> None:
    assert findings_with("a = 1\nb = 2\nrun()\n") == []
    assert config_for([]).rules == ()


def test_the_analysis_imports_no_rule() -> None:
    imported: set[str] = set()

    for path in sorted(ANALYSIS.glob("*.py")):
        for node in ast.walk(ast.parse(path.read_text(encoding="utf-8"))):
            if isinstance(node, ast.Import):
                imported.update(f"{path.name}: {alias.name}" for alias in node.names)
            elif isinstance(node, ast.ImportFrom):
                imported.update(f"{path.name}: {node.module}.{alias.name}" for alias in node.names)

    assert [name for name in imported if ": nickelony_stanza.rules" in name] == []


def test_a_file_keeps_its_comments_when_the_tokenizer_fails(monkeypatch: pytest.MonkeyPatch) -> None:
    def fail(*arguments: object) -> None:
        raise tokenize.TokenError("EOF in multi-line statement")

    monkeypatch.setattr(tokenize, "generate_tokens", fail)

    assert [comment.text for comment in SourceFile("a = 1\n# note\nb = 2\n").comments] == ["# note"]


def test_a_line_is_in_a_format_region_after_its_off_comment_through_its_on_comment() -> None:
    source = SourceFile("a = 1\n# fmt: off\nb = 2\n# fmt: on\nc = 3\n# fmt: off\nd = 4\n")

    assert source.fmt_regions == [(2, 4), (6, 8)]
    assert [number for number in range(1, 9) if source.in_fmt_region(number)] == [3, 4, 7, 8]


@pytest.mark.parametrize(
    ("comment", "expected"),
    [
        ("# type: ignore[attr-defined]", True),
        ("#noqa", True),
        ("# NOQA: E501", True),
        ("# pragma: no cover", True),
        ("# pylint: disable=all", True),
        ("# fmt: skip", True),
        ("# yapf: disable", True),
        ("# the type: ignore below", False),
        ("# noqafoo", False),
        ("# todo: fmt: off", False),
        ("# an ordinary comment", False),
    ],
)
def test_a_tool_directive_is_recognized_by_its_opening_words(comment: str, expected: bool) -> None:
    assert is_tool_directive(comment) is expected


def _headers(text: str) -> list[Container]:
    return [c for c in collect_containers(SourceFile(text), headers=True) if c.kind == "header"]


def test_a_header_is_a_unit_above_the_first_unit_of_each_body() -> None:
    text = "def f(\n    a,\n):\n\n    # note\n    x = 1\nif a:\n    y = 1\nelif b:\n    z = 1\nelse:\n    w = 1\n"
    headers = _headers(text)

    assert [[(unit.first_line, unit.last_line) for unit in c.units] for c in headers] == [
        [(3, 3), (6, 6)],
        [(7, 7), (8, 8)],
        [(9, 9), (10, 10)],
        [(11, 11), (12, 12)],
    ]

    assert headers[0].gaps[0].insert_line == 5
    assert headers[0].gaps[0].blank_lines == range(4, 5)
    assert headers[1].gaps[0].blank_lines == range(0)


def test_the_headers_of_every_statement_with_a_body_are_found() -> None:
    text = (
        "class A:\n    x = 1\nfor x in y:\n    a()\nelse:\n    b()\nwhile z:\n    a()\nwith b:\n    pass\n"
        "try:\n    a()\nexcept A:\n    b()\nelse:\n    d()\nfinally:\n    e()\n"
        "match v:\n    case 1:\n        a()\n    case 2:\n        b()\n"
    )

    assert len(_headers(text)) == 1 + 2 + 1 + 1 + 4 + 3


def test_a_body_that_shares_the_line_of_its_header_has_no_header_container() -> None:
    assert _headers("if a: x = 1\nclass A: pass\ndef f(): ...\nfor i in y: continue\n") == []


def test_headers_are_collected_only_on_request() -> None:
    text = "def f():\n\n    x = 1\n"

    assert not [c for c in collect_containers(SourceFile(text)) if c.kind == "header"]
    assert len(_headers(text)) == 1


def test_a_header_gap_with_a_comment_deletes_the_blank_lines_above_the_first_comment() -> None:
    gap = _headers("def f():\n\n\n    # one\n\n    # two\n\n    x = 1\n")[0].gaps[0]

    assert gap.blank_lines == range(2, 4)
    assert [comment.text for comment in gap.lead_comments] == []
    assert gap.has_comment


@pytest.mark.parametrize(
    ("text", "index", "restriction"),
    [
        ("if a:\n\n    def g():\n        pass\n", 0, "definition"),
        ("try:\n    pass\nfinally:\n\n    class A:\n        pass\n", 1, "definition"),
        ("if a:\n\n    # note\n    def g():\n        pass\n", 0, None),
        ("def f():\n\n    def g():\n        pass\n", 0, None),
        ("class K:\n\n    class A:\n        pass\n", 0, None),
        ("if a:\n\n    x = 1\n", 0, None),
    ],
)
def test_a_header_gap_is_restricted_where_a_formatter_writes_a_blank_line_above_a_definition(
    text: str, index: int, restriction: str | None
) -> None:
    assert _headers(text)[index].gaps[0].restriction == restriction


def test_a_header_gap_is_joined_by_a_directive_or_a_comment_at_a_lower_indent() -> None:
    assert _headers("def f():\n\n    # noqa\n    x = 1\n")[0].gaps[0].is_joined
    assert _headers("def f():\n\n# odd\n    x = 1\n")[0].gaps[0].is_joined
    assert not _headers("def f():\n\n    # note\n    x = 1\n")[0].gaps[0].is_joined
