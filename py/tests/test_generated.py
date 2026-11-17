"""Generated bodies of single-line and block statements settle in one pass under any set of rules."""

from __future__ import annotations

import random
from collections.abc import Collection

import pytest

from nickelony_stanza import Config, Options, fix, lint
from nickelony_stanza.analysis import session
from nickelony_stanza.analysis.model import Container
from nickelony_stanza.rules import ALL_RULES, TWO_UNIT_RULES
from tests.helpers import OPTION_VARIANTS, same_tree

STATEMENTS = [
    *["a = 1", "b = 2", "print(a)", "print(b)", "self.q = 3", "saved.connect(h)", "assert a", "pass"] * 3,
    "c: str = ''",
    "b = build(a)",
    "c = build(b)",
    "d: bytes = convert(c)",
    "db.add(a)",
    "db.child.save()",
    "await db.flush()",
    "db.child.value = a",
    "self.db.save()",
    "cache.clear()",
    "d, e = 1, 2",
    "self.y += 1",
    "log.info(b)",
    "self.assertEqual(a, 1)",
    "await run()",
    "yield a",
    "yield from g()",
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

    config = Config.create(
        "none",
        enable=[rule.code for rule in ALL_RULES if rng.random() < share],
        options=rng.choice([Options(), *OPTION_VARIANTS.values()]),
    )

    first = fix(text, config)

    assert first.passes <= 1
    assert fix(first.text, config).passes == 0
    assert same_tree(text, first.text, config)
    assert lint(first.text, config) == []


# Statements that bind, check, seed, initialize and call through a receiver, alone and in the stanzas the
# relationship rules judge, with blank lines inside some of them.
RELATED = [
    *["a = 1", "log()", "audit(a)", "total = 0", "names = []", "value = get(a)", "result = compute(a)"],
    *["svc = Service(a)", "svc.start()", "svc.run()", "db.open()", "db.count = 1", "await db.flush()", "log.write()"],
    *["b = f(a)", "c = f(b)", "if not value: return", "assert value is not None", "check(a)", "return result"],
    "for x in xs:\n    total += x\n    names.append(x)",
    "if value is None:\n    return None",
    "x = f(\n    1,\n)",
    "# note",
    "svc = Service(a)\n\nsvc.start()\n\nsvc.run()",
    "value = get(a)\nif not value:\n    raise E(a)",
    "total = 0\n\nnames = []\n\nfor x in xs:\n    total += x\n    names.append(x)",
    "b = f(a)\n\nc = f(b)",
    "if not a: return\n\nif not xs: return",
]

RELATIONSHIP_RULES = ["ZA3012", "ZA3010", *(rule.code for rule in TWO_UNIT_RULES)]


def _generate_related(seed: int) -> str:
    rng = random.Random(seed)
    lines: list[str] = []

    for _ in range(rng.randint(3, 9)):
        lines.extend("    " + line if line else "" for line in rng.choice(RELATED).split("\n"))
        lines.extend([""] * rng.choice([0, 1, 1, 2]))

    while not lines[-1]:
        lines.pop()

    return "async def f(a, xs, svc, db, log):\n" + "\n".join(lines) + "\n"


def _mark(text: str, line_numbers: Collection[int], code: str) -> str:
    lines = text.split("\n")

    for number in line_numbers:
        lines[number - 1] += f"  # noqa: {code}"

    return "\n".join(lines)


def _claimed_lines(monkeypatch: pytest.MonkeyPatch, text: str, config: Config, code: str) -> set[int]:
    """Reads the first lines of the units below the gaps a rule asks for, blank or tight, in every container."""
    claims: list[tuple[Container, dict[str, list[bool]]]] = []
    own_gaps = session._own_gaps

    def recording(container: Container, judging: object, claimed: dict[str, list[bool]], owned: object) -> None:
        claims.append((container, claimed))
        own_gaps(container, judging, claimed, owned)  # type: ignore[arg-type]

    monkeypatch.setattr(session, "_own_gaps", recording)
    lint(text, config)
    monkeypatch.undo()

    return {
        container.units[index + 1].first_line
        for container, claimed in claims
        for index, flag in enumerate(claimed.get(code, ()))
        if flag
    }


@pytest.mark.parametrize("seed", range(150))
def test_a_relationship_body_settles_and_a_noqa_on_each_gap_of_a_keeper_is_the_keeper_off(
    seed: int, monkeypatch: pytest.MonkeyPatch
) -> None:
    text = _generate_related(seed)
    rng = random.Random(seed)
    chosen = [code for code in RELATIONSHIP_RULES if rng.random() < 0.5]
    options = rng.choice([Options(), *OPTION_VARIANTS.values()])
    config = Config.create("strong", enable=chosen, options=options)

    first = fix(text, config)
    edited = [edit.line for finding in lint(text, config) for edit in finding.edits]

    assert first.passes <= 1
    assert fix(first.text, config).passes == 0
    assert same_tree(text, first.text, config)
    assert len(edited) == len(set(edited)), "two rules own one gap"

    # The keeper silenced is one that asks for a gap of the text: an enabled one first, else one enabled for this check.
    keepers = [rule.code for rule in TWO_UNIT_RULES]
    enabled = [code for code in keepers if code in chosen]
    others = [code for code in keepers if code not in chosen]

    for keeper in [*rng.sample(enabled, len(enabled)), *rng.sample(others, len(others))]:
        config = Config.create("strong", enable=[*chosen, keeper], options=options)
        marked = _claimed_lines(monkeypatch, text, config, keeper)

        if marked:
            break
    else:
        return

    # A comment changes the gap below its line, so the keeper's gaps are read again until the comments add none.
    for _ in range(4):
        more = _claimed_lines(monkeypatch, _mark(text, marked, "ZA0000"), config, keeper)

        if more <= marked:
            break

        marked |= more

    off = Config.create("strong", enable=[code for code in chosen if code != keeper], options=options)
    silenced = fix(_mark(text, marked, keeper), config)

    assert silenced.passes <= 1
    assert silenced.text.replace(keeper, "ZA0000") == fix(_mark(text, marked, "ZA0000"), off).text
