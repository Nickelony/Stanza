"""The tree comparison of the tests accepts the one change a call rule makes and no other."""

from __future__ import annotations

import pytest

from tests.helpers import CONFIGS, same_tree

NAMING = CONFIGS["strong"]
NO_NAMING = CONFIGS["minimal"]


@pytest.mark.parametrize(
    ("before", "after"),
    [
        ("f(a, b)\n", "f(x=a, y=b)\n"),
        ("f(a, b)\n", "f(a, y=b)\n"),
        ("f(a, k=1)\n", "f(x=a, k=1)\n"),
        ("f(a, **rest)\n", "f(x=a, **rest)\n"),
        ("f(g(a, b), c)\n", "f(x=g(y=a, z=b), w=c)\n"),
        ("f(a)\n\n\nx = 1\n", "f(a)\nx = 1\n"),
    ],
)
def test_a_call_may_pass_a_tail_of_its_positional_arguments_by_name(before: str, after: str) -> None:
    assert same_tree(before, after, NAMING)


@pytest.mark.parametrize(
    ("before", "after"),
    [
        ("f(a, b)\n", "f(x=b, y=a)\n"),
        ("f(a, k=1)\n", "f(x=a)\n"),
        ("f(a, b=1)\n", "f(b=1, x=a)\n"),
        ("f(a, b)\n", "g(x=a, y=b)\n"),
        ("f(a, b)\n", "f(x=a, y=b)\nz = 1\n"),
        ("f(a, 'x')\n", "f(a, 'y')\n"),
        ("x = f(True)\n", "x = f(1)\n"),
        ("x = 0\n", "x = False\n"),
        ("x = 1\n", "x = 1.0\n"),
    ],
)
def test_no_other_change_to_the_tree_is_accepted(before: str, after: str) -> None:
    assert not same_tree(before, after, NAMING)


def test_a_name_is_a_change_unless_a_call_rule_is_enabled() -> None:
    assert not same_tree("f(a, b)\n", "f(x=a, y=b)\n")
    assert not same_tree("f(a, b)\n", "f(x=a, y=b)\n", NO_NAMING)
