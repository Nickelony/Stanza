"""Opt-in initialization, receiver and unpacking groups."""

from __future__ import annotations

import pytest

from nickelony_stanza import Config, Options, fix
from nickelony_stanza.config import ConfigError, config_from_table
from tests.helpers import fix_with


def test_a_standalone_initialization_keeps_its_first_receiver_call() -> None:
    source = "def f():\n    log()\n\n    item = Item()\n\n    item.first()\n    item.second()\n"
    expected = source.replace("    item = Item()\n\n", "    item = Item()\n")

    assert fix_with(source, "ZA3062") == expected
    assert fix_with(expected, "ZA3062") == expected


@pytest.mark.parametrize("prefix", ["    log()\n", ""])
def test_initializations_respect_their_upper_stanza_boundary(prefix: str) -> None:
    source = "def f():\n" + prefix + "    item = Item()\n\n    item.first()\n    finish()\n"
    expected = source if prefix else source.replace("    item = Item()\n\n", "    item = Item()\n")

    assert fix_with(source, "ZA3062") == expected


@pytest.mark.parametrize(
    "call",
    ["other.first()", "item.child.first()", "item.first(\n        1,\n    )", "# note\n    item.first()"],
)
def test_unrelated_or_multiline_calls_keep_the_gap(call: str) -> None:
    source = "def f():\n    log()\n\n    item = Item()\n\n    " + call + "\n    finish()\n"
    assert fix_with(source, "ZA3062") == source


def test_receiver_roots_join_sync_and_awaited_calls_in_one_run() -> None:
    source = "async def f(self, cart):\n    log()\n    self.db.add(cart)\n\n    await self.db.flush()\n    await self.db.refresh(cart)\n    finish()\n"

    expected = source.replace("    self.db.add(cart)\n\n", "\n    self.db.add(cart)\n").replace(
        "    finish()", "\n    finish()"
    )

    assert fix_with(source, "ZA3004", "ZA3006", "ZA3055", options=Options(receiver_roots=("self.db",))) == expected


def test_receiver_roots_preserve_different_meaningful_roots() -> None:
    source = "async def f():\n    self.db.add(x)\n    await self.cache.flush()\n    await self.cache.refresh(x)\n    finish()\n"

    expected = source.replace("    await self.cache.flush()", "\n    await self.cache.flush()").replace(
        "    finish()", "\n    finish()"
    )

    assert fix_with(source, "ZA3004", "ZA3006", options=Options(receiver_roots=("*",))) == expected


@pytest.mark.parametrize("rule", ["ZA3002", "ZA3003"])
def test_unpacking_declarations_can_form_a_separate_run(rule: str) -> None:
    source = "first, set_first = load()\nsecond, set_second = load()\ncount = 12\nname = 'test'\n"
    expected = source.replace("count = 12", "\ncount = 12")

    assert fix_with(source, rule, "ZA3055", options=Options(declaration_shapes="split")) == expected
    assert fix_with(source, rule, "ZA3055") == source


def test_grouping_options_are_loaded_from_configuration() -> None:
    options = config_from_table({"receiver-roots": ["*.db"], "declaration-shapes": "split"}).options

    assert options.receiver_roots == ("*.db",)
    assert options.declaration_shapes == "split"

    with pytest.raises(ConfigError):
        config_from_table({"declaration-shapes": "invalid"})


def test_an_awaited_factory_initialization_keeps_its_call() -> None:
    source = "async def f():\n    log()\n\n    item = await make_item()\n\n    await item.start()\n    finish()\n"
    expected = source.replace("    item = await make_item()\n\n", "    item = await make_item()\n")

    assert fix_with(source, "ZA3062") == expected


def test_an_upper_comment_keeps_the_initialization_gap() -> None:
    source = "def f():\n    log()\n\n    # item\n    item = Item()\n\n    item.start()\n    finish()\n"
    assert fix_with(source, "ZA3062") == source


def test_a_kept_declaration_run_can_remove_the_initialization_anchor() -> None:
    source = "def f():\n    count = 1\n\n    item = Item()\n\n    item.start()\n    finish()\n"
    expected = source.replace("    count = 1\n\n", "    count = 1\n")

    assert fix_with(source, "ZA3062", "ZA3002", "ZA3055") == expected
    assert fix_with(expected, "ZA3062", "ZA3002", "ZA3055") == expected


@pytest.mark.parametrize("roots", [("*",), ("self.db",), ("db",)])
def test_receiver_grouping_uses_the_first_root_after_self(roots: tuple[str, ...]) -> None:
    source = "async def f(self):\n    log()\n    self.db.items.add(x)\n\n    await self.db.flush()\n    finish()\n"

    expected = source.replace("    self.db.items.add(x)\n\n", "\n    self.db.items.add(x)\n").replace(
        "    finish()", "\n    finish()"
    )

    assert fix_with(source, "ZA3004", "ZA3006", "ZA3055", options=Options(receiver_roots=roots)) == expected


def test_direct_self_calls_have_no_concrete_receiver_root() -> None:
    source = "async def f(self):\n    self.add(x)\n    await self.flush()\n    await self.refresh(x)\n    finish()\n"

    expected = source.replace("    await self.flush()", "\n    await self.flush()").replace(
        "    finish()", "\n    finish()"
    )

    assert fix_with(source, "ZA3004", "ZA3006", options=Options(receiver_roots=("*",))) == expected


def test_an_initialization_whose_displaced_border_holds_its_anchor_joins_in_one_pass() -> None:
    source = "def f():\n    a = 1\n\n    b = 2\n\n    item = Item()\n\n    item.start()\n    if x:\n        use(x)\n"
    config = Config.create("none", enable=["ZA3001", "ZA3002", "ZA3052", "ZA3062"])
    expected = source.replace("Item()\n\n", "Item()\n").replace("    if x:", "\n    if x:")
    result = fix(source, config)

    assert result.text == expected
    assert result.passes == 1
    assert fix(result.text, config).passes == 0


def test_a_planned_upper_border_can_start_an_initialization_stanza() -> None:
    source = "def f():\n    log()\n    if x:\n        use(x)\n    item = Item()\n\n    item.start()\n    finish()\n"
    config = Config.create("none", enable=["ZA3001", "ZA3062"])
    expected = "def f():\n    log()\n\n    if x:\n        use(x)\n\n    item = Item()\n    item.start()\n    finish()\n"
    result = fix(source, config)

    assert result.text == expected
    assert result.passes == 1
    assert fix(result.text, config).passes == 0


@pytest.mark.parametrize("silenced", [False, True])
def test_a_noqa_leaves_an_initialization_apart_from_its_call(silenced: bool) -> None:
    comment = "  # noqa: ZA3062" if silenced else ""
    source = f"def f():\n    log()\n\n    item = Item()\n\n    item.start(){comment}\n    finish()\n"

    assert fix_with(source, "ZA3062") == (source if silenced else source.replace("Item()\n\n", "Item()\n"))


@pytest.mark.parametrize("receiver_keeper", [False, True])
def test_an_initialization_joins_its_call_in_the_pass_that_gathers_nothing_above_it(receiver_keeper: bool) -> None:
    source = "def f(sid):\n    audit()\n\n    svc = Service(sid)\n\n    svc.start()\n\n    svc.run()\n"
    rules = ["ZA3062", *(["ZA3064"] if receiver_keeper else [])]
    config = Config.create("strong", enable=rules, options=Options(receiver_roots=("*",)))
    expected = source.replace("Service(sid)\n\n", "Service(sid)\n")

    if receiver_keeper:
        expected = expected.replace("start()\n\n", "start()\n")

    result = fix(source, config)

    assert result.text == expected
    assert result.passes == 1
