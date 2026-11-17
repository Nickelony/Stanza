"""Dependent binding chains keep immediate value-producing steps together."""

from __future__ import annotations

import pytest

from nickelony_stanza import Config, Options, fix, lint
from nickelony_stanza.analysis.source import SourceFile
from nickelony_stanza.engine import apply_edits
from tests.helpers import fix_with


def test_async_binding_pipeline_is_kept_as_one_group() -> None:
    source = "async def read():\n    file = await handle.get_file()\n\n    data = await file.array_buffer()\n\n    stream = MemoryStream(data)\n"
    expected = source.replace("\n\n", "\n")
    config = Config.create("none", enable=["ZA3061"])
    result = fix(source, config)

    assert result.text == expected
    assert result.passes == 1
    assert fix(result.text, config).passes == 0

    findings = lint(source, config)

    assert len(findings) == 1
    assert findings[0].code == "ZA3061"
    assert apply_edits(SourceFile(source), [findings[0]]) == expected


@pytest.mark.parametrize(
    "following",
    [
        "next_value = transform(value)",
        "next_value: bytes = value.data",
        "next_value = value[0]",
        "next_value = value + 1",
        "next_value = f'{value}'",
        "value = transform(value)",
    ],
)
def test_value_reads_in_single_name_initializers_connect_bindings(following: str) -> None:
    source = f"value = load()\n\n{following}\n"
    assert fix_with(source, "ZA3061") == source.replace("\n\n", "\n")


@pytest.mark.parametrize(
    "source",
    [
        "value = load()\n\nresult = other.value\n",
        "value = load()\n\nresult = 'value'\n",
        "value = load()\n\nresult: value = 1\n",
        "value = load()\n\nresult = lambda: value\n",
        "value = load()\n\nresult = (item for item in value)\n",
        "value = load()\n\nresult = [item for item in value]\n",
        "value = load()\n\nresult = {item for item in value}\n",
        "value = load()\n\nresult = {item: item for item in value}\n",
        "value = load()\n\nresult = ((value := other()), value)\n",
        "value = other = load()\n\nresult = use(value)\n",
        "value, = load()\n\nresult = use(value)\n",
        "[value] = load()\n\nresult = use(value)\n",
        "value = load()\n\nresult, = use(value)\n",
        "value = load()\n\nresult += value\n",
        "value = load()\n\nresult.data = value\n",
        "value = load()\n\nresult: value\n",
        "value = load(\n    arg,\n)\n\nresult = use(value)\n",
        "value = load()\n\nresult = use(\n    value,\n)\n",
        "value = load()\n\n# Section\nresult = use(value)\n",
        "value = load()  # Section\n\nresult = use(value)\n",
        "# fmt: off\nvalue = load()\n\nresult = use(value)\n# fmt: on\n",
        "value = load(); result = use(value)\n",
        "value = load()\n\nother = independent()\n\nresult = use(value)\n",
        "class C:\n    value = load()\n\n    result = use(value)\n",
    ],
)
def test_non_binding_reads_and_barriers_remain_outside_the_chain(source: str) -> None:
    assert fix_with(source, "ZA3061") == source


def test_destructuring_groups_retain_their_shape_boundary() -> None:
    source = "catalog, store = read_page()\nconfig, flags = read_state()\npreference = choose(config)\nowner = find_owner(catalog, config)\n"
    expected = source.replace("preference =", "\npreference =")
    options = Options(declaration_shapes="split")

    assert fix_with(source, "ZA3002", "ZA3061", options=options) == expected


def test_existing_tight_sections_are_preserved() -> None:
    source = "a = load()\nb = use(a)\n\nc = use(b)\nd = use(c)\n"
    assert fix_with(source, "ZA3061") == source


def test_single_fix_cannot_create_an_artificial_section() -> None:
    source = "a = load()\n\nb = use(a)\n\nc = use(b)\nd = use(c)\n"
    expected = source.replace("\n\n", "\n")
    config = Config.create("none", enable=["ZA3061"])
    findings = lint(source, config)

    assert findings
    assert apply_edits(SourceFile(source), [findings[0]]) == expected
    assert fix(source, config).text == expected


def test_declaration_type_separation_moves_above_the_complete_chain() -> None:
    source = "prepare()\na: int = read()\n\nb: str = convert(a)\n\nc: bytes = encode(b)\nfinish()\n"
    expected = "prepare()\n\na: int = read()\nb: str = convert(a)\nc: bytes = encode(b)\n\nfinish()\n"
    config = Config.create("none", enable=["ZA3002", "ZA3003", "ZA3061"])
    result = fix(source, config)

    assert result.text == expected
    assert result.passes == 1


@pytest.mark.parametrize("keeper", ["ZA3059", "ZA3062"])
def test_a_binding_inside_the_chain_is_not_a_lone_stanza_anchor(keeper: str) -> None:
    tail = "return b" if keeper == "ZA3059" else "b.start()"
    source = f"def f():\n    a = load()\n\n    b = build(a)\n\n    {tail}\n"
    expected = source.replace("a = load()\n\n", "a = load()\n")
    config = Config.create("none", enable=["ZA3061", keeper])
    result = fix(source, config)

    assert result.text == expected
    assert result.passes == 1


def test_disabled_rule_and_noqa_leave_the_chain_untouched() -> None:
    source = "a = load()\n\nb = use(a)\n"

    assert fix_with(source) == source
    assert fix_with("# flake8: noqa\n" + source, "ZA3061") == "# flake8: noqa\n" + source


@pytest.mark.parametrize("newline", ["\n", "\r\n"])
def test_line_endings_are_preserved(newline: str) -> None:
    source = "a = load()\n\nb = use(a)\n".replace("\n", newline)
    assert fix_with(source, "ZA3061") == "a = load()\nb = use(a)\n".replace("\n", newline)


def test_jump_separator_reads_the_chain_as_tight_before_the_first_fix() -> None:
    source = "def f():\n    a = load()\n\n    b = use(a)\n\n    c = use(b)\n    return c\n"
    expected = "def f():\n    a = load()\n    b = use(a)\n    c = use(b)\n\n    return c\n"
    result = fix(source, Config.create("none", enable=["ZA3013", "ZA3061"]))

    assert result.text == expected
    assert result.passes == 1


@pytest.mark.parametrize(
    ("keeper", "tail"),
    [
        ("ZA3058", "if not c:\n    return\nuse(c)\n"),
        ("ZA3060", "assert c is not None\nuse(c)\n"),
        ("ZA3059", "return c\n"),
        ("ZA3062", "c.start()\nc.save()\ndone()\n"),
        ("ZA3063", "total = 0\n\nfor item in c:\n    total += item\nuse(total)\n"),
    ],
)
def test_binding_chains_and_existing_relationships_settle_together(keeper: str, tail: str) -> None:
    from itertools import product

    prefixes = ["", "prepare()\n", "db.first()\ndb.second()\n", "x = 1\n\ny = 2\n\nz = 3\n"]
    variants = ("ZA3002", "ZA3003", "ZA3052", "ZA3055", "ZA3013")

    for enabled, prefix, inner, outer in product(
        product((False, True), repeat=len(variants)), prefixes, ("", "\n"), ("", "\n")
    ):
        body = prefix + outer + f"a = load()\n{inner}b = build(a)\n{inner}c = build(b)\n{inner}" + tail
        source = "def f():\n" + "".join("    " + line + "\n" if line else "\n" for line in body.splitlines())
        rules = ["ZA3061", keeper, "ZA3012", "ZA3010", "ZA3064", "ZA3001"]

        rules.extend(code for code, active in zip(variants, enabled, strict=True) if active)

        config = Config.create("none", enable=rules, options=Options(receiver_roots=("db",)))
        result = fix(source, config)

        assert result.passes <= 1, f"{rules}:\n{source}\n{result.text}"
        assert fix(result.text, config).passes == 0


def test_outer_padding_is_not_added_by_the_keeper() -> None:
    source = "prepare()\na = load()\n\nb = use(a)\nfinish()\n"
    assert fix_with(source, "ZA3061") == source.replace("load()\n\n", "load()\n")


@pytest.mark.parametrize("decls", [("ZA3003",), ("ZA3002", "ZA3003")])
def test_mixed_typed_sections_are_protected_from_global_run_keeping(decls: tuple[str, ...]) -> None:
    source = "a: int = load()\nb: str = use(a)\n\nc: str = use(b)\nd: int = use(c)\n"
    config = Config.create("none", enable=["ZA3061", "ZA3055", "ZA3052", *decls])

    assert fix(source, config).text == source
    assert fix(source, config).passes == 0

    native = Config.create("none", enable=["ZA3055", *decls])
    assert fix(source, native).text != source


def test_a_tight_upper_gap_cannot_become_a_new_section_after_prefix_joining() -> None:
    source = "def f():\n    x = 1\n\n    y = 2\n\n    z = 3\n    a = load()\n    b = build(a)\n    c = build(b)\n    assert c is not None\n    use(c)\n"
    expected = source.replace("\n\n", "\n")
    config = Config.create("none", enable=["ZA3061", "ZA3060", "ZA3012", "ZA3002", "ZA3003", "ZA3055"])
    result = fix(source, config)

    assert result.text == expected
    assert result.passes == 1
