"""Generated bodies of single-line and block statements settle in one pass under any set of rules."""

from __future__ import annotations

import random

import pytest

from nickelony_stanza import Config, fix, lint
from nickelony_stanza.rules import ALL_RULES
from tests.helpers import same_tree

STATEMENTS = [
    *["a = 1", "b = 2", "print(a)", "print(b)", "self.q = 3", "saved.connect(h)", "assert a", "pass"] * 3,
    "c: str = ''",
    "d, e = 1, 2",
    "self.y += 1",
    "log.info(b)",
    "self.assertEqual(a, 1)",
    "await run()",
    "closed.disconnect(h)",
    "super().f()",
    "x = f(\n    1,\n)",
    "if a: return",
    "if a:\n    b()",
    "for i in y:\n    pass",
    "raise E",
    "import os",
    "global g",
    "# note",
    "y = 2  # trail",
    "def inner():\n    pass",
    "try:\n    a()\nexcept E:\n    b()\nelse:\n    c()\nfinally:\n    d()",
    "if a:\n    b()\nelif c:\n    d()\nelse:\n    e()",
    "match v:\n    case 1:\n        a()\n\n    case 2:\n        b()\n    case 3: c()",
    "while a:\n    b()\nelse:\n    c()",
]

HEADERS = {
    "module": ("", 0),
    "function": ("async def f(self, a):\n", 1),
    "docstring": ('async def f(self, a):\n    """Doc."""\n', 1),
    "class": ("class A(B):\n    async def f(self, a):\n", 2),
}


def _indent(block: str, depth: int) -> str:
    return "\n".join("    " * depth + line if line else line for line in block.split("\n"))


def _generate(seed: int) -> str:
    rng = random.Random(seed)
    header, depth = HEADERS[rng.choice(list(HEADERS))]

    lines: list[str] = [""] * rng.choice([0, 0, 1, 2]) if header else []

    for _ in range(rng.randint(4, 16)):
        lines.append(_indent(rng.choice(STATEMENTS), depth))
        lines.extend([""] * rng.choice([0, 0, 1, 1, 2]))

    return header + "\n".join(lines) + "\n"


@pytest.mark.parametrize("seed", range(400))
def test_a_generated_text_settles_in_one_pass_under_a_random_set_of_rules(seed: int) -> None:
    text = _generate(seed)
    rng = random.Random(seed)
    share = rng.choice([0.3, 0.6, 0.9])

    config = Config.create("none", enable=[rule.code for rule in ALL_RULES if rng.random() < share])
    first = fix(text, config)

    assert first.passes <= 1
    assert fix(first.text, config).passes == 0
    assert same_tree(text, first.text, config)
    assert lint(first.text, config) == []
