"""Generated bodies of single-line and block statements settle in one pass under any set of rules."""

from __future__ import annotations

import random
from collections.abc import Collection, Sequence

import pytest

from nickelony_stanza import Config, Options, fix, lint
from nickelony_stanza.analysis.model import Container
from nickelony_stanza.analysis.source import SourceFile
from nickelony_stanza.engine import Edit, Finding, apply_edits, find_findings
from nickelony_stanza.rules import ALL_RULES, KEEP_ORDER, TWO_UNIT_RULES
from tests.helpers import OPTION_VARIANTS, same_tree_ignoring_keyword_names

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
    assert same_tree_ignoring_keyword_names(text, first.text, config)
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


def _claimed_lines(text: str, config: Config, code: str) -> set[int]:
    """Reads the first lines of the units below the gaps a rule asks for, blank or tight, in every container."""
    lines: set[int] = set()

    def observe(container: Container, claimed: dict[str, list[bool]]) -> None:
        lines.update(container.units[index + 1].first_line for index, flag in enumerate(claimed.get(code, ())) if flag)

    find_findings(SourceFile(text), config, observer=observe)

    return lines


def _edited_lines(findings: Sequence[Finding]) -> list[int]:
    """
    Lists the line of each edit the findings carry.

    The findings of one run of one rule each carry the edits of the whole run, so a run counts once.
    An edit two rules share stays twice.
    """
    groups = dict.fromkeys((finding.code, finding.edits) for finding in findings)
    return [edit.line for _, group in groups for edit in group]


@pytest.mark.parametrize("seed", range(150))
def test_a_relationship_body_settles_and_a_noqa_on_each_gap_of_a_keeper_is_the_keeper_off(seed: int) -> None:
    text = _generate_related(seed)
    rng = random.Random(seed)
    chosen = [code for code in RELATIONSHIP_RULES if rng.random() < 0.5]
    options = rng.choice([Options(), *OPTION_VARIANTS.values()])
    config = Config.create("strong", enable=chosen, options=options)

    first = fix(text, config)

    edited = _edited_lines(lint(text, config))

    assert first.passes <= 1
    assert fix(first.text, config).passes == 0
    assert same_tree_ignoring_keyword_names(text, first.text, config)
    assert len(edited) == len(set(edited)), "two rules own one gap"

    # The keeper silenced is one that asks for a gap of the text: an enabled one first, else one enabled for this check.
    keepers = [rule.code for rule in KEEP_ORDER if rule.claim is not None]
    enabled = [code for code in keepers if code in chosen]
    others = [code for code in keepers if code not in chosen]

    for keeper in [*rng.sample(enabled, len(enabled)), *rng.sample(others, len(others))]:
        config = Config.create("strong", enable=[*chosen, keeper], options=options)
        marked = _claimed_lines(text, config, keeper)

        if marked:
            break
    else:
        return

    # A comment changes the gap below its line, so the keeper's gaps are read again until the comments add none.
    for _ in range(4):
        more = _claimed_lines(_mark(text, marked, "ZA0000"), config, keeper)

        if more <= marked:
            break

        marked |= more

    off = Config.create("strong", enable=[code for code in chosen if code != keeper], options=options)
    silenced = fix(_mark(text, marked, keeper), config)

    assert silenced.passes <= 1
    assert silenced.text.replace(keeper, "ZA0000") == fix(_mark(text, marked, "ZA0000"), off).text


_GAP = Edit(line=2, delete=1, insert="")


def _finding(rule_index: int, *edits: Edit) -> Finding:
    return Finding(edits[0].line, 1, ALL_RULES[rule_index], "message", edits)


def test_two_rules_that_own_one_gap_count_the_edit_twice() -> None:
    owners = [_finding(0, _GAP), _finding(1, _GAP)]
    assert _edited_lines(owners) == [2, 2]


def test_the_findings_of_one_run_count_its_edits_once() -> None:
    edits = (_GAP, Edit(line=4, delete=1, insert=""))
    run = [_finding(0, *edits), _finding(0, *edits), _finding(0, *edits)]

    assert _edited_lines(run) == [2, 4]


def test_a_gap_two_rules_own_is_not_hidden_when_the_edits_apply() -> None:
    source = SourceFile("a = 1\n\n\nb = 2\n")

    assert apply_edits(source, [_finding(0, _GAP)]) == "a = 1\n\nb = 2\n"
    assert apply_edits(source, [_finding(0, _GAP), _finding(0, _GAP)]) == "a = 1\n\nb = 2\n"
    assert apply_edits(source, [_finding(0, _GAP), _finding(1, _GAP)]) == "a = 1\nb = 2\n"


@pytest.mark.parametrize("which", [[0], [1], [2], [0, 1, 2]])
def test_each_finding_of_a_run_settles_it_as_the_whole_run_does(which: list[int]) -> None:
    text = "a = 1\n\nb = 2\n\nc = 3\n\nd = 4\n"
    config = Config.create("none", enable=["ZA3052"])
    findings = lint(text, config)

    assert len(findings) == 3

    fixed = apply_edits(SourceFile(text), [findings[index] for index in which])

    assert fixed == "a = 1\nb = 2\nc = 3\nd = 4\n"
    assert lint(fixed, config) == []
