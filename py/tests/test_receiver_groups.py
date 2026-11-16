"""Receiver grouping uses one written owner and preserves explicit ownership and sections."""

from __future__ import annotations

import pytest

from nickelony_stanza import Config, Options, fix, lint
from nickelony_stanza.analysis.source import SourceFile
from nickelony_stanza.config import ConfigError, config_from_table
from nickelony_stanza.engine import apply_edits
from nickelony_stanza.options import ReceiverComparison
from nickelony_stanza.rules.base import Level
from tests.helpers import fix_in_steps, fix_with, function_of


def receiver_options(comparison: ReceiverComparison = "root", roots: tuple[str, ...] = ("*",)) -> Options:
    return Options(receiver_roots=roots, receiver_comparison=comparison)


@pytest.mark.parametrize("comparison", ["root", "path"])
@pytest.mark.parametrize("invocations", [False, True])
@pytest.mark.parametrize("separator", [False, True])
@pytest.mark.parametrize("keeper", [False, True])
def test_root_and_path_runs_share_one_boundary_owner(
    comparison: ReceiverComparison, invocations: bool, separator: bool, keeper: bool
) -> None:
    source = "object.first()\nobject.second()\nobject.child.first()\nobject.child.second()\n"
    rules = [code for code, enabled in [("ZA3004", invocations), ("ZA3019", separator), ("ZA3063", keeper)] if enabled]

    expected = (
        source.replace("object.child.first()", "\nobject.child.first()")
        if comparison == "path" and (invocations or separator)
        else source
    )

    options = receiver_options(comparison)
    config = Config.create("none", enable=rules, options=options)
    result = fix(source, config)

    assert result.text == expected
    assert result.passes <= 1
    assert fix(result.text, config).passes == 0

    findings = lint(source, config)

    assert [finding.code for finding in findings] == (
        ["ZA3004" if invocations else "ZA3019"] if source != expected else []
    )


@pytest.mark.parametrize("comparison", ["root", "path"])
def test_mixed_writes_and_calls_use_the_owning_path(comparison: ReceiverComparison) -> None:
    source = "object.name = name\n\nobject.save()\nobject.child.name = child_name\n\nobject.child.save()\n"
    expected = source.replace(" = name\n\n", " = name\n").replace(" = child_name\n\n", " = child_name\n")

    if comparison == "path":
        expected = expected.replace("object.child.name", "\nobject.child.name")

    result = fix(source, Config.create("none", enable=["ZA3019", "ZA3063"], options=receiver_options(comparison)))

    assert result.text == expected
    assert result.passes == 1


def test_receiver_keeping_overrides_a_mixed_family_edge_and_transfers_it_above_the_group() -> None:
    source = "prepare()\nobject.a = 1\nobject.b = 2\nobject.save()\nfinish()\n"
    expected = source.replace("prepare()\n", "prepare()\n\n")

    assert fix_with(source, "ZA3008", "ZA3063", options=receiver_options("path")) == expected


def test_receiver_separator_terminates_ordinary_assignment_runs() -> None:
    source = "object.a = 1\nobject.b = 2\nobject.child.a = 1\nobject.child.b = 2\n"
    options = receiver_options("path")

    assert fix_with(source, "ZA3008", "ZA3054", options=options) == source

    config = Config.create("none", enable=["ZA3008", "ZA3019", "ZA3054"], options=options)
    result = fix(source, config)

    assert result.text == source.replace("object.child.a", "\nobject.child.a")
    assert result.passes == 1
    assert [finding.code for finding in lint(source, config)] == ["ZA3008"]


def test_singletons_alone_are_not_new_stanzas() -> None:
    source = "logger.info('start')\ndb.add(cart)\ncache.clear()\n"
    assert fix_with(source, "ZA3019", options=receiver_options()) == source

    source = "logger.info('start')\ndb.add(cart)\ndb.save()\ncache.clear()\n"
    expected = source.replace("db.add(cart)", "\ndb.add(cart)").replace("cache.clear()", "\ncache.clear()")

    assert fix_with(source, "ZA3019", "ZA3052", options=receiver_options()) == expected


@pytest.mark.parametrize(
    ("consumer", "owner"), [((), "ZA3006"), (("ZA3052",), "ZA3006"), (("ZA3054",), "ZA3006"), (("ZA3004",), "ZA3004")]
)
def test_selected_awaits_stay_one_separated_run_and_only_a_receiver_rule_takes_them_over(
    consumer: tuple[str, ...], owner: str
) -> None:
    source = "async def f():\n    prepare()\n    await db.flush()\n    await db.refresh()\n    done()\n"
    expected = "async def f():\n    prepare()\n\n    await db.flush()\n    await db.refresh()\n\n    done()\n"
    config = Config.create("none", enable=["ZA3006", *consumer], options=receiver_options(roots=("db",)))

    assert fix(source, config).text == expected
    assert [finding.code for finding in lint(source, config)] == [owner, owner]


@pytest.mark.parametrize(("receiver", "selected"), [("dbs[0]", False), ("db", True)])
def test_a_selected_call_and_await_share_one_receiver_run(receiver: str, selected: bool) -> None:
    owner = receiver.replace("[0]", "[1]")
    source = f"async def f():\n    prepare()\n    {receiver}.add(x)\n    await {owner}.flush()\n    done()\n"
    expected = source.replace("    prepare()\n", "    prepare()\n\n").replace("    done()", "\n    done()")

    assert fix_with(source, "ZA3004", "ZA3006", options=receiver_options()) == (expected if selected else source)


@pytest.mark.parametrize(
    "source",
    [
        "dbs[0].add()\n\ndbs[1].save()\n",
        "get_db().add()\n\nget_db().save()\n",
        "self.add()\n\nself.save()\n",
        "super().add()\n\nsuper().save()\n",
        "db.add(\n    x,\n)\n\ndb.save()\n",
        "db.add()\n\n# section\ndb.save()\n",
        "db.add()  # trailing\n\ndb.save()\n",
        "# fmt: off\ndb.add()\n\ndb.save()\n# fmt: on\n",
    ],
)
def test_unsupported_or_protected_operations_are_not_joined(source: str) -> None:
    assert fix_with(source, "ZA3063", options=receiver_options()) == source


@pytest.mark.parametrize(
    "receiver", ["db", "self.db", "cls.db", "self.self", "object.self", "_db", "self.__db", "(db)"]
)
def test_supported_direct_owners_keep_their_calls(receiver: str) -> None:
    source = f"{receiver}.add()\n\n{receiver}.save()\n"
    assert fix_with(source, "ZA3063", options=receiver_options()) == source.replace("\n\n", "\n")


@pytest.mark.parametrize(
    ("roots", "receiver", "selected"),
    [
        (("self.db",), "self.db", True),
        (("*.db",), "self.db", False),
        (("*.db",), "object.db", True),
        (("self",), "self.db", False),
        (("self",), "object.self", True),
        (("child",), "object.child", True),
        (("child",), "object", False),
        (("Db",), "db", False),
    ],
)
def test_selection_uses_normalized_segments_and_ordinary_dotted_globs(
    roots: tuple[str, ...], receiver: str, selected: bool
) -> None:
    source = f"{receiver}.add()\n\n{receiver}.save()\n"
    expected = source.replace("\n\n", "\n") if selected else source

    assert fix_with(source, "ZA3063", options=receiver_options(roots=roots)) == expected


@pytest.mark.parametrize(
    "statement",
    ["db.value = 1", "db.value += 1", "db.value: int = 1", "self.db.value = 1", "db.changed.connect(handler)"],
)
def test_member_writes_and_event_calls_can_share_a_root(statement: str) -> None:
    source = statement + "\n\ndb.save()\n"
    assert fix_with(source, "ZA3063", options=receiver_options()) == source.replace("\n\n", "\n")


@pytest.mark.parametrize(
    "statement",
    ["value = db.load()", "db.value: int", "self.value: int = 1", "left.value = right.value = 1", "values[0] = 1"],
)
def test_declarations_annotation_only_and_ambiguous_writes_are_not_absorbed(statement: str) -> None:
    source = statement + "\n\ndb.save()\n"
    assert fix_with(source, "ZA3063", options=receiver_options()) == source


def test_selective_receiver_keeping_leaves_logger_gaps_alone() -> None:
    source = "async def f():\n    logger.info('one')\n\n    logger.info('two')\n    db.add(cart)\n\n    await db.flush()\n    finish()\n"
    expected = source.replace("    db.add(cart)\n\n", "\n    db.add(cart)\n").replace("    finish()", "\n    finish()")

    assert fix_with(source, "ZA3004", "ZA3006", "ZA3063", options=receiver_options(roots=("db",))) == expected


@pytest.mark.parametrize(
    "source",
    [
        "db.a()\ndb.b()\n\ndb.c()\ndb.d()\n",
        "db.a()\ndb.b()\n\ndb.c()\n\ndb.d()\ndb.e()\n",
    ],
)
def test_authored_sections_are_preserved(source: str) -> None:
    assert fix_with(source, "ZA3063", options=receiver_options()) == source


def test_one_receiver_finding_removes_every_eligible_gap_of_its_component() -> None:
    source = "db.a()\n\ndb.b()\n\ndb.c()\ndb.d()\n"
    config = Config.create("none", enable=["ZA3063"], options=receiver_options())
    findings = lint(source, config)

    assert len(findings) == 1
    assert len(findings[0].edits) == 2

    expected = source.replace("\n\n", "\n")

    assert apply_edits(SourceFile(source), findings[:1]) == expected
    assert fix(source, config).text == expected


@pytest.mark.parametrize("reverse", [False, True])
def test_individual_receiver_fixes_and_batch_fixes_agree(reverse: bool) -> None:
    source = "db.a()\n\ndb.b()\n\ndb.c()\ndb.d()\ncache.a()\n\ncache.b()\n\ncache.c()\ncache.d()\n"
    config = Config.create("none", enable=["ZA3019", "ZA3063"], options=receiver_options("path"))
    expected = fix(source, config).text
    current = source

    for _ in range(10):
        findings = lint(current, config)

        if not findings:
            break

        current = apply_edits(SourceFile(current), [findings[-1] if reverse else findings[0]])

    assert current == expected
    assert not lint(current, config)


def test_a_noqa_hands_one_receiver_gap_to_the_run_keeper() -> None:
    source = "db.a()\n\ndb.b()\n\ndb.c()  # noqa: ZA3063\n"
    expected = source.replace("\n\n", "\n")

    assert fix_with(source, "ZA3063", "ZA3004", "ZA3054", options=receiver_options()) == expected


@pytest.mark.parametrize(
    ("table", "splits"),
    [({}, False), ({"receiver-comparison": "root"}, False), ({"receiver-comparison": "path"}, True)],
)
def test_the_comparison_key_compares_roots_unless_it_asks_for_paths(table: dict[str, str], splits: bool) -> None:
    source = "object.first()\nobject.second()\nobject.child.first()\nobject.child.second()\n"
    options = config_from_table({"receiver-roots": ["object"], **table}).options
    expected = source.replace("object.child.first()", "\nobject.child.first()") if splits else source

    assert fix_with(source, "ZA3019", options=options) == expected


def test_the_comparison_key_refuses_another_word() -> None:
    with pytest.raises(ConfigError, match="receiver-comparison"):
        config_from_table({"receiver-comparison": "invalid"})


def test_class_member_containers_are_outside_receiver_geometry() -> None:
    source = "class C:\n    db.a()\n\n    db.b()\n    db.child.a()\n    db.child.b()\n"
    assert fix_with(source, "ZA3019", "ZA3063", options=receiver_options("path")) == source


@pytest.mark.parametrize("rules", [("ZA3063",), ("ZA3004", "ZA3054")])
def test_guard_helpers_are_not_reclassified_by_wildcard_receiver_selection(rules: tuple[str, ...]) -> None:
    source = "guard.first(value)\n\nguard.second(value)\n"
    options = Options(receiver_roots=("*",), guard_roots=("guard",))

    assert fix_with(source, *rules, options=options) == source


@pytest.mark.parametrize("comparison", ["root", "path"])
@pytest.mark.parametrize("middle", ["# section\n", "# noqa: ZA3006\n", "# fmt: off\n# fmt: on\n", ""])
def test_ordinary_receiver_runs_respect_comment_and_directive_barriers_without_rule19(
    comparison: ReceiverComparison, middle: str
) -> None:
    source = "prepare()\ndb.add()\n" + middle + "db.save()\ndone()\n"
    expected = source if middle else source.replace("db.add()", "\ndb.add()").replace("done()", "\ndone()")
    config = Config.create("none", enable=["ZA3004", "ZA3054"], options=receiver_options(comparison, ("db",)))
    result = fix(source, config)

    assert result.text == expected
    assert result.passes <= 1


def test_ordinary_receiver_runs_respect_trailing_comments_without_rule19() -> None:
    source = "prepare()\ndb.add()  # boundary\ndb.save()\ndone()\n"
    assert fix_with(source, "ZA3004", options=receiver_options()) == source


def test_same_line_receiver_operations_do_not_form_a_run_across_a_joined_gap() -> None:
    source = "prepare()\ndb.first(); db.second()\ndone()\n"

    for rules in [("ZA3004",), ("ZA3019",), ("ZA3004", "ZA3019", "ZA3063")]:
        assert fix_with(source, *rules, options=receiver_options()) == source


def test_factory_receiver_evaluations_do_not_override_ordinary_await_grouping() -> None:
    source = "async def f():\n    prepare()\n    get_db().add(x)\n    await get_db().flush()\n    done()\n"

    assert fix_with(source, "ZA3004", "ZA3006", options=receiver_options()) == source
    assert fix_with(source, "ZA3004", "ZA3006") == source


def test_a_multiline_selected_call_cannot_invent_a_run_with_one_following_await() -> None:
    source = "async def f():\n    db.add(\n        x,\n    )\n\n    await db.flush()\n    other()\n"

    for root in ("db", "cache"):
        for multi in (False, True):
            config = Config.create(
                "moderate", disable=[] if multi else ["ZA3001"], options=receiver_options(roots=(root,))
            )

            assert fix(source, config).text == source


@pytest.mark.parametrize("comparison", ["root", "path"])
@pytest.mark.parametrize("separator", [False, True])
@pytest.mark.parametrize(
    "generic",
    [
        [],
        ["ZA3004"],
        ["ZA3004", "ZA3008", "ZA3052"],
        ["ZA3004", "ZA3008", "ZA3054"],
        ["ZA3004", "ZA3008", "ZA3052", "ZA3054"],
    ],
)
@pytest.mark.parametrize(
    "operations",
    [
        ("db.a()", "db.b()", "db.c()", "db.d()"),
        ("db.field = 1", "db.b()", "db.c()", "db.field = 2"),
        ("db.a.x = 1", "db.a.b()", "db.a.c()", "db.a.x = 2"),
    ],
)
def test_a_section_of_a_receiver_group_is_kept_from_every_other_rule(
    comparison: ReceiverComparison, separator: bool, generic: list[str], operations: tuple[str, ...]
) -> None:
    source = function_of("\n".join((*operations[:2], "", *operations[2:])))

    config = Config.create(
        "none",
        enable=["ZA3063", *generic, *(["ZA3019"] if separator else [])],
        options=Options(receiver_roots=("db",), receiver_comparison=comparison, max_loose_run_size=1),
    )

    assert fix(source, config).text == source
    assert lint(source, config) == []


@pytest.mark.parametrize("separator", [False, True])
@pytest.mark.parametrize("comparison", ["root", "path"])
@pytest.mark.parametrize("mixed", [False, True])
def test_a_section_of_a_receiver_group_adds_no_border_of_its_own(
    separator: bool, comparison: ReceiverComparison, mixed: bool
) -> None:
    operations = (
        ("db.field = 1", "db.b()", "db.c()", "db.field = 2") if mixed else ("db.a()", "db.b()", "db.c()", "db.d()")
    )

    source = function_of("prepare()\n" + "\n".join((*operations[:2], "", *operations[2:])) + "\nfinish()")

    config = Config.create(
        "none",
        enable=["ZA3063", "ZA3004", "ZA3008", "ZA3052", "ZA3054", *(["ZA3019"] if separator else [])],
        options=Options(receiver_roots=("db",), receiver_comparison=comparison, max_loose_run_size=1),
    )

    expected = (
        source.replace("prepare()\n", "prepare()\n\n").replace("    finish()", "\n    finish()")
        if separator or not mixed
        else source
    )

    result = fix(source, config)

    assert result.text == expected
    assert result.passes <= 1


@pytest.mark.parametrize("mode", ["first", "last", "batch"])
def test_receiver_group_fixes_keep_the_middle_section_in_any_order(mode: str) -> None:
    source = "db.a()\n\ndb.b()\ndb.c()\n\ndb.d()\ndb.e()\n\ndb.f()\n"
    expected = "db.a()\ndb.b()\ndb.c()\n\ndb.d()\ndb.e()\ndb.f()\n"

    config = Config.create(
        "none",
        enable=["ZA3063", "ZA3004", "ZA3054", "ZA3052"],
        options=Options(receiver_roots=("db",), max_loose_run_size=1),
    )

    current = fix_in_steps(source, config, mode)

    assert current == expected
    assert lint(current, config) == []


@pytest.mark.parametrize("keeper", [False, True])
def test_only_an_enabled_receiver_keeper_protects_a_section_of_a_run(keeper: bool) -> None:
    source = "db.field = 1\ndb.first()\n\ndb.second()\ndb.field = 2\n"
    rules = ["ZA3004", "ZA3054", *(["ZA3063"] if keeper else [])]
    config = Config.create("none", enable=rules, options=Options(receiver_roots=("db",)))

    assert fix(source, config).text == (
        source if keeper else "db.field = 1\n\ndb.first()\ndb.second()\n\ndb.field = 2\n"
    )


@pytest.mark.parametrize("level", ["none", "strong"])
@pytest.mark.parametrize(
    ("keeper", "group"),
    [
        ("ZA3063", "db.open()\n\ndb.count = 1\n\nawait db.flush()"),
        ("ZA3064", "text = await read()\n\nlines = text.split()\n\nwords = count(lines)"),
    ],
)
def test_a_kept_group_ends_the_loose_run_below_it(level: Level, keeper: str, group: str) -> None:
    source = "async " + function_of(group + "\n\nlog.write()\n\naudit()\n\ndb.close()", "db, log")
    expected = source.replace("open()\n\n", "open()\n").replace("= 1\n\n", "= 1\n")
    expected = expected.replace("read()\n\n", "read()\n").replace("split()\n\n", "split()\n")
    config = Config.create(level, enable=[keeper], options=receiver_options())

    assert fix(source, config).text == expected


@pytest.mark.parametrize("pattern", ["self.", "cls.", "self. "])
def test_a_pattern_that_is_blank_without_its_self_prefix_is_refused(pattern: str) -> None:
    with pytest.raises(ConfigError, match=r"receiver-roots.*not blank once a leading `self\.` or `cls\.` is dropped"):
        config_from_table({"receiver-roots": [pattern]})
