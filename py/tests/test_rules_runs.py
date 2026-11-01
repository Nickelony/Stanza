"""One case for each behavior of the rules for typed runs, subscriptions, base calls, loose runs and kept runs."""

from __future__ import annotations

import pytest

from nickelony_stanza import Config, Options, fix
from tests.helpers import Case, case, check_case, findings_with, fix_with

CASES = [
    # ZA3003 separate-same-type-declaration-runs
    case(
        "types: runs of two annotations",
        "ZA3003",
        "a: int = 1\nb: int = 2\nc: str = ''\nd: str = ''\nrun()\n",
        "a: int = 1\nb: int = 2\n\nc: str = ''\nd: str = ''\n\nrun()\n",
    ),
    case("types: a lone declaration of each type", "ZA3003", "a: int = 1\nb: str = ''\nc: float = 0.0\n"),
    case(
        "types: bindings without an annotation are one type",
        "ZA3003",
        "a = 1\nb = 'x'\nc: int = 3\nd: int = 4\n",
        "a = 1\nb = 'x'\n\nc: int = 3\nd: int = 4\n",
    ),
    case(
        "types: spacing in the annotation does not matter",
        "ZA3003",
        "a: dict[str, int] = {}\nb: dict[str,int] = {}\nrun()\n",
        "a: dict[str, int] = {}\nb: dict[str,int] = {}\n\nrun()\n",
    ),
    case(
        "types: the run is separated from the statement below it",
        "ZA3003",
        "def f():\n    a: int = 1\n    b: int = 2\n    return a + b\n",
        "def f():\n    a: int = 1\n    b: int = 2\n\n    return a + b\n",
    ),
    case(
        "types: a run of unpacked names",
        "ZA3003",
        "a, b = 1, 2\nc = 3\nrun()\n",
        "a, b = 1, 2\nc = 3\n\nrun()\n",
    ),
    case(
        "types: a class body belongs to the member rule",
        "ZA3003",
        "class A:\n    a: int = 1\n    b: int = 2\n    c: str = ''\n    d: str = ''\n",
    ),
    case("types: an attribute assignment has no type", "ZA3003", "self.a = 1\nself.b = 2\nrun()\n"),
    case("types: global names have no type", "ZA3003", "def f():\n    global a\n    global b\n    a = 1\n"),
    # ZA3007 separate-subscription-runs
    case(
        "subscriptions: connect calls of different receivers",
        "ZA3007",
        "setup()\nsaved.connect(on_saved)\nclosed.connect(on_closed)\nstart()\n",
        "setup()\n\nsaved.connect(on_saved)\nclosed.connect(on_closed)\n\nstart()\n",
    ),
    case(
        "subscriptions: connecting and disconnecting are one run",
        "ZA3007",
        "saved.disconnect(old)\nsaved.connect(new)\nstart()\n",
        "saved.disconnect(old)\nsaved.connect(new)\n\nstart()\n",
    ),
    case("subscriptions: a lone subscription", "ZA3007", "setup()\nsaved.connect(on_saved)\nstart()\n"),
    case(
        "subscriptions: the roots are an option",
        "ZA3007",
        "setup()\nbus.listen(a)\nbus.listen(b)\nstart()\n",
        "setup()\n\nbus.listen(a)\nbus.listen(b)\n\nstart()\n",
        Options(subscription_roots=("listen",)),
    ),
    case("subscriptions: a call that no root matches", "ZA3007", "setup()\nbus.listen(a)\nbus.listen(b)\nstart()\n"),
    case(
        "subscriptions: an awaited subscription is a subscription",
        "ZA3007",
        "await setup()\nawait saved.subscribe(a)\nawait closed.subscribe(b)\nawait start()\n",
        "await setup()\n\nawait saved.subscribe(a)\nawait closed.subscribe(b)\n\nawait start()\n",
    ),
    case(
        "subscriptions: an assignment of a connection is no subscription",
        "ZA3007",
        "a = db.connect()\nb = db.connect()\nrun()\n",
    ),
    # ZA3010 separate-base-call
    case(
        "base call: the call that opens a body",
        "ZA3010",
        "class A(B):\n    def f(self):\n        super().f()\n        x = 1\n        y = 2\n",
        "class A(B):\n    def f(self):\n        super().f()\n\n        x = 1\n        y = 2\n",
    ),
    case(
        "base call: the call that closes a body",
        "ZA3010",
        "class A(B):\n    def f(self):\n        x = 1\n        y = 2\n        super().f()\n",
        "class A(B):\n    def f(self):\n        x = 1\n        y = 2\n\n        super().f()\n",
    ),
    case(
        "base call: an opening and a closing call",
        "ZA3010",
        "class A(B):\n    def f(self):\n        super().a()\n        x = 1\n        y = 2\n        super().b()\n",
        "class A(B):\n    def f(self):\n        super().a()\n\n        x = 1\n        y = 2\n\n        super().b()\n",
    ),
    case(
        "base call: two base calls in a row",
        "ZA3010",
        "class A(B):\n    def f(self):\n        super().a()\n        super().b()\n        x = 1\n",
        "class A(B):\n    def f(self):\n        super().a()\n\n        super().b()\n        x = 1\n",
    ),
    case(
        "base call: the opening call below the docstring and the guards",
        "ZA3010",
        'class A(B):\n    def f(self, a):\n        """Doc."""\n        if a is None:\n            return\n        super().f(a)\n        x = 1\n        y = 2\n',
        'class A(B):\n    def f(self, a):\n        """Doc."""\n        if a is None:\n            return\n        super().f(a)\n\n        x = 1\n        y = 2\n',
    ),
    case(
        "base call: an awaited base call",
        "ZA3010",
        "class A(B):\n    async def f(self):\n        await super().f()\n        x = 1\n        y = 2\n",
        "class A(B):\n    async def f(self):\n        await super().f()\n\n        x = 1\n        y = 2\n",
    ),
    case(
        "base call: a chain that starts at super",
        "ZA3010",
        "class A(B):\n    def f(self):\n        super().items.clear()\n        x = 1\n        y = 2\n",
        "class A(B):\n    def f(self):\n        super().items.clear()\n\n        x = 1\n        y = 2\n",
    ),
    case(
        "base call: a function docstring keeps the line below it",
        "ZA3010",
        'class A(B):\n    def f(self):\n        """Doc."""\n        super().f()\n        x = 1\n        y = 2\n',
        'class A(B):\n    def f(self):\n        """Doc."""\n        super().f()\n\n        x = 1\n        y = 2\n',
    ),
    case(
        "base call: a base call in the middle",
        "ZA3010",
        "class A(B):\n    def f(self):\n        x = 1\n        super().f()\n        y = 2\n        z = 3\n",
    ),
    case(
        "base call: a returned base call is a jump",
        "ZA3010",
        "class A(B):\n    def f(self):\n        x = 1\n        y = 2\n        return super().f()\n",
    ),
    case(
        "base call: a call that passes super as an argument",
        "ZA3010",
        "class A(B):\n    def f(self):\n        use(super())\n        x = 1\n        y = 2\n",
    ),
    case(
        "base call: an explicit parent call is no base call",
        "ZA3010",
        "class A(B):\n    def f(self):\n        B.f(self)\n        x = 1\n        y = 2\n",
    ),
    case(
        "base call: the body of a block is left alone",
        "ZA3010",
        "class A(B):\n    def f(self, a):\n        if a:\n            super().f()\n            x = 1\n            y = 2\n",
    ),
    case(
        "base call: a body of two statements belongs to the pair rule",
        "ZA3010",
        "class A(B):\n    def f(self):\n        super().f()\n        x = 1\n",
    ),
    # ZA3052 gather-loose-statements
    case(
        "loose statements: a run past the limit",
        "ZA3052",
        "a = 1\n\nb = f()\n\nc = g()\n\nd = h()\n",
        "a = 1\nb = f()\nc = g()\nd = h()\n",
    ),
    case("loose statements: a run at the limit", "ZA3052", "a = 1\n\nb = f()\n\nc = g()\n"),
    case(
        "loose statements: a smaller limit",
        "ZA3052",
        "a = 1\n\nb = f()\n\nc = g()\n",
        "a = 1\nb = f()\nc = g()\n",
        Options(max_loose_run_size=2),
    ),
    case(
        "loose statements: a limit of zero gathers nothing",
        "ZA3052",
        "a = 1\n\nb = 2\n\nc = 3\n\nd = 4\n",
        None,
        Options(max_loose_run_size=0),
    ),
    case(
        "loose statements: a statement that sits tight against its other neighbor stays out",
        "ZA3052",
        "a = 1\n\nb = f()\n\nc = g()\n\nd = h()\ne = i()\n",
        "a = 1\nb = f()\nc = g()\n\nd = h()\ne = i()\n",
        Options(max_loose_run_size=2),
    ),
    case("loose statements: a comment ends the run", "ZA3052", "a = 1\n\nb = f()\n\n# note\nc = g()\n\nd = h()\n"),
    case(
        "loose statements: a trailing comment ends the run",
        "ZA3052",
        "a = 1\n\nb = f()  # note\n\nc = g()\n\nd = h()\n\ne = i()\n",
        "a = 1\n\nb = f()  # note\n\nc = g()\nd = h()\ne = i()\n",
        Options(max_loose_run_size=2),
    ),
    case(
        "loose statements: a multi-line statement ends the run",
        "ZA3052",
        "a = 1\n\nb = f()\n\nc = g(\n    1\n)\n\nd = h()\n\ne = i()\n",
    ),
    case(
        "loose statements: a jump is no part of a run",
        "ZA3052",
        "def f():\n    a = 1\n\n    b = f()\n\n    c = g()\n\n    d = h()\n\n    return d\n",
        "def f():\n    a = 1\n    b = f()\n    c = g()\n    d = h()\n\n    return d\n",
    ),
    case("loose statements: imports are no statements", "ZA3052", "import a\n\nimport b\n\nimport c\n\nimport d\n"),
    case(
        "loose statements: a function body",
        "ZA3052",
        "def f():\n    a = 1\n\n    b = 2\n\n    c = 3\n\n    d = 4\n",
        "def f():\n    a = 1\n    b = 2\n    c = 3\n    d = 4\n",
    ),
    case(
        "loose statements: a class body belongs to the member rule",
        "ZA3052",
        "class A:\n    a = 1\n\n    b = 2\n\n    c = 3\n\n    d = 4\n",
    ),
    case(
        "loose statements: a run below a function",
        "ZA3052",
        "def f():\n    pass\n\n\na = 1\n\nb = 2\n\nc = 3\n\nd = 4\n",
        "def f():\n    pass\n\n\na = 1\nb = 2\nc = 3\nd = 4\n",
    ),
    # ZA3053 gather-loose-members
    case(
        "loose members: fields past the limit",
        "ZA3053",
        "class A:\n    a: int\n\n    b: int\n\n    c: int\n\n    d: int\n",
        "class A:\n    a: int\n    b: int\n    c: int\n    d: int\n",
    ),
    case("loose members: fields at the limit", "ZA3053", "class A:\n    a: int\n\n    b: int\n\n    c: int\n"),
    case(
        "loose members: methods are definitions",
        "ZA3053",
        "class A:\n    a: int\n\n    def f(self): ...\n\n    b: int\n\n    c: int\n\n    d: int\n\n    e: int\n",
        "class A:\n    a: int\n\n    def f(self): ...\n\n    b: int\n    c: int\n    d: int\n    e: int\n",
    ),
    case(
        "loose members: the class docstring keeps its blank line",
        "ZA3053",
        'class A:\n    """Doc."""\n\n    a = 1\n\n    b = 2\n\n    c = 3\n\n    d = 4\n',
        'class A:\n    """Doc."""\n\n    a = 1\n    b = 2\n    c = 3\n    d = 4\n',
    ),
    case(
        "loose members: statements of a function belong to the statement rule",
        "ZA3053",
        "def f():\n    a = 1\n\n    b = 2\n\n    c = 3\n\n    d = 4\n",
    ),
]

KEPT_RUNS = [
    # The grouping rule that reads the run is part of the configuration, and ZA3054 keeps the run it reads.
    Case(
        "kept statements: a run of one helper",
        ("ZA3004", "ZA3054"),
        "print(a)\n\nprint(b)\n\nprint(c)\nrun()\n",
        "print(a)\nprint(b)\nprint(c)\n\nrun()\n",
    ),
    Case(
        "kept statements: no grouping rule reads the run",
        ("ZA3054",),
        "print(a)\n\nprint(b)\n\nprint(c)\n",
    ),
    Case(
        "kept statements: another helper ends the run",
        ("ZA3004", "ZA3054"),
        "print(a)\n\nprint(b)\n\nlog(c)\n\nlog(d)\n",
        "print(a)\nprint(b)\n\nlog(c)\nlog(d)\n",
    ),
    Case(
        "kept statements: two declarations of different types stay apart while the type rule is enabled",
        ("ZA3002", "ZA3003", "ZA3054"),
        "a: int = 1\n\nb: int = 2\n\nc: str = ''\n\nd: str = ''\n",
        "a: int = 1\nb: int = 2\n\nc: str = ''\nd: str = ''\n",
    ),
    Case(
        "kept statements: declarations of different types are one run without the type rule",
        ("ZA3002", "ZA3054"),
        "a: int = 1\n\nb: str = ''\n\nc = 3\nrun()\n",
        "a: int = 1\nb: str = ''\nc = 3\n\nrun()\n",
    ),
    Case(
        "kept statements: a comment keeps the blank line",
        ("ZA3004", "ZA3054"),
        "print(a)\n\n# note\nprint(b)\n",
    ),
    Case(
        "kept statements: a trailing comment keeps the blank line",
        ("ZA3004", "ZA3054"),
        "print(a)  # note\n\nprint(b)\n\nprint(c)\n",
        "print(a)  # note\n\nprint(b)\nprint(c)\n",
    ),
    Case(
        "kept statements: a multi-line statement keeps the blank line",
        ("ZA3004", "ZA3054"),
        "print(a)\n\nprint(\n    b\n)\n\nprint(c)\n",
    ),
    Case(
        "kept statements: a blank line a separating rule asks for stays",
        ("ZA3004", "ZA3011", "ZA3005", "ZA3054"),
        "def f(a):\n    assert a\n\n    self.assertEqual(a, 1)\n\n    self.assertTrue(a)\n",
        "def f(a):\n    assert a\n\n    self.assertEqual(a, 1)\n    self.assertTrue(a)\n",
    ),
    Case(
        "kept statements: a container of two units belongs to the pair rule",
        ("ZA3004", "ZA3054"),
        "print(a)\n\nprint(b)\n",
    ),
    Case(
        "kept statements: awaited calls are one run",
        ("ZA3006", "ZA3054"),
        "await a()\n\nawait b()\n\nawait c()\n",
        "await a()\nawait b()\nawait c()\n",
    ),
    Case(
        "kept statements: subscriptions are one run",
        ("ZA3007", "ZA3054"),
        "a.connect(x)\n\nb.connect(y)\n\nc.connect(z)\n",
        "a.connect(x)\nb.connect(y)\nc.connect(z)\n",
    ),
    Case(
        "kept statements: two statements on one line are tight, so two such lines are two sections",
        ("ZA3004", "ZA3054"),
        "print(a); print(b)\n\nprint(c); print(d)\n",
    ),
    Case(
        "kept statements: a section of declarations stays beside the blank line a type rule keeps",
        ("ZA3002", "ZA3003", "ZA3054"),
        "a: int = 1\nb: int = 2\n\nc: int = 3\nd: int = 4\n\ne: str = ''\n",
    ),
    # ZA3055 keep-member-runs-together
    Case(
        "kept members: fields",
        ("ZA3013", "ZA3055"),
        "class A:\n    a: int\n\n    b: int\n\n    c = 1\n\n    d = 2\n",
        "class A:\n    a: int\n    b: int\n\n    c = 1\n    d = 2\n",
    ),
    Case(
        "kept members: no member rule reads the run",
        ("ZA3055",),
        "class A:\n    a: int\n\n    b: int\n\n    c: int\n",
    ),
    Case(
        "kept members: a method ends the run",
        ("ZA3013", "ZA3055"),
        "class A:\n    a: int\n\n    b: int\n\n    def f(self): ...\n\n    c: int\n\n    d: int\n",
        "class A:\n    a: int\n    b: int\n\n    def f(self): ...\n\n    c: int\n    d: int\n",
    ),
    Case(
        "kept members: two members on one line are tight, so two such lines are two sections",
        ("ZA3013", "ZA3055"),
        "class A:\n    a: int; b: int\n\n    c: int; d: int\n",
    ),
    Case(
        "kept members: statements of a function belong to the statement rule",
        ("ZA3013", "ZA3055"),
        "def f():\n    a = 1\n\n    b = 2\n\n    c = 3\n",
    ),
]

# The sections of a run by their sizes, the text the rule leaves as the sizes of its sections, and the blank lines it reports.
# A lone unit beside one section is absorbed, and a lone unit between two sections stays a section.
SECTIONS = [
    ("1|2|2", (1, 2, 2), (3, 2), 1),
    ("2|2|1", (2, 2, 1), (2, 3), 1),
    ("1|1|2|1|1", (1, 1, 2, 1, 1), (6,), 4),
    ("2|1|2", (2, 1, 2), (2, 1, 2), 0),
    ("2|2|2", (2, 2, 2), (2, 2, 2), 0),
]


def _lay_out(sizes: tuple[int, ...], unit: str, indent: str) -> str:
    """Writes the units one to a line, numbered from 1, with a blank line between sections."""
    numbers = iter(range(1, sum(sizes) + 1))
    sections = ["\n".join(indent + unit.format(next(numbers)) for _ in range(size)) for size in sizes]

    return "\n\n".join(sections) + "\n"


@pytest.mark.parametrize(("name", "sizes", "left", "reported"), SECTIONS, ids=[row[0] for row in SECTIONS])
def test_a_lone_statement_is_absorbed_unless_it_sits_between_two_sections(
    name: str, sizes: tuple[int, ...], left: tuple[int, ...], reported: int
) -> None:
    text = _lay_out(sizes, "print({})", "")

    assert fix_with(text, "ZA3004", "ZA3054") == _lay_out(left, "print({})", "")
    assert len(findings_with(text, "ZA3004", "ZA3054")) == reported


@pytest.mark.parametrize(("name", "sizes", "left", "reported"), SECTIONS, ids=[row[0] for row in SECTIONS])
def test_a_lone_member_is_absorbed_unless_it_sits_between_two_sections(
    name: str, sizes: tuple[int, ...], left: tuple[int, ...], reported: int
) -> None:
    text = "class A:\n" + _lay_out(sizes, "m{}: int", "    ")

    assert fix_with(text, "ZA3013", "ZA3055") == "class A:\n" + _lay_out(left, "m{}: int", "    ")
    assert len(findings_with(text, "ZA3013", "ZA3055")) == reported


@pytest.mark.parametrize("item", [*CASES, *KEPT_RUNS], ids=lambda item: item.id)
def test_rule_case(item: Case) -> None:
    check_case(item)


def test_the_subscription_rule_hands_its_calls_back_to_the_invocation_rule_while_it_is_off() -> None:
    text = "setup()\nsaved.connect(a)\nsaved.connect(b)\nstart()\n"
    expected = "setup()\n\nsaved.connect(a)\nsaved.connect(b)\n\nstart()\n"

    assert fix_with(text, "ZA3004") == expected
    assert fix_with(text, "ZA3007") == expected
    assert fix_with(text, "ZA3004", "ZA3007") == expected


def test_the_subscription_rule_joins_calls_the_invocation_rule_keeps_apart() -> None:
    text = "saved.connect(a)\nclosed.connect(b)\nstart()\n"

    assert fix_with(text, "ZA3004") == text
    assert fix_with(text, "ZA3007") == "saved.connect(a)\nclosed.connect(b)\n\nstart()\n"


def test_a_gather_rule_never_removes_a_blank_line_a_separating_rule_writes_back() -> None:
    text = "a = 1\n\nb = 2\n\nc = foo(\n    1\n)\n\nd = 4\n\ne = 5\n\nf = 6\n"
    config = Config.create("none", enable=["ZA3001", "ZA3052"], options=Options(max_loose_run_size=2))
    expected = "a = 1\n\nb = 2\n\nc = foo(\n    1\n)\n\nd = 4\ne = 5\nf = 6\n"

    assert fix(text, config).text == expected
    assert fix(expected, config).text == expected


def test_a_gather_rule_keeps_the_blank_line_above_a_jump_where_the_jump_rule_is_enabled() -> None:
    text = "def f():\n    a = 1\n\n    b = 2\n\n    c = 3\n\n    d = 4\n\n    return d\n"
    config = Config.create("none", enable=["ZA3009", "ZA3052"])
    expected = "def f():\n    a = 1\n    b = 2\n    c = 3\n    d = 4\n\n    return d\n"

    assert fix(text, config).text == expected
    assert fix(expected, config).text == expected


def test_a_gather_rule_and_a_kept_run_rule_remove_the_same_blank_line_once() -> None:
    text = "print(a)\n\nprint(b)\n\nprint(c)\n\nprint(d)\n"
    config = Config.create("none", enable=["ZA3004", "ZA3052", "ZA3054"])

    assert fix(text, config).text == "print(a)\nprint(b)\nprint(c)\nprint(d)\n"
