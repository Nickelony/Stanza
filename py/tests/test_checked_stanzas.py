"""Checked stanza borders, the checked bindings kept with their guards, and the jump below a stanza."""

from __future__ import annotations

import pytest

from nickelony_stanza import Config, Options, fix, lint
from nickelony_stanza.analysis.source import SourceFile
from nickelony_stanza.engine import apply_edits
from tests.helpers import findings_with, fix_in_steps, fix_with, function_of


@pytest.mark.parametrize("opening_rule", [(), ("ZA3011",)])
def test_an_opening_chain_is_separated_with_either_owner(opening_rule: tuple[str, ...]) -> None:
    source = "def f(x):\n    if x is None:\n        return\n    if not x.items:\n        return False\n    use(x)\n"
    expected = source.replace("        return False\n    use", "        return False\n\n    use")

    assert fix_with(source, "ZA3012", *opening_rule) == expected
    codes = [finding.code for finding in findings_with(source, "ZA3012", *opening_rule)]
    assert codes == ["ZA3011" if opening_rule else "ZA3012"]


def test_a_chain_reads_bindings_used_by_later_guards() -> None:
    source = "def f():\n    log()\n    a = load()\n    b = load()\n    if not a:\n        return\n    if not b:\n        return\n    use(a, b)\n"
    expected = source.replace("    log()\n", "    log()\n\n").replace("    use(a, b)", "\n    use(a, b)")

    assert fix_with(source, "ZA3012") == expected


def test_a_plain_assertion_is_not_a_checked_stanza() -> None:
    source = "def f():\n    log()\n    response = get()\n    assert response.status == 200\n    use(response)\n"
    assert fix_with(source, "ZA3012") == source


def test_an_assert_only_stanza_has_no_lower_border() -> None:
    source = "def f():\n    log()\n    value = get()\n    assert value is not None\n    use(value)\n"
    expected = source.replace("    log()\n", "    log()\n\n")

    assert fix_with(source, "ZA3012") == expected


def test_a_checked_stanza_leaves_a_following_jump_to_the_jump_rule() -> None:
    source = "def f(x):\n    log()\n    if not x:\n        return\n    return x\n"
    expected = source.replace("    log()\n", "    log()\n\n")

    assert fix_with(source, "ZA3012", "ZA3013") == expected.replace("    return x", "\n    return x")
    assert fix_with(source, "ZA3012") == expected.replace("    return x", "\n    return x")
    assert [finding.code for finding in findings_with(source, "ZA3012", "ZA3013")] == ["ZA3012", "ZA3013"]
    assert [finding.code for finding in findings_with(source, "ZA3012")] == ["ZA3012", "ZA3012"]


def test_a_helper_and_an_if_guard_form_one_checked_chain() -> None:
    source = "def f(x):\n    log()\n    check(x)\n    if x.closed:\n        return False\n    use(x)\n"
    expected = source.replace("    log()\n", "    log()\n\n").replace("    use(x)", "\n    use(x)")

    assert fix_with(source, "ZA3012", options=Options(guard_roots=("check",))) == expected


def test_a_chain_of_narrowing_asserts_gets_only_an_upper_border() -> None:
    source = "def f(a, b):\n    log()\n    assert a is not None\n    assert b is not None\n    use(a, b)\n"
    expected = source.replace("    log()\n", "    log()\n\n")

    assert fix_with(source, "ZA3012") == expected
    assert fix_with(expected, "ZA3012") == expected


@pytest.mark.parametrize("jump_rules", [(), ("ZA3013",)])
def test_a_lone_helper_before_a_jump_gets_its_checked_border(jump_rules: tuple[str, ...]) -> None:
    source = "def f(x):\n    log()\n    check(x)\n    return x\n"
    expected = "def f(x):\n    log()\n\n    check(x)\n\n    return x\n"
    options = Options(guard_roots=("check",))

    assert fix_with(source, "ZA3012", *jump_rules, options=options) == expected
    assert fix_with(expected, "ZA3012", *jump_rules, options=options) == expected


def test_checked_bindings_transfer_separation_above_the_whole_run() -> None:
    source = (
        "def f():\n    log()\n    a = load()\n    b = load()\n    if not a or not b:\n        return\n    use(a, b)\n"
    )

    expected = source.replace("    log()\n", "    log()\n\n").replace("    use(a, b)", "\n    use(a, b)")

    assert fix_with(source, "ZA3012", "ZA3058", "ZA3001", "ZA3002") == expected
    assert fix_with(expected, "ZA3012", "ZA3058", "ZA3001", "ZA3002") == expected


def test_narrowing_assertions_keep_their_checked_binding_run() -> None:
    source = "def f():\n    log()\n    a = load()\n\n    b = load()\n\n    assert a is not None and b is not None\n    use(a, b)\n"
    expected = "def f():\n    log()\n\n    a = load()\n    b = load()\n    assert a is not None and b is not None\n    use(a, b)\n"

    assert fix_with(source, "ZA3012", "ZA3060", "ZA3002") == expected
    assert fix_with(expected, "ZA3012", "ZA3060", "ZA3002") == expected


def test_a_comment_inside_an_assertion_phase_leaves_the_stanza_its_upper_border() -> None:
    source = "def f():\n    log()\n    value = get()\n    assert value is not None\n    # next phase\n    assert value.id == 1\n"
    expected = source.replace("    log()\n", "    log()\n\n")

    assert fix_with(source, "ZA3012") == expected


@pytest.mark.parametrize("barrier", ["    # phase\n", "    # noqa: ZA3005\n", ""])
def test_an_assertion_helper_joins_the_assertion_phase_unless_a_comment_ends_it(barrier: str) -> None:
    source = (
        "def f():\n    log()\n    value = get()\n    assert value is not None\n" + barrier + "    assert_valid(value)\n"
    )

    expected = source.replace("    log()\n", "    log()\n\n") if barrier else source

    assert fix_with(source, "ZA3012") == expected


def test_a_guard_keeps_every_binding_it_checks_above_it() -> None:
    source = (
        "def f():\n    a = get_a()\n\n    b = get_b()\n    if a is None or b is None:\n        return\n    use(a, b)\n"
    )

    assert fix_with(source, "ZA3058") == source.replace("    a = get_a()\n\n", "    a = get_a()\n")


def test_a_later_guard_of_the_chain_keeps_the_binding_it_checks() -> None:
    source = "def f():\n    a = get_a()\n\n    b = get_b()\n    if not b:\n        return\n    if not a:\n        return\n    use(a, b)\n"
    assert fix_with(source, "ZA3058") == source.replace("    a = get_a()\n\n", "    a = get_a()\n")


def test_a_guard_keeps_no_binding_it_does_not_check() -> None:
    source = "def f():\n    a = get_a()\n\n    b = get_b()\n    if not b:\n        return\n    use(a, b)\n"
    assert fix_with(source, "ZA3058") == source


def test_a_multi_line_binding_can_join_a_single_line_narrowing_assertion() -> None:
    source = "def f():\n    value = get(\n        arg,\n    )\n\n    assert value is not None\n    use(value)\n"
    expected = source.replace("    )\n\n", "    )\n")

    assert fix_with(source, "ZA3001", "ZA3060", "ZA3012") == expected


@pytest.mark.parametrize("function", [False, True])
@pytest.mark.parametrize("barrier", ["# next phase\n", "# noqa: E501\n", "# fmt: off\n# fmt: on\n", ""])
@pytest.mark.parametrize(
    "tail", ["assert value.id == 1\n", "assert_valid(value)\n", "assert (\n    value.id == 1\n)\n"]
)
def test_an_assertion_narrows_its_binding_unless_it_opens_an_assertion_phase(
    function: bool, barrier: str, tail: str
) -> None:
    body = "value = get()\n\nassert value is not None\n" + barrier + tail
    source = function_of(body) if function else body
    joins = bool(barrier) or tail.startswith("assert (\n")
    expected = source.replace("get()\n\n", "get()\n") if joins else source

    assert fix_with(source, "ZA3060") == expected


@pytest.mark.parametrize("function", [False, True])
def test_a_phase_of_narrowing_assertions_keeps_every_binding_it_narrows(function: bool) -> None:
    body = "a = get_a()\n\nb = get_b()\n\nassert a is not None\nassert b is not None\nuse(a, b)\n"
    source = function_of(body) if function else body

    assert fix_with(source, "ZA3060") == source.replace("\n\n", "\n")


@pytest.mark.parametrize("function", [False, True])
@pytest.mark.parametrize("comment", ["# next phase", "# noqa: E501", "# type: ignore"])
def test_a_trailing_comment_ends_an_assertion_phase(function: bool, comment: str) -> None:
    body = f"value = get()\n\nassert value is not None  {comment}\nassert value.id == 1\n"
    source = function_of(body) if function else body

    assert fix_with(source, "ZA3060") == source.replace("get()\n\n", "get()\n")


@pytest.mark.parametrize("suppressed", ["", "ZA3012", "ZA3013"])
@pytest.mark.parametrize("jump", ["return item", "raise Error(item)"])
@pytest.mark.parametrize("generic", [[], ["ZA3004"], ["ZA3002", "ZA3055"], ["ZA3052"]])
@pytest.mark.parametrize("prefix", ["", "    audit()\n\n"])
@pytest.mark.parametrize("inner", ["", "\n"])
def test_the_jump_below_a_kept_initialization_takes_the_lower_border_of_its_checked_stanza(
    suppressed: str, jump: str, generic: list[str], prefix: str, inner: str
) -> None:
    comment = f"  # noqa: {suppressed}" if suppressed else ""
    source = f"def f():\n{prefix}    item = Item()\n{inner}    item.check()\n    {jump}{comment}\n"
    expected = source.replace("Item()\n\n", "Item()\n")

    if suppressed != "ZA3013":
        expected = expected.replace(f"    {jump}", f"\n    {jump}")

    config = Config.create(
        "none", enable=["ZA3012", "ZA3062", "ZA3013", *generic], options=Options(guard_roots=("check",))
    )

    findings = lint(source, config)

    assert {finding.code for finding in findings} == {
        *(["ZA3062"] if inner else []),
        *([] if suppressed == "ZA3013" else ["ZA3013"]),
    }

    assert apply_edits(SourceFile(source), findings) == expected
    assert fix(source, config).text == expected
    assert fix(source, config).passes == int(bool(inner) or suppressed != "ZA3013")


@pytest.mark.parametrize("suppressed", ["", "ZA3012", "ZA3013"])
@pytest.mark.parametrize("keeper", [False, True])
def test_the_checked_stanza_separates_a_jump_while_the_jump_rule_does_not(suppressed: str, keeper: bool) -> None:
    comment = f"  # noqa: {suppressed}" if suppressed else ""
    source = f"def f():\n    item = Item()\n\n    item.check()\n    return item{comment}\n"
    rules = ["ZA3012", "ZA3062"] if keeper else ["ZA3012", "ZA3013"]
    expected = source.replace("Item()\n\n", "Item()\n") if keeper else source

    if suppressed != "ZA3012":
        expected = expected.replace("    return item", "\n    return item")

    config = Config.create("none", enable=rules, options=Options(guard_roots=("check",)))

    assert {finding.code for finding in lint(source, config)} == {
        *(["ZA3062"] if keeper else []),
        *([] if suppressed == "ZA3012" else ["ZA3012"]),
    }

    assert fix(source, config).text == expected


@pytest.mark.parametrize("mode", ["first", "last", "batch"])
def test_a_kept_initialization_above_a_silenced_jump_settles_in_any_fix_order(mode: str) -> None:
    source = "def f():\n    item = Item()\n\n    item.check()\n    return item  # noqa: ZA3013\n"
    expected = source.replace("Item()\n\n", "Item()\n")
    config = Config.create("none", enable=["ZA3012", "ZA3062", "ZA3013"], options=Options(guard_roots=("check",)))
    current = fix_in_steps(source, config, mode)

    assert current == expected
    assert lint(current, config) == []


@pytest.mark.parametrize("import_rule", [False, True])
def test_the_gap_below_an_import_has_one_owner(import_rule: bool) -> None:
    source = "def f():\n    log()\n    import sys\n    if sys.flags.debug:\n        return\n    main()\n"
    expected = source.replace("    import sys\n", "    import sys\n\n").replace("    main()", "\n    main()")
    rules = ["ZA3012", *(["ZA3019"] if import_rule else [])]

    assert [finding.code for finding in findings_with(source, *rules)] == [
        "ZA3019" if import_rule else "ZA3012",
        "ZA3012",
    ]

    assert fix_with(source, *rules) == expected


@pytest.mark.parametrize("keeper", [False, True])
def test_a_dependent_binding_chain_moves_the_upper_border_above_the_whole_chain(keeper: bool) -> None:
    source = "def f(p):\n    log()\n    text = read(p)\n    lines = text.split()\n    if not lines:\n        return\n    use(lines)\n"
    upper = "    log()\n" if keeper else "    text = read(p)\n"
    expected = source.replace(upper, upper + "\n").replace("    use(lines)", "\n    use(lines)")
    rules = ["ZA3012", *(["ZA3061"] if keeper else [])]

    assert fix_with(source, *rules) == expected
    assert fix_with(expected, *rules) == expected


@pytest.mark.parametrize("silenced", [False, True])
def test_a_noqa_silences_one_border_of_a_checked_stanza(silenced: bool) -> None:
    comment = "  # noqa: ZA3012" if silenced else ""
    source = f"def f(x):\n    log()\n    if not x:{comment}\n        return\n    use(x)\n"
    expected = source.replace("    use(x)", "\n    use(x)")

    assert fix_with(source, "ZA3012") == (expected if silenced else expected.replace("    log()\n", "    log()\n\n"))


def test_the_lower_border_above_a_jump_is_written_in_the_pass_that_separates_the_bindings() -> None:
    source = "def f(v):\n    publish(v)\n    a = f(v)\n    b = f(a)\n    if b is None: return None\n    return None\n"
    config = Config.create("strong", enable=["ZA3012"])
    expected = source.replace("\n    ", "\n\n    ").replace("(v):\n\n", "(v):\n")
    result = fix(source, config)

    assert result.text == expected
    assert result.passes == 1
    assert ("ZA3012", 6) in {(finding.code, finding.line) for finding in lint(source, config)}


@pytest.mark.parametrize("awaited", [False, True])
def test_an_awaited_guard_helper_is_a_guard(awaited: bool) -> None:
    call = "await check(x)" if awaited else "check(x)"
    source = f"async def f(x):\n    {call}\n    use(x)\n    done()\n"
    expected = source.replace("(x)\n    use", "(x)\n\n    use")

    assert fix_with(source, "ZA3011", options=Options(guard_roots=("check",))) == expected
