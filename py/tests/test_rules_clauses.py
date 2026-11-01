"""One case for each behavior of the rules for case clauses and for clause keywords."""

from __future__ import annotations

import pytest

from nickelony_stanza import Config, fix
from tests.helpers import Case, case, check_case, fix_with

CASES = [
    # ZA3111 separate-multi-line-cases
    case(
        "ZA3111: multi-line case clauses",
        "ZA3111",
        "match v:\n    case 1:\n        a()\n    case 2:\n        b()\n",
        "match v:\n    case 1:\n        a()\n\n    case 2:\n        b()\n",
    ),
    case(
        "ZA3111: every gap of a match",
        "ZA3111",
        "match v:\n    case 1:\n        a()\n    case 2:\n        b()\n    case _:\n        c()\n",
        "match v:\n    case 1:\n        a()\n\n    case 2:\n        b()\n\n    case _:\n        c()\n",
    ),
    case(
        "ZA3111: one multi-line clause next to a single-line clause",
        "ZA3111",
        "match v:\n    case 1: a()\n    case 2:\n        b()\n",
        "match v:\n    case 1: a()\n\n    case 2:\n        b()\n",
    ),
    case("ZA3111: single-line case clauses stay as written", "ZA3111", "match v:\n    case 1: a()\n    case 2: b()\n"),
    case("ZA3111: a match with one clause has no gap", "ZA3111", "match v:\n    case 1:\n        a()\n"),
    case(
        "ZA3111: a comment between the cases leads the next clause",
        "ZA3111",
        "match v:\n    case 1:\n        a()\n    # two\n    case 2:\n        b()\n",
        "match v:\n    case 1:\n        a()\n\n    # two\n    case 2:\n        b()\n",
    ),
    case(
        "ZA3111: a nested match",
        "ZA3111",
        "def f(v):\n    match v:\n        case 1:\n            a()\n        case 2:\n            b()\n",
        "def f(v):\n    match v:\n        case 1:\n            a()\n\n        case 2:\n            b()\n",
    ),
    case(
        "ZA3111: the statements around a match belong to the construct rule",
        "ZA3111",
        "x = 1\nmatch v:\n    case 1:\n        a()\ny = 2\n",
    ),
    # ZA3112 keep-multi-line-cases-together
    case(
        "ZA3112: multi-line case clauses",
        "ZA3112",
        "match v:\n    case 1:\n        a()\n\n    case 2:\n        b()\n",
        "match v:\n    case 1:\n        a()\n    case 2:\n        b()\n",
    ),
    case(
        "ZA3112: several blank lines",
        "ZA3112",
        "match v:\n    case 1:\n        a()\n\n\n    case 2:\n        b()\n",
        "match v:\n    case 1:\n        a()\n    case 2:\n        b()\n",
    ),
    case(
        "ZA3112: every gap of a match",
        "ZA3112",
        "match v:\n    case 1:\n        a()\n\n    case 2:\n        b()\n\n    case _:\n        c()\n",
        "match v:\n    case 1:\n        a()\n    case 2:\n        b()\n    case _:\n        c()\n",
    ),
    case(
        "ZA3112: a case that ends in a function keeps the blank line the formatter writes",
        "ZA3112",
        "match v:\n    case 1:\n\n        def f():\n            pass\n\n    case 2:\n        b()\n",
    ),
    case(
        "ZA3112: a case that ends in a class keeps the blank line the formatter writes",
        "ZA3112",
        "match v:\n    case 1:\n\n        class A:\n            pass\n\n    case 2:\n        b()\n",
    ),
    case(
        "ZA3112: a comment between the cases keeps the blank line",
        "ZA3112",
        "match v:\n    case 1:\n        a()\n\n    # two\n    case 2:\n        b()\n",
    ),
    case(
        "ZA3112: single-line case clauses belong to the other rule",
        "ZA3112",
        "match v:\n    case 1: a()\n\n    case 2: b()\n",
    ),
    case("ZA3112: an if statement is no match", "ZA3112", "if v:\n    a()\n\nelse:\n    b()\n"),
    case(
        "ZA3112: a nested match",
        "ZA3112",
        "def f(v):\n    match v:\n        case 1:\n            a()\n\n        case 2:\n            b()\n",
        "def f(v):\n    match v:\n        case 1:\n            a()\n        case 2:\n            b()\n",
    ),
    # ZA3113 keep-single-line-cases-together
    case(
        "ZA3113: single-line case clauses",
        "ZA3113",
        "match v:\n    case 1: a()\n\n    case 2: b()\n",
        "match v:\n    case 1: a()\n    case 2: b()\n",
    ),
    case(
        "ZA3113: a clause of several lines belongs to the other rule",
        "ZA3113",
        "match v:\n    case 1:\n        a()\n\n    case 2: b()\n",
    ),
    case(
        "ZA3113: a run of single-line clauses stops at a clause of several lines",
        "ZA3113",
        "match v:\n    case 1: a()\n\n    case 2: b()\n\n    case 3:\n        c()\n",
        "match v:\n    case 1: a()\n    case 2: b()\n\n    case 3:\n        c()\n",
    ),
    # ZA3201 separate-clause-keywords
    case(
        "ZA3201: an if chain",
        "ZA3201",
        "if a:\n    x = 1\nelif b:\n    x = 2\nelse:\n    x = 3\n",
        "if a:\n    x = 1\n\nelif b:\n    x = 2\n\nelse:\n    x = 3\n",
    ),
    case(
        "ZA3201: a try statement",
        "ZA3201",
        "try:\n    a()\nexcept E:\n    b()\nexcept F:\n    c()\nelse:\n    d()\nfinally:\n    e()\n",
        "try:\n    a()\n\nexcept E:\n    b()\n\nexcept F:\n    c()\n\nelse:\n    d()\n\nfinally:\n    e()\n",
    ),
    case(
        "ZA3201: a try statement with only a finally",
        "ZA3201",
        "try:\n    a()\nfinally:\n    e()\n",
        "try:\n    a()\n\nfinally:\n    e()\n",
    ),
    case(
        "ZA3201: an except star clause",
        "ZA3201",
        "try:\n    a()\nexcept* E:\n    b()\n",
        "try:\n    a()\n\nexcept* E:\n    b()\n",
    ),
    case(
        "ZA3201: the else of a loop",
        "ZA3201",
        "for x in y:\n    a()\nelse:\n    b()\nwhile z:\n    c()\nelse:\n    d()\n",
        "for x in y:\n    a()\n\nelse:\n    b()\nwhile z:\n    c()\n\nelse:\n    d()\n",
    ),
    case(
        "ZA3201: a nested else",
        "ZA3201",
        "def f():\n    if a:\n        if b:\n            x()\n        else:\n            y()\n",
        "def f():\n    if a:\n        if b:\n            x()\n\n        else:\n            y()\n",
    ),
    case(
        "ZA3201: an else block that holds an if is no elif",
        "ZA3201",
        "if a:\n    x()\nelse:\n    if b:\n        y()\n",
        "if a:\n    x()\n\nelse:\n    if b:\n        y()\n",
    ),
    case(
        "ZA3201: a multi-line condition",
        "ZA3201",
        "if a:\n    x()\nelif (\n    b\n    and c\n):\n    y()\n",
        "if a:\n    x()\n\nelif (\n    b\n    and c\n):\n    y()\n",
    ),
    case(
        "ZA3201: a comment at the keyword moves with it",
        "ZA3201",
        "if a:\n    x()\n# otherwise\nelse:\n    y()\n",
        "if a:\n    x()\n\n# otherwise\nelse:\n    y()\n",
    ),
    case(
        "ZA3201: a comment that trails the block stays above the blank line",
        "ZA3201",
        "if a:\n    x()\n    # trails\nelse:\n    y()\n",
        "if a:\n    x()\n    # trails\n\nelse:\n    y()\n",
    ),
    case(
        "ZA3201: a block that ends in a function belongs to the formatter",
        "ZA3201",
        "if a:\n    def f():\n        pass\nelse:\n    y()\n",
    ),
    case("ZA3201: a gap that is already blank", "ZA3201", "if a:\n    x()\n\nelse:\n    y()\n"),
    case("ZA3201: a statement with no clause", "ZA3201", "if a:\n    x()\ny()\n"),
    case(
        "ZA3201: a single-line block",
        "ZA3201",
        "if a: x()\nelse: y()\n",
        "if a: x()\n\nelse: y()\n",
    ),
    case(
        "ZA3201: a trailing comment on the keyword",
        "ZA3201",
        "if a:\n    x()\nelse:  # no\n    y()\n",
        "if a:\n    x()\n\nelse:  # no\n    y()\n",
    ),
    case(
        "ZA3201: a keyword inside a fmt: off region",
        "ZA3201",
        "# fmt: off\nif a:\n    x()\nelse:\n    y()\n# fmt: on\n",
    ),
    case(
        "ZA3201: a blank line above the comment is the gap",
        "ZA3201",
        "if a:\n    x()\n\n# otherwise\nelse:\n    y()\n",
    ),
    case(
        "ZA3201: a blank line between the comment and the keyword is a gap too",
        "ZA3201",
        "if a:\n    x()\n# otherwise\n\nelse:\n    y()\n",
    ),
    case(
        "ZA3201: a run of comments moves with the keyword",
        "ZA3201",
        "if a:\n    x()\n# one\n# two\nelse:\n    y()\n",
        "if a:\n    x()\n\n# one\n# two\nelse:\n    y()\n",
    ),
    case(
        "ZA3201: an ordinary comment that names a tool moves with the keyword",
        "ZA3201",
        "if a:\n    x()\n# the type: ignore below\nelse:\n    y()\n",
        "if a:\n    x()\n\n# the type: ignore below\nelse:\n    y()\n",
    ),
    case(
        "ZA3201: a fmt: off comment in the gap keeps the gap",
        "ZA3201",
        "if a:\n    x()\n# fmt: off\nelse:\n    y()\n# fmt: on\n",
    ),
    case(
        "ZA3201: a fmt: on comment in the gap keeps the gap",
        "ZA3201",
        "# fmt: off\nif a:\n    x()\n# fmt: on\nelse:\n    y()\n",
    ),
    case(
        "ZA3201: a comment at a lower indent is left alone",
        "ZA3201",
        "def f():\n    if a:\n        x()\n# odd\n    else:\n        y()\n",
    ),
    # ZA3202 keep-clause-keywords-adjacent
    case(
        "ZA3202: a blank line before elif and else",
        "ZA3202",
        "if a:\n    x = 1\n\nelif b:\n    x = 2\n\n\nelse:\n    x = 3\n",
        "if a:\n    x = 1\nelif b:\n    x = 2\nelse:\n    x = 3\n",
    ),
    case(
        "ZA3202: a blank line before except, else and finally",
        "ZA3202",
        "try:\n    a()\n\nexcept E:\n    b()\n\nelse:\n    c()\n\nfinally:\n    d()\n",
        "try:\n    a()\nexcept E:\n    b()\nelse:\n    c()\nfinally:\n    d()\n",
    ),
    case(
        "ZA3202: the else of a loop",
        "ZA3202",
        "for x in y:\n    a()\n\nelse:\n    b()\n",
        "for x in y:\n    a()\nelse:\n    b()\n",
    ),
    case(
        "ZA3202: the blank line above a comment in the gap goes",
        "ZA3202",
        "try:\n    load()\n\n# fall back to the cache\nexcept OSError:\n    use_cache()\n",
        "try:\n    load()\n# fall back to the cache\nexcept OSError:\n    use_cache()\n",
    ),
    case(
        "ZA3202: several blank lines above a comment go",
        "ZA3202",
        "if a:\n    x()\n\n\n# otherwise\nelse:\n    y()\n",
        "if a:\n    x()\n# otherwise\nelse:\n    y()\n",
    ),
    case(
        "ZA3202: a comment already tight under the block stays",
        "ZA3202",
        "if a:\n    x()\n# otherwise\nelse:\n    y()\n",
    ),
    case(
        "ZA3202: the blank line between the comment and the keyword stays",
        "ZA3202",
        "if a:\n    x()\n# otherwise\n\nelse:\n    y()\n",
    ),
    case(
        "ZA3202: only the blank lines above the first comment go",
        "ZA3202",
        "if a:\n    x()\n\n# one\n\n# two\n\nelse:\n    y()\n",
        "if a:\n    x()\n# one\n\n# two\n\nelse:\n    y()\n",
    ),
    case(
        "ZA3202: an ordinary comment that names a tool is a comment",
        "ZA3202",
        "if a:\n    x()\n\n# the type: ignore below\nelse:\n    y()\n",
        "if a:\n    x()\n# the type: ignore below\nelse:\n    y()\n",
    ),
    case(
        "ZA3202: a fmt: off comment in the gap keeps the gap",
        "ZA3202",
        "if a:\n    x()\n\n# fmt: off\nelse:\n    y()\n# fmt: on\n",
    ),
    case(
        "ZA3202: a fmt: on comment in the gap keeps the gap",
        "ZA3202",
        "# fmt: off\nif a:\n    x()\n# fmt: on\n\n\nelse:\n    y()\n",
    ),
    case(
        "ZA3202: a trailing comment of the block is part of the block",
        "ZA3202",
        "if a:\n    x()\n    # trails\n\nelse:\n    y()\n",
        "if a:\n    x()\n    # trails\nelse:\n    y()\n",
    ),
    case(
        "ZA3202: the blank line above a trailing comment is inside the block",
        "ZA3202",
        "if a:\n    x()\n\n    # trails\nelse:\n    y()\n",
    ),
    case(
        "ZA3202: a comment at the keyword below a trailing comment",
        "ZA3202",
        "if a:\n    x()\n    # trails\n\n# otherwise\nelse:\n    y()\n",
        "if a:\n    x()\n    # trails\n# otherwise\nelse:\n    y()\n",
    ),
    case(
        "ZA3202: a block that ends in a function keeps the blank line",
        "ZA3202",
        "if a:\n\n    def f():\n        pass\n\nelse:\n    y()\n",
    ),
    case(
        "ZA3202: a block that ends in a class keeps the blank line",
        "ZA3202",
        "try:\n\n    class A:\n        pass\n\nexcept E:\n    y()\n",
    ),
    case("ZA3202: clauses that already touch", "ZA3202", "if a:\n    x()\nelse:\n    y()\n"),
    case(
        "ZA3202: a multi-line block",
        "ZA3202",
        "if a:\n    x = f(\n        1\n    )\n\nelse:\n    y()\n",
        "if a:\n    x = f(\n        1\n    )\nelse:\n    y()\n",
    ),
    case(
        "ZA3202: a line of spaces is a blank line",
        "ZA3202",
        "if a:\n    x()\n   \nelse:\n    y()\n",
        "if a:\n    x()\nelse:\n    y()\n",
    ),
    case(
        "ZA3202: a keyword inside a fmt: off region",
        "ZA3202",
        "# fmt: off\nif a:\n    x()\n\nelse:\n    y()\n# fmt: on\n",
    ),
    case("ZA3202: a single-line block", "ZA3202", "if a: x()\n\nelse: y()\n", "if a: x()\nelse: y()\n"),
]


@pytest.mark.parametrize("item", CASES, ids=lambda item: item.id)
def test_rule_case(item: Case) -> None:
    check_case(item)


def test_naming_the_tight_case_rule_replaces_the_separating_one_at_a_level() -> None:
    tight = Config.create("moderate", enable=["ZA3112"])
    both = Config.create("moderate", enable=["ZA3111", "ZA3112"])

    assert "ZA3111" not in {rule.code for rule in tight.rules}
    assert "ZA3112" in {rule.code for rule in tight.rules}
    assert {"ZA3111", "ZA3112"} <= {rule.code for rule in both.rules}


def test_disabling_the_separating_case_rule_leaves_the_other_case_rules_working() -> None:
    config = Config.create("minimal", enable=["ZA3112"], disable=["ZA3111"])
    text = "match v:\n    case 1:\n        a()\n\n    case 2: b()\n\n    case 3: c()\n"

    assert fix(text, config).text == "match v:\n    case 1:\n        a()\n    case 2: b()\n    case 3: c()\n"

    assert fix(
        "match v:\n    case 1:\n        a()\n    case 2:\n        b()\n", Config.create(disable=["ZA3111"])
    ).text == ("match v:\n    case 1:\n        a()\n    case 2:\n        b()\n")


def test_a_noqa_comment_on_the_keyword_silences_the_blank_line() -> None:
    text = "if a:\n    x()\n\nelse:  # noqa: ZA3202\n    y()\n"
    assert fix_with(text, "ZA3202") == text


def test_the_tight_rule_wins_a_gap_the_separating_rule_asks_for() -> None:
    text = "if a:\n    x()\n\nelse:\n    y()\n"
    tight = "if a:\n    x()\nelse:\n    y()\n"

    assert fix_with(text, "ZA3201", "ZA3202") == tight
    assert fix_with(tight, "ZA3201", "ZA3202") == tight


def test_the_tight_case_rule_wins_a_gap_the_separating_rule_asks_for() -> None:
    text = "match v:\n    case 1:\n        a()\n    case 2:\n        b()\n\n    case 3:\n        c()\n"
    tight = "match v:\n    case 1:\n        a()\n    case 2:\n        b()\n    case 3:\n        c()\n"

    assert fix_with(text, "ZA3111", "ZA3112") == tight
    assert fix_with(tight, "ZA3111", "ZA3112") == tight


def test_the_separating_rule_still_judges_the_gaps_the_tight_rule_leaves() -> None:
    text = "match v:\n    case 1: a()\n    case 2:\n        b()\n    case 3: c()\n"

    assert (
        fix_with(text, "ZA3111", "ZA3113")
        == "match v:\n    case 1: a()\n\n    case 2:\n        b()\n\n    case 3: c()\n"
    )


def test_a_clause_gap_asks_nothing_of_the_other_rules() -> None:
    text = "import os\nif a:\n    import sys\nelse:\n    import json\n"
    config = Config.create("strong")

    assert fix(text, config).text == "import os\n\nif a:\n    import sys\nelse:\n    import json\n"


def test_naming_a_clause_rule_replaces_its_opposite_at_a_level() -> None:
    separate = Config.create("moderate", enable=["ZA3201"])
    tight = Config.create("moderate", enable=["ZA3202"])
    both = Config.create("moderate", enable=["ZA3201", "ZA3202"])

    assert "ZA3202" not in {rule.code for rule in separate.rules}
    assert "ZA3201" in {rule.code for rule in separate.rules}
    assert "ZA3202" in {rule.code for rule in tight.rules}
    assert "ZA3201" not in {rule.code for rule in tight.rules}
    assert {"ZA3201", "ZA3202"} <= {rule.code for rule in both.rules}

    text = "if a:\n    x()\nelse:\n    y()\n"

    assert fix(text, separate).text == "if a:\n    x()\n\nelse:\n    y()\n"
    assert fix(text, both).text == text


def test_a_disabled_opposite_stays_off() -> None:
    config = Config.create("moderate", enable=["ZA3201"], disable=["ZA3201"])
    assert {"ZA3201", "ZA3202"} & {rule.code for rule in config.rules} == set()


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
@pytest.mark.parametrize("rule", ["ZA3201", "ZA3202"])
def test_a_tool_directive_in_the_gap_keeps_the_gap_as_written(rule: str, directive: str) -> None:
    for text in (
        f"if a:\n    x()\n{directive}\nelse:\n    y()\n",
        f"if a:\n    x()\n\n{directive}\nelse:\n    y()\n",
        f"if a:\n    x()\n\n\n{directive}\n\nelse:\n    y()\n",
    ):
        assert fix_with(text, rule) == text
