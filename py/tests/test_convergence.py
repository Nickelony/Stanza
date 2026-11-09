"""Fixing settles in one pass to one text whatever the rule order, and the syntax tree stays."""

from __future__ import annotations

import itertools
import random
from pathlib import Path

import pytest

from nickelony_stanza import Config, FixError, engine, fix, lint
from nickelony_stanza.rules import ALL_RULES
from tests.helpers import CONFIGS, add_blank_lines, corpus_files, drop_blank_lines, run_formatter, same_tree


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


def _orders(config: Config) -> dict[str, Config]:
    rules = list(config.rules)
    shuffled = []

    for seed in (1, 2, 3):
        mixed = list(rules)
        random.Random(seed).shuffle(mixed)
        shuffled.append(mixed)

    variants = {"declared": rules, "reversed": rules[::-1], "rotated": rules[3:] + rules[:3]}
    variants.update({f"shuffled {index}": mixed for index, mixed in enumerate(shuffled, start=1)})

    return {name: Config(tuple(ordered), config.options) for name, ordered in variants.items()}


@pytest.mark.parametrize("name", CONFIGS)
@pytest.mark.parametrize("path", corpus_files(), ids=lambda path: path.name)
def test_every_rule_order_settles_to_one_text(name: str, path: Path) -> None:
    base = CONFIGS[name]

    for text in _inputs(path):
        outputs = {}

        for order, config in _orders(base).items():
            first = fix(text, config)
            second = fix(first.text, config)

            assert first.passes <= 1, f"{order}: the fix took {first.passes} passes"
            assert second.passes == 0, f"{order}: a second fix changes the text"
            assert lint(first.text, config) == [], f"{order}: the output does not lint clean"
            assert same_tree(text, first.text, config), f"{order}: the syntax tree changed"

            outputs[order] = first.text

        assert len(set(outputs.values())) == 1, "the rule orders write different texts"


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
        assert same_tree(text, first.text, config)


SINGLE_LINE_UNITS = [
    "x = f()",
    "self.a = 1",
    "total += 1",
    "print(x)",
    "assert x",
    "await run()",
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
