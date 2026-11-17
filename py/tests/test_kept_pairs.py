"""Kept pairs and chains: what they join, the separation they move above their group, and how `# noqa` releases them."""

from __future__ import annotations

import pytest

from nickelony_stanza import Config, Options, fix, lint
from nickelony_stanza.analysis.source import SourceFile
from nickelony_stanza.engine import apply_edits
from tests.helpers import JUMP_KEEPERS, findings_with, fix_with, function_of


def test_a_kept_accumulator_moves_separation_above_its_seed() -> None:
    source = "def f(xs):\n    check(xs)\n    total = 0\n\n    for x in xs:\n        total += x\n    show(total)\n"
    expected = "def f(xs):\n    check(xs)\n\n    total = 0\n    for x in xs:\n        total += x\n\n    show(total)\n"

    assert fix_with(source, "ZA3001", "ZA3063") == expected
    assert fix_with(expected, "ZA3001", "ZA3063") == expected


def test_an_opening_seed_needs_no_upper_separation() -> None:
    source = "def f(xs):\n    total = 0\n\n    for x in xs:\n        total += x\n    show(total)\n"
    expected = "def f(xs):\n    total = 0\n    for x in xs:\n        total += x\n\n    show(total)\n"

    assert fix_with(source, "ZA3001", "ZA3063") == expected


def test_a_transferred_border_preserves_a_run_above_the_seed() -> None:
    source = "def f(xs):\n    count = 1\n    total = 0\n    for x in xs:\n        total += x\n    show(total)\n"
    expected = "def f(xs):\n    count = 1\n    total = 0\n    for x in xs:\n        total += x\n\n    show(total)\n"

    assert fix_with(source, "ZA3001", "ZA3002", "ZA3055", "ZA3063") == expected


def test_a_comment_blocks_transfer_above_the_seed() -> None:
    source = (
        "def f(xs):\n    check(xs)\n    # total\n    total = 0\n    for x in xs:\n        total += x\n    show(total)\n"
    )

    expected = source.replace("        total += x\n    show", "        total += x\n\n    show")

    assert fix_with(source, "ZA3001", "ZA3063") == expected


@pytest.mark.parametrize("prefix", ["", "log()\n"])
def test_the_seeds_a_loop_builds_form_one_group_with_it(prefix: str) -> None:
    source = function_of(
        prefix + "total = 0\ncount = 0\n\nfor x in xs:\n    total += x\n    count += 1\nreturn total, count"
    )

    expected = source.replace("count = 0\n\n", "count = 0\n").replace("    return", "\n    return")

    if prefix:
        expected = expected.replace("    log()\n", "    log()\n\n")

    assert fix(source, Config.create("strong", enable=["ZA3063"])).text == expected


def test_a_seed_the_loop_does_not_build_stays_outside_the_group() -> None:
    source = function_of("count = 0\ntotal = 0\n\nfor x in xs:\n    total += x\nreturn total, count")
    expected = function_of("count = 0\n\ntotal = 0\nfor x in xs:\n    total += x\n\nreturn total, count")

    assert fix(source, Config.create("strong", enable=["ZA3063"])).text == expected


@pytest.mark.parametrize(
    "statement",
    [
        "values[total] = x",
        "values[total], other = x, 1",
        "total[x] = 1",
        "total[x] += 1",
        "total: int",
        "def later():\n    total = x",
        "async def later():\n    total = x",
        "class Later:\n    total = x",
        "callback = lambda: (total := x)",
        "values = [total for total in xs]",
    ],
)
def test_reads_element_writes_annotations_and_nested_scopes_do_not_build_an_accumulator(statement: str) -> None:
    source = function_of(
        "total = 0\n\nfor x in xs:\n" + "\n".join("    " + line for line in statement.splitlines()) + "\nuse(total)"
    )

    assert fix(source, Config.create("none", enable=["ZA3063"])).text == source


@pytest.mark.parametrize(
    "statement",
    [
        "total += x",
        "total = x",
        "total, other = x, 1",
        "total: int = x",
        "if x:\n    total += x",
        "for total in xs:\n    use(total)",
        "while ready():\n    total += x",
        "total.append(x)",
    ],
)
def test_an_update_the_loop_body_executes_builds_an_accumulator(statement: str) -> None:
    source = function_of(
        "total = 0\n\nfor x in xs:\n" + "\n".join("    " + line for line in statement.splitlines()) + "\nuse(total)"
    )

    assert fix(source, Config.create("none", enable=["ZA3063"])).text == source.replace("total = 0\n\n", "total = 0\n")


SILENCED = {
    "ZA3057": "log()\nif not xs:\n    return\n\nif not values:{noqa}\n    return\nuse(xs, values)",
    "ZA3058": "log()\nvalue = get()\n\nif not value:{noqa}\n    return\nuse(value)",
    "ZA3063": "log()\ntotal = 0\n\nfor x in xs:{noqa}\n    total += x\nreturn total",
    "ZA3059": "log()\n\nresult = compute()\n\nreturn result{noqa}",
    "ZA3060": "log()\nvalue = get()\n\nassert value is not None{noqa}\nuse(value)",
    "ZA3062": "log()\n\nitem = Item()\n\nitem.start(){noqa}\nitem.save()\nfinish()",
    "ZA3064": "log()\ndb.first()\n\ndb.second(){noqa}\nfinish()",
    "ZA3061": "log()\ntext = read()\n\nlines = text.split(){noqa}\nuse(lines)",
}


def test_a_noqa_on_a_seed_loop_moves_no_separation_above_the_seed() -> None:
    source = function_of("log()\ntotal = 0\n\nfor x in xs:  # noqa: ZA3063\n    total += x\nreturn total")
    expected = source.replace("    return", "\n    return")
    config = Config.create("moderate", enable=["ZA3063"])

    assert [(finding.line, finding.code) for finding in lint(source, config)] == [(7, "ZA3001")]
    assert fix(source, config).text == expected

    assert fix(source.replace("  # noqa: ZA3063", ""), config).text == function_of(
        "log()\n\ntotal = 0\nfor x in xs:\n    total += x\n\nreturn total"
    )


@pytest.mark.parametrize(
    ("rule", "source", "third"),
    [
        ("ZA3011", "def f(value):\n    if not value: return\n    use(value)\n", "    done()\n"),
        ("ZA3014", "class A(B):\n    def f(self):\n        super().f()\n        x = 1\n", "        y = 2\n"),
    ],
)
def test_a_container_of_two_units_gets_no_blank_line_while_the_pair_rule_is_off(
    rule: str, source: str, third: str
) -> None:
    *head, last = source.splitlines(keepends=True)

    assert fix_with(source, rule) == source
    assert fix_with(source + third, rule) == "".join(head) + "\n" + last + third


@pytest.mark.parametrize("pair", [False, True])
def test_a_relationship_keeper_joins_a_container_of_two_units_while_the_pair_rule_is_off(pair: bool) -> None:
    source = "def f():\n    item = Item()\n\n    item.start()\n"
    rules = ["ZA3062", *(["ZA3054"] if pair else [])]

    assert [finding.code for finding in findings_with(source, *rules)] == ["ZA3054" if pair else "ZA3062"]
    assert fix_with(source, *rules) == "def f():\n    item = Item()\n    item.start()\n"


@pytest.mark.parametrize("multi_line", [False, True])
def test_a_multi_line_assertion_ends_a_guard_chain(multi_line: bool) -> None:
    second = "assert (\n        b is not None\n    )" if multi_line else "assert b is not None"
    source = f"def f(a, b):\n    assert a is not None\n\n    {second}\n    use(a, b)\n"
    expected = source if multi_line else source.replace("not None\n\n", "not None\n")

    assert fix_with(source, "ZA3057") == expected


@pytest.mark.parametrize("multi_rule", [False, True])
@pytest.mark.parametrize("checked", [False, True])
def test_a_multi_line_assertion_stays_apart_from_a_guard_chain_whatever_else_is_on(
    multi_rule: bool, checked: bool
) -> None:
    source = "def f(a, b):\n    assert a is not None\n\n    assert (\n        b is not None\n    )\n    use(a, b)\n"
    rules = ["ZA3057", *(["ZA3001"] if multi_rule else []), *(["ZA3012"] if checked else [])]
    expected = source.replace("    use(a, b)", "\n    use(a, b)") if multi_rule else source

    assert fix_with(source, *rules) == expected


@pytest.mark.parametrize("is_assertion", [False, True])
def test_a_multi_line_helper_ends_a_guard_chain_only_when_it_is_an_assertion(is_assertion: bool) -> None:
    source = "def f(a, b):\n    check(a)\n\n    check(\n        b,\n    )\n    use(a, b)\n"
    options = Options(guard_roots=("check",), assertion_roots=("check",) if is_assertion else ())
    expected = source if is_assertion else source.replace("    check(a)\n\n", "    check(a)\n")

    assert fix_with(source, "ZA3057", options=options) == expected


@pytest.mark.parametrize(("keeper", "body"), JUMP_KEEPERS)
def test_the_jump_rule_reads_the_joins_a_keeper_makes_in_the_same_pass(keeper: str, body: str) -> None:
    source = function_of(body)
    expected = source.replace("\n\n", "\n").replace("    return", "\n    return")
    config = Config.create("none", enable=[keeper, "ZA3013"], options=Options(receiver_roots=("db",)))
    findings = lint(source, config)

    assert {finding.code for finding in findings} == {keeper, "ZA3013"}
    assert apply_edits(SourceFile(source), findings) == expected

    result = fix(source, config)

    assert result.text == expected
    assert result.passes == 1


@pytest.mark.parametrize(("keeper", "body"), JUMP_KEEPERS)
def test_the_jump_rule_reads_no_join_while_the_keeper_is_off(keeper: str, body: str) -> None:
    source = function_of(body)
    config = Config.create("none", enable=["ZA3013"], options=Options(receiver_roots=("db",)))

    assert fix(source, config).text == source


TIGHT_SILENCED = {
    "ZA3057": "log()\nif not xs:\n    return\nif not values:{noqa}\n    return\nuse(xs, values)",
    "ZA3058": "log()\nvalue = get()\nif not value:{noqa}\n    return\nuse(value)",
    "ZA3063": "log()\ntotal = 0\nfor x in xs:{noqa}\n    total += x\nreturn total",
    "ZA3059": "log()\n\nresult = compute()\nreturn result{noqa}",
    "ZA3060": "log()\nvalue = get()\nassert value is not None{noqa}\nuse(value)",
    "ZA3062": "log()\n\nitem = Item()\nitem.start(){noqa}\nitem.save()\nfinish()",
    "ZA3064": "log()\ndb.first()\ndb.second(){noqa}\nfinish()",
    "ZA3061": "log()\ntext = read()\nlines = text.split(){noqa}\nuse(lines)",
}


@pytest.mark.parametrize("gap", ["blank", "tight"])
@pytest.mark.parametrize("keeper", SILENCED)
def test_a_noqa_on_a_keeper_writes_what_the_level_writes_with_the_keeper_off(keeper: str, gap: str) -> None:
    body = (SILENCED if gap == "blank" else TIGHT_SILENCED)[keeper]
    options = Options(receiver_roots=("db",))
    silenced = function_of(body.replace("{noqa}", f"  # noqa: {keeper}"))
    neutral = function_of(body.replace("{noqa}", "  # noqa: ZA0000"))

    for level in ("moderate", "strong"):
        keeping = Config.create(level, enable=[keeper], options=options)
        off = Config.create(level, options=options)

        assert fix(silenced, keeping).text.replace(keeper, "ZA0000") == fix(neutral, off).text

    if gap == "blank":
        assert keeper in {
            finding.code for finding in lint(neutral, Config.create("strong", enable=[keeper], options=options))
        }


def test_a_noqa_on_a_keeper_is_a_comment_that_keeps_the_blank_line_below_its_statement() -> None:
    source = function_of(
        "svc = Service(sid)\n\nsvc.start()  # noqa: ZA3062\n\nlog()\n\nprint(1)\n\naudit()\n\nnotify()", "sid"
    )

    expected = source.replace("()\n\n", "()\n").replace("(1)\n\n", "(1)\n")

    assert fix(source, Config.create("strong", enable=["ZA3062"])).text == expected


def test_a_noqa_on_a_guard_above_a_tight_binding_hands_the_gap_to_the_multi_line_rule() -> None:
    source = (
        "def f(cid):\n    audit(cid)\n    customer = service.get(cid)\n"
        "    if not customer:  # noqa: ZA3058\n        raise NotFound(cid)\n"
    )

    expected = source.replace("    if not", "\n    if not")

    assert fix(source, Config.create("strong", enable=["ZA3058"])).text == expected


@pytest.mark.parametrize("keeper", SILENCED)
def test_a_file_wide_noqa_on_a_keeper_is_the_keeper_turned_off(keeper: str) -> None:
    source = "# ruff: noqa: " + keeper + "\n" + function_of(SILENCED[keeper].replace("{noqa}", ""))
    options = Options(receiver_roots=("db",))

    for level in ("none", "strong"):
        keeping = Config.create(level, enable=[keeper, "ZA3059"], options=options)
        off = Config.create(level, enable=["ZA3059"], disable=[keeper], options=options)

        assert fix(source, keeping).text == fix(source, off).text


# A keeper that is no relationship rule, at a gap that another rule asks for once the keeper is off:
# the rule that takes the gap over, the text with the keeper's gap tight, and the text with it written by that rule.
HANDED_ON = {
    "ZA3202": (
        "ZA3201",
        "if a:\n    x()\nelse:{noqa}\n    y()\n",
        "if a:\n    x()\n\nelse:{noqa}\n    y()\n",
    ),
    "ZA3112": (
        "ZA3111",
        "match v:\n    case 1:\n        a()\n        b()\n    case 2:{noqa}\n        c()\n        d()\n",
        "match v:\n    case 1:\n        a()\n        b()\n\n    case 2:{noqa}\n        c()\n        d()\n",
    ),
    "ZA3054": (
        "ZA3060",
        "def f():\n    value = get()\n\n    assert value is not None{noqa}\n",
        "def f():\n    value = get()\n    assert value is not None{noqa}\n",
    ),
}


@pytest.mark.parametrize("keeper", HANDED_ON)
def test_a_noqa_on_a_pair_half_or_the_pair_hands_the_gap_to_the_next_rule(keeper: str) -> None:
    taker, source, written = HANDED_ON[keeper]
    silenced = source.replace("{noqa}", f"  # noqa: {keeper}")

    assert written != source
    assert fix_with(silenced, keeper, taker) == written.replace("{noqa}", f"  # noqa: {keeper}")


@pytest.mark.parametrize("keeper", HANDED_ON)
def test_a_file_wide_noqa_on_a_pair_half_or_the_pair_turns_it_off(keeper: str) -> None:
    taker, source, written = HANDED_ON[keeper]
    header = f"# ruff: noqa: {keeper}\n"

    assert fix_with(header + source.replace("{noqa}", ""), keeper, taker) == header + written.replace("{noqa}", "")


# A gap that only the keeper judges, blank, where the comment leaves it as written and the keeper alone would remove it.
BLANK_GAPS = {
    "ZA3203": "def f():\n\n    x = 1{noqa}\n    y = 2\n",
    "ZA3113": "match v:\n    case 1: a()\n\n    case 2: b(){noqa}\n",
    "ZA3112": "match v:\n    case 1:\n        a()\n        b()\n\n    case 2:{noqa}\n        c()\n        d()\n",
    "ZA3054": "def f():\n    x = 1\n\n    y = 2{noqa}\n",
    "ZA3202": "if a:\n    x()\n\nelse:{noqa}\n    y()\n",
}


@pytest.mark.parametrize("keeper", BLANK_GAPS)
def test_a_noqa_on_an_older_keeper_over_a_blank_gap_leaves_it_as_written(keeper: str) -> None:
    silenced = BLANK_GAPS[keeper].replace("{noqa}", f"  # noqa: {keeper}")
    neutral = BLANK_GAPS[keeper].replace("{noqa}", "  # noqa: ZA0000")

    assert fix_with(silenced, keeper) == silenced
    assert fix_with(neutral, keeper) != neutral


# A pair each keeper joins, of single-line statements so that each could read as a loose statement.
LOOSE_PAIRS = {
    "ZA3057": ("if not a: return", "if not text: return"),
    "ZA3058": ("v1 = find(text)", "if v1 is None: return None"),
    "ZA3063": ("total = 0", "for x in xs: total += x"),
    "ZA3060": ("v1 = find(text)", "assert v1 is not None"),
    "ZA3062": ("svc = Service(text)", "svc.start()"),
    "ZA3064": ("db.first()", "db.second()"),
    "ZA3061": ("v1 = find(text)", "v2 = use(v1)"),
}


@pytest.mark.parametrize(("above", "below"), [(0, 1), (1, 0), (1, 1), (3, 0), (0, 3), (1, 3), (3, 3)])
@pytest.mark.parametrize("keeper", LOOSE_PAIRS)
def test_a_kept_pair_among_loose_statements_is_no_loose_run_and_its_neighbors_stay_apart(
    keeper: str, above: int, below: int
) -> None:
    first, second = LOOSE_PAIRS[keeper]
    above_pair = ["audit()", "log.flush()", "print(1)"][:above]
    below_pair = ["log.write(1)", "print(2)", "notify()"][:below]

    source = function_of("\n\n".join([*above_pair, first, second, *below_pair]), "text, a, xs, db")
    config = Config.create("strong", enable=[keeper], options=Options(receiver_roots=("db",)))

    assert [finding.code for finding in lint(source, config)] == [keeper]
    assert fix(source, config).text == source.replace(f"{first}\n\n", f"{first}\n")


@pytest.mark.parametrize("assertion_rule", [False, True])
def test_a_binding_above_a_guard_chain_that_an_assert_opens_belongs_to_the_assertion_rule(assertion_rule: bool) -> None:
    source = function_of(
        "audit()\nv1 = find(text)\n\nassert v1 is not None\nif v1 is None: return None\ndb.write(v1)", "text"
    )

    rules = ["ZA3058", *(["ZA3060"] if assertion_rule else [])]

    assert [finding.code for finding in findings_with(source, *rules)] == (["ZA3060"] if assertion_rule else [])
    assert fix_with(source, *rules) == (source.replace("(text)\n\n", "(text)\n") if assertion_rule else source)


@pytest.mark.parametrize(
    ("code", "body", "enable", "written"),
    [
        (
            "ZA3014",
            "class A(B):\n    def __init__(self):\n        super().__init__()\n        self.a = 1\n"
            "        self.b = 2\n        log()\n",
            [],
            "        log()",
        ),
        (
            "ZA3002",
            'def f():\n    a: int = 1\n    b: int = 2\n    c: str = "x"\n    d: str = "y"\n    log()\n',
            ["ZA3003"],
            '    c: str = "x"',
        ),
    ],
)
def test_a_file_wide_noqa_on_a_rule_that_adds_a_blank_line_silences_its_findings_and_hands_no_gap_on(
    code: str, body: str, enable: list[str], written: str
) -> None:
    source = f"# ruff: noqa: {code}\n" + body
    expected = source.replace(written, "\n" + written)

    assert fix(source, Config.create("strong", enable=[code, *enable])).text == expected


def test_a_file_wide_noqa_on_the_chain_keeper_leaves_the_jump_keeper_its_binding() -> None:
    source = (
        "# ruff: noqa: ZA3061\ndef f():\n    log()\n\n    a = load()\n\n    result = compute(a)\n\n    return result\n"
    )

    assert fix_with(source, "ZA3059", "ZA3061") == source.replace("compute(a)\n\n", "compute(a)\n")


def test_seeds_apart_from_each_other_form_one_group_with_the_loop_that_builds_them() -> None:
    source = function_of(
        "total = 0\n\nnames = []\n\nfor x in xs:\n    total += x\n    names.append(x)\nuse(total, names)"
    )

    expected = function_of(
        "total = 0\nnames = []\nfor x in xs:\n    total += x\n    names.append(x)\nuse(total, names)"
    )

    assert fix_with(source, "ZA3063") == expected


@pytest.mark.parametrize(
    ("imports", "value", "seeds"),
    [
        ("import collections\n", "collections.Counter()", True),
        ("import decimal\n", "decimal.Decimal('0')", True),
        ("from decimal import Decimal\n", "Decimal.from_float(0.5)", True),
        ("", "collections.Counter()", False),
        ("", "source.copy()", False),
        ("", "self.make()", False),
        ("", "cls.make()", False),
        ("", "other.count", False),
    ],
)
def test_a_seed_may_call_through_a_root_the_file_imports_but_not_through_a_variable(
    imports: str, value: str, seeds: bool
) -> None:
    source = imports + function_of(f"items = {value}\n\nfor x in xs:\n    items.append(x)\nuse(items)", "source, xs")
    expected = source.replace(f"{value}\n\n", f"{value}\n") if seeds else source

    assert fix_with(source, "ZA3063") == expected
