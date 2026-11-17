"""Receiver boundaries and focused relationships settle together without stealing a binding from its stanza."""

from __future__ import annotations

from itertools import product

import pytest

from nickelony_stanza import Config, Options, fix, lint
from nickelony_stanza.analysis.source import SourceFile
from nickelony_stanza.engine import apply_edits
from nickelony_stanza.options import ReceiverComparison
from tests.helpers import JUMP_KEEPERS

RELATIONSHIPS = {
    "ZA3057": "if not first:\n    return\n{gap}if not second:\n    return\nuse(first, second)\n",
    "ZA3058": "value = get()\n{gap}if not value:\n    return\nuse(value)\n",
    "ZA3063": "total = 0\n{gap}for x in xs:\n    total += x\nuse(total)\n",
    "ZA3059": "result = compute()\n{gap}return result\n",
    "ZA3060": "value = get()\n{gap}assert value is not None\nuse(value)\n",
    "ZA3062": "item = Item()\n{gap}item.start()\nitem.save()\ndone()\n",
}

PREFIXES = [
    "db.first()\ndb.second()\n",
    "db.first()\n\ndb.second()\n",
    "db.first()\n# section\ndb.second()\n",
    "first = 1\nsecond = 2\n",
    "first = 1\n\nsecond = 2\n\nthird = 3\n",
]

NATIVE = ("ZA3001", "ZA3003", "ZA3005", "ZA3006", "ZA3009", "ZA3013", "ZA3011", "ZA3012")


def _body(prefix: str, pair: str, upper: str, inner: str) -> str:
    body = prefix + upper + pair.replace("{gap}", inner)

    return "def f(xs, first=None, second=None):\n" + "".join(
        "    " + line + "\n" if line else "\n" for line in body.splitlines()
    )


@pytest.mark.parametrize("relationship", RELATIONSHIPS)
@pytest.mark.parametrize("comparison", ["root", "path"])
def test_receiver_and_relationship_policies_settle_in_one_pass(
    relationship: str, comparison: ReceiverComparison
) -> None:
    for receivers, generic, prefix, upper, inner, roots in product(
        product((False, True), repeat=2),
        product((False, True), repeat=3),
        PREFIXES,
        ("", "\n"),
        ("", "\n"),
        (("db",), ("*",)),
    ):
        rules = [relationship, "ZA3002"]

        rules.extend(code for code, on in zip(("ZA3010", "ZA3064"), receivers, strict=True) if on)
        rules.extend(code for code, on in zip(("ZA3004", "ZA3052", "ZA3055"), generic, strict=True) if on)

        if all(receivers) and all(generic):
            rules.extend(NATIVE)

        config = Config.create(
            "none", enable=rules, options=Options(receiver_roots=roots, receiver_comparison=comparison)
        )

        source = _body(prefix, RELATIONSHIPS[relationship], upper, inner)
        result = fix(source, config)

        assert result.passes <= 1, f"{rules}, {roots}, {comparison}:\n{source}\n{result.text}"
        assert fix(result.text, config).passes == 0


def test_a_receiver_boundary_can_open_a_lone_result_stanza_in_the_same_pass() -> None:
    source = "def f():\n    db.first()\n    db.second()\n    result = compute()\n\n    return result\n"

    expected = source.replace("    result = compute()", "\n    result = compute()").replace(
        "compute()\n\n", "compute()\n"
    )

    config = Config.create("none", enable=["ZA3010", "ZA3059"], options=Options(receiver_roots=("db",)))
    result = fix(source, config)

    assert result.text == expected
    assert result.passes == 1


def test_a_result_binding_is_not_lone_when_a_run_keeper_removes_its_upper_border() -> None:
    source = "def f():\n    first = 1\n\n    result = compute()\n\n    return result\n"
    expected = source.replace("    first = 1\n\n", "    first = 1\n")
    config = Config.create("none", enable=["ZA3002", "ZA3055", "ZA3059", "ZA3013"])
    result = fix(source, config)

    assert result.text == expected
    assert result.passes == 1


def test_gathering_leaves_the_loose_prefix_outside_a_kept_seed_and_loop() -> None:
    source = "def f(xs):\n    first = 1\n\n    second = 2\n\n    third = 3\n\n    total = 0\n\n    for x in xs:\n        total += x\n    use(total)\n"
    expected = source.replace("    total = 0\n\n", "    total = 0\n")
    config = Config.create("none", enable=["ZA3002", "ZA3052", "ZA3063"])
    result = fix(source, config)

    assert result.text == expected
    assert result.passes == 1


@pytest.mark.parametrize("keeper", ["ZA3064", "ZA3061"])
def test_a_noqa_on_one_edge_of_a_kept_group_leaves_that_edge_to_the_next_rule(keeper: str) -> None:
    body = "db.a()\n\ndb.b()\n\ndb.c()" if keeper == "ZA3064" else "a = load()\n\nb = use(a)\n\nc = use(b)"
    source = body + f"  # noqa: {keeper}\n"
    generic = ["ZA3004"] if keeper == "ZA3064" else ["ZA3002"]
    config = Config.create("none", enable=[keeper, "ZA3055", *generic], options=Options(receiver_roots=("db",)))
    expected = source.replace("\n\n", "\n")
    findings = lint(source, config)

    assert [finding.code for finding in findings] == [keeper, "ZA3055"]
    assert apply_edits(SourceFile(source), findings) == expected
    assert fix(source, config).text == expected
    assert lint(expected, config) == []


@pytest.mark.parametrize(("keeper", "body"), JUMP_KEEPERS)
def test_keepers_jumps_and_run_rules_settle_in_one_pass_in_any_combination(keeper: str, body: str) -> None:
    variants = ("ZA3002", "ZA3004", "ZA3010", "ZA3052", "ZA3055")

    for choices, prefix in product(
        product((False, True), repeat=len(variants)), ("", "prepare()\n\n", "first = 1\n\nsecond = 2\n\n")
    ):
        rules = [keeper, "ZA3013", "ZA3059", *[code for code, on in zip(variants, choices, strict=True) if on]]
        source = _body(prefix, body, "", "")
        config = Config.create("none", enable=rules, options=Options(receiver_roots=("db",), max_loose_run_size=1))
        result = fix(source, config)

        assert result.passes <= 1, f"{rules}:\n{source}\n{result.text}"
        assert fix(result.text, config).passes == 0


@pytest.mark.parametrize("silenced", [False, True])
def test_a_noqa_silences_one_border_of_a_receiver_group(silenced: bool) -> None:
    comment = "  # noqa: ZA3010" if silenced else ""
    source = f"prepare()\ndb.first()\ndb.second()\nfinish(){comment}\n"
    expected = source.replace("prepare()\n", "prepare()\n\n")
    config = Config.create("none", enable=["ZA3010"], options=Options(receiver_roots=("db",)))

    assert fix(source, config).text == (expected if silenced else expected.replace("db.second()\n", "db.second()\n\n"))
