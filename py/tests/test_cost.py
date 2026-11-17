"""The cost of a run: each module is parsed, scanned and given a scope table once, and each container read once."""

from __future__ import annotations

import ast
import collections
from pathlib import Path
from typing import Any

import pytest

from nickelony_stanza import Config, Options, lint
from nickelony_stanza.analysis import binding, dependent_bindings, module_index, receivers, scopes, session, syntax
from nickelony_stanza.analysis.projects import Projects
from nickelony_stanza.rules import claims

MODULES = 120


def _module(number: int) -> str:
    """Writes a module with a class that calls its own method and a function, so a resolvable call needs the scan."""
    return (
        f"MARK = {number}\n\n\n"
        f"class Worker{number}:\n"
        "    def run(self):\n"
        "        return self.step(1, 2, 3, 4)\n\n"
        "    def step(self, a, b, c, d):\n"
        "        return helper(5, 6, 7, 8)\n\n\n"
        "def helper(a, b, c, d):\n"
        "    return a\n"
    )


@pytest.fixture
def project(tmp_path: Path) -> dict[Path, str]:
    """Writes the generated project and gives each module path with its text."""
    files = {tmp_path / f"m{number}.py": _module(number) for number in range(MODULES)}

    for path, text in files.items():
        path.write_bytes(text.encode("utf-8"))

    return files


class Counts:
    """What a run did: how often each text was parsed, and how many scope tables, scans and adopted files it took."""

    def __init__(self) -> None:
        self.parses: collections.Counter[str] = collections.Counter()
        self.tables = 0
        self.scans = 0
        self.adopts = 0


def _count(monkeypatch: pytest.MonkeyPatch) -> Counts:
    """Wraps the three functions that do the work of a module, which are the test seams of the cost."""
    counts = Counts()

    parse: Any = ast.parse

    build_scope_table = scopes.build_scope_table
    scan_module = getattr(module_index, "scan_module", None)

    def counting_parse(source: Any, *args: Any, **keywords: Any) -> Any:
        counts.parses[source] += 1
        return parse(source, *args, **keywords)

    def counting_table(*args: Any) -> Any:
        counts.tables += 1
        return build_scope_table(*args)

    def counting_scan(*args: Any) -> Any:
        counts.scans += 1
        assert scan_module is not None

        return scan_module(*args)

    adopt = module_index.SourceIndex.adopt

    def counting_adopt(*args: Any, **keywords: Any) -> Any:
        counts.adopts += 1
        return adopt(*args, **keywords)

    monkeypatch.setattr(module_index.SourceIndex, "adopt", counting_adopt)
    monkeypatch.setattr(ast, "parse", counting_parse)
    monkeypatch.setattr(module_index, "build_scope_table", counting_table)
    monkeypatch.setattr(binding, "build_scope_table", counting_table)

    # A seam that is absent counts no scan, so the test fails on the count and not on the missing name.
    monkeypatch.setattr(module_index, "scan_module", counting_scan, raising=False)

    return counts


def test_a_strong_run_parses_each_module_once_and_gives_it_one_scope_table(
    project: dict[Path, str], monkeypatch: pytest.MonkeyPatch
) -> None:
    counts = _count(monkeypatch)
    projects = Projects([next(iter(project)).parent])
    config = Config.create("none", enable=["ZA3501"])

    found = sum(len(lint(text, config, path=path, projects=projects)) for path, text in project.items())

    assert found == 2 * MODULES
    assert {counts.parses[text] for text in project.values()} == {1}
    assert counts.tables == MODULES
    assert counts.scans == MODULES
    assert counts.adopts == MODULES


def test_a_one_file_check_scans_every_module_once_and_builds_one_scope_table(
    project: dict[Path, str], monkeypatch: pytest.MonkeyPatch
) -> None:
    counts = _count(monkeypatch)
    path = next(iter(project))
    projects = Projects([path.parent])
    config = Config.create("none", enable=["ZA3501"])

    assert len(lint(project[path], config, path=path, projects=projects)) == 2
    assert {counts.parses[text] for text in project.values()} == {1}
    assert counts.tables == 1
    assert counts.scans == MODULES
    assert counts.adopts == 1


def test_a_blank_line_run_reads_each_container_once_for_each_relationship_cache(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    seen: dict[str, list[int]] = {"receivers": [], "bindings": [], "checked": []}

    def counted(name: str, read: Any) -> Any:
        def counting(container: Any, *args: Any) -> Any:
            seen[name].append(id(container))
            return read(container, *args)

        return counting

    monkeypatch.setattr(session, "find_receiver_groups", counted("receivers", receivers.find_receiver_groups))

    monkeypatch.setattr(
        session, "find_dependent_binding_groups", counted("bindings", dependent_bindings.find_dependent_binding_groups)
    )

    monkeypatch.setattr(claims, "read_checked_stanza_gaps", counted("checked", claims.read_checked_stanza_gaps))

    body = "".join(f"    a{i} = use(a{i - 1})\n\n    db.child{i // 2}.save()\n" for i in range(1, 100))
    source = f"async def f(a0):\n{body}    item = Item()\n\n    item.check()\n    return item\n"

    rules = ["ZA3004", "ZA3013", "ZA3012", "ZA3010", "ZA3055", "ZA3059", "ZA3062", "ZA3064", "ZA3061"]
    options = Options(receiver_roots=("db",), receiver_comparison="path", guard_roots=("check",))

    lint(source, Config.create("none", enable=rules, options=options))

    assert all(ids and len(ids) == len(set(ids)) for ids in seen.values()), seen


@pytest.mark.parametrize("depth", [0, 24])
def test_reading_the_values_of_an_expression_visits_each_node_at_most_twice(
    monkeypatch: pytest.MonkeyPatch, depth: int
) -> None:
    text = (
        "(lambda x: (lambda y=x: [y + z for z in values]))(), "
        "[(lambda y=x: y) for item in values], lambda: (lambda y=(local := 1): local), "
        "[1 for x.value in values]"
    )

    for _ in range(depth):
        text = f"lambda: (lambda argument=({text}): argument)"

    expression = ast.parse(text, mode="eval").body

    visits: collections.Counter[int] = collections.Counter()
    children = ast.iter_child_nodes

    def counted(node: ast.AST) -> list[ast.AST]:
        if isinstance(node, ast.expr):
            visits[id(node)] += 1

        return list(children(node))

    monkeypatch.setattr(ast, "iter_child_nodes", counted)

    assert syntax.read_value_names(expression) == {"values", "x"}
    assert max(visits.values()) <= 2
