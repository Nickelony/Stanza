"""Fixing settles in one pass to one text whatever the rule order, and the syntax tree stays."""

from __future__ import annotations

import itertools
import random
from pathlib import Path

import pytest

from nickelony_stanza import Config, FixError, engine, fix, lint
from nickelony_stanza.analysis.source import SourceFile
from nickelony_stanza.engine import Finding, apply_edits
from nickelony_stanza.rules import ALL_RULES
from tests.helpers import (
    SETTLING_CONFIGS,
    add_blank_lines,
    corpus_files,
    drop_blank_lines,
    run_formatter,
    same_tree_ignoring_keyword_names,
)


def _inputs(path: Path) -> list[str]:
    text = path.read_text(encoding="utf-8")
    formatted = run_formatter("black", text) or text

    return [
        formatted,
        text,
        drop_blank_lines(formatted, 7),
        drop_blank_lines(formatted, 11),
        add_blank_lines(formatted, 3),
        drop_blank_lines(add_blank_lines(formatted, 5), 13),
    ]


@pytest.mark.parametrize("name", SETTLING_CONFIGS)
@pytest.mark.parametrize("path", corpus_files(), ids=lambda path: path.name)
def test_every_input_settles_in_one_pass_to_a_clean_text_with_the_same_tree(name: str, path: Path) -> None:
    config = SETTLING_CONFIGS[name]

    for text in _inputs(path):
        first = fix(text, config)

        assert first.passes <= 1, f"the fix took {first.passes} passes"
        assert fix(first.text, config).passes == 0, "a second fix changes the text"
        assert lint(first.text, config) == [], "the output does not lint clean"
        assert same_tree_ignoring_keyword_names(text, first.text, config), "the syntax tree changed"


@pytest.mark.parametrize("path", corpus_files(), ids=lambda path: path.name)
def test_the_order_a_configuration_lists_its_rules_in_does_not_change_the_text(path: Path) -> None:
    config = SETTLING_CONFIGS["everything"]
    reversed_config = Config(tuple(reversed(config.rules)), config.options)

    for text in _inputs(path)[:2]:
        assert fix(text, reversed_config).text == fix(text, config).text


def _assert_every_partial_fix_settles_like_a_batch_fix(text: str, config: Config) -> None:
    batch = fix(text, config).text
    findings = lint(text, config)
    source = SourceFile(text)

    first_of_code: dict[str, Finding] = {}

    for finding in findings:
        first_of_code.setdefault(finding.code, finding)

    # The first finding, the last one, the first of each code and all of each code, each applied alone.
    applications = {
        "the first finding": findings[:1],
        "the last finding": findings[-1:],
        **{f"the first {code} finding": [finding] for code, finding in first_of_code.items()},
        **{f"every {code} finding": [f for f in findings if f.code == code] for code in first_of_code},
    }

    for label, chosen in applications.items():
        settled = fix(apply_edits(source, chosen), config)
        assert settled.text == batch, f"applying {label} settles to another text than a batch fix"


@pytest.mark.parametrize("name", SETTLING_CONFIGS)
@pytest.mark.parametrize("path", corpus_files(), ids=lambda path: path.name)
def test_a_fix_applied_in_part_settles_to_the_text_a_batch_fix_writes(name: str, path: Path) -> None:
    for index in (0, 4):
        _assert_every_partial_fix_settles_like_a_batch_fix(_inputs(path)[index], SETTLING_CONFIGS[name])


# A loose run of statements and one of members, which a gather rule removes whole whichever finding is applied.
LOOSE_RUNS = (
    "import os\n\n\nclass C:\n    a = 1\n\n    b = 2\n\n    c = 3\n\n    d = 4\n\n\n"
    "def f():\n    p = 1\n\n    q = 2\n\n    r = 3\n\n    s = 4\n\n    t = 5\n"
)


@pytest.mark.parametrize("name", SETTLING_CONFIGS)
def test_a_fix_applied_in_part_settles_a_loose_run_like_a_batch_fix(name: str) -> None:
    _assert_every_partial_fix_settles_like_a_batch_fix(LOOSE_RUNS, SETTLING_CONFIGS[name])


def test_the_none_level_changes_nothing() -> None:
    config = Config.create("none")

    for path in corpus_files():
        text = path.read_text(encoding="utf-8")
        assert fix(text, config).text == text


def test_the_strong_level_writes_blank_lines_into_every_corpus_file() -> None:
    config = Config.create("strong")

    for path in corpus_files():
        text = drop_blank_lines(run_formatter("black", path.read_text(encoding="utf-8")) or "", 7)
        assert fix(text, config).text != text, f"{path.name} has nothing to fix"


@pytest.mark.parametrize("seed", range(12))
def test_any_subset_of_the_rules_settles(seed: int) -> None:
    generator = random.Random(seed)
    chosen = [rule.code for rule in ALL_RULES if generator.random() < 0.5]
    config = Config.create("none", enable=chosen)

    for path in corpus_files():
        text = drop_blank_lines(run_formatter("black", path.read_text(encoding="utf-8")) or "", seed)
        first = fix(text, config)

        assert first.passes <= 1
        assert fix(first.text, config).passes == 0
        assert same_tree_ignoring_keyword_names(text, first.text, config)


SINGLE_LINE_UNITS = [
    "x = f()",
    "self.a = 1",
    "total += 1",
    "print(x)",
    "assert x",
    "await run()",
    "yield x",
    "yield from run()",
    "return x",
    "raise E",
    "pass",
    "global g",
    "run()",
    "log.info(x)",
    "y: int = 2",
]


@pytest.mark.parametrize(("first", "second"), list(itertools.product(SINGLE_LINE_UNITS, SINGLE_LINE_UNITS)))
def test_a_container_of_two_single_line_units_gets_no_blank_line_from_any_rule(first: str, second: str) -> None:
    config = Config.create("strong")
    tight = f"async def f():\n    {first}\n    {second}\n"
    padded = f"async def f():\n    {first}\n\n    {second}\n"

    assert fix(tight, config).text == tight
    assert fix(padded, config).text == tight


@pytest.mark.parametrize(("first", "second"), list(itertools.product(SINGLE_LINE_UNITS, SINGLE_LINE_UNITS)))
def test_a_docstring_above_two_single_line_units_is_no_unit_for_any_rule(first: str, second: str) -> None:
    config = Config.create("strong")
    tight = f'async def f():\n    """Doc."""\n    {first}\n    {second}\n'
    padded = f'async def f():\n    """Doc."""\n    {first}\n\n    {second}\n'
    spaced = f'async def f():\n    """Doc."""\n\n    {first}\n\n    {second}\n'

    assert fix(tight, config).text == tight
    assert fix(padded, config).text == tight
    assert fix(spaced, config).text == f'async def f():\n    """Doc."""\n\n    {first}\n    {second}\n'


def test_a_fix_that_settles_on_the_last_pass_is_not_an_error(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(engine, "MAX_PASSES", 1)

    result = fix("a = 1\nb = 2\nrun()\n", Config.create("moderate"))

    assert result.passes == 1
    assert result.text == "a = 1\nb = 2\n\nrun()\n"


def test_a_fix_that_is_left_with_findings_after_the_last_pass_is_an_error(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(engine, "MAX_PASSES", 0)

    with pytest.raises(FixError, match="did not settle in 0 passes"):
        fix("a = 1\nb = 2\nrun()\n", Config.create("moderate"))

    assert fix("a = 1\nb = 2\n\nrun()\n", Config.create("moderate")).passes == 0
