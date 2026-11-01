"""Tells which run a statement belongs to, so neighbors of one run stay together."""

from __future__ import annotations

import ast
from dataclasses import dataclass
from typing import Literal

from nickelony_stanza.analysis.model import Container
from nickelony_stanza.analysis.syntax import callee_names, callee_path, matches_any
from nickelony_stanza.options import Options

Family = Literal[
    "declaration", "typed-declaration", "assignment", "invocation", "assertion", "await", "subscription", "member"
]

_TypeAlias = getattr(ast, "TypeAlias", None)


@dataclass(frozen=True)
class RunKey:
    """What a unit is for a run: neighbors with the same identity are one run."""

    family: Family
    identity: str


@dataclass(frozen=True)
class Environment:
    """What the rules agree on when they read a statement."""

    options: Options

    # The run families whose rule is enabled, because a statement that no enabled rule groups reads as a plainer family.
    families: frozenset[Family]


def _key(family: Family, detail: str | None = None) -> RunKey:
    return RunKey(family, f"{family}:{family if detail is None else detail}")


def _binds_only_names(target: ast.expr) -> bool:
    if isinstance(target, ast.Name):
        return True

    if isinstance(target, ast.Tuple | ast.List):
        return all(_binds_only_names(element) for element in target.elts)

    return isinstance(target, ast.Starred) and _binds_only_names(target.value)


def _classify_call(statement: ast.Expr, environment: Environment) -> RunKey | None:
    value = statement.value
    awaited = isinstance(value, ast.Await)
    call = value.value if isinstance(value, ast.Await) else value

    if not isinstance(call, ast.Call):
        return _key("await") if awaited and "await" in environment.families else None

    path = callee_path(call.func)
    names = callee_names(call.func)

    if path is not None and matches_any(environment.options.assertion_roots, path, names):
        return _key("assertion")

    if path is not None and "subscription" in environment.families:
        roots = environment.options.subscription_roots

        if matches_any(roots, path, names):
            return _key("subscription")

    if awaited and "await" in environment.families:
        return _key("await")

    return None if path is None else _key("invocation", path)


def _classify_with(statement: ast.With | ast.AsyncWith, environment: Environment) -> RunKey | None:
    """Reads a `with` block whose items are all assertion contexts, such as `pytest.raises(...)`."""
    for item in statement.items:
        call = item.context_expr

        if not isinstance(call, ast.Call):
            return None

        path = callee_path(call.func)

        if path is None or not matches_any(environment.options.assertion_roots, path, callee_names(call.func)):
            return None

    return _key("assertion")


def classify_statement(node: ast.AST, environment: Environment) -> RunKey | None:
    """Tells which run a statement belongs to, or None when it belongs to none."""
    if isinstance(node, ast.AnnAssign):
        return _key("declaration", "binding") if isinstance(node.target, ast.Name) else _key("assignment")

    if isinstance(node, ast.Assign):
        pure = all(_binds_only_names(target) for target in node.targets)
        return _key("declaration", "binding") if pure else _key("assignment")

    if isinstance(node, ast.AugAssign):
        return _key("assignment")

    if isinstance(node, ast.Global):
        return _key("declaration", "global")

    if isinstance(node, ast.Nonlocal):
        return _key("declaration", "nonlocal")

    if _TypeAlias is not None and isinstance(node, _TypeAlias):
        return _key("declaration", "type")

    if isinstance(node, ast.Assert):
        return _key("assertion")

    if isinstance(node, ast.With | ast.AsyncWith):
        return _classify_with(node, environment)

    if isinstance(node, ast.Expr):
        return _classify_call(node, environment)

    return None


def classify_typed(node: ast.AST) -> RunKey | None:
    """Tells which type a declaration is written with: its annotation, or none for a binding that infers it."""
    if isinstance(node, ast.AnnAssign) and isinstance(node.target, ast.Name):
        return RunKey("typed-declaration", f"typed-declaration:type:{ast.unparse(node.annotation)}")

    if isinstance(node, ast.Assign) and all(_binds_only_names(target) for target in node.targets):
        return RunKey("typed-declaration", "typed-declaration:inferred")

    return None


def classify_member(node: ast.AST) -> RunKey | None:
    """Tells which kind of member a class body statement is."""
    if isinstance(node, ast.AnnAssign):
        return _key("member", "field")

    if isinstance(node, ast.Assign | ast.AugAssign):
        return _key("member", "attribute")

    return None


@dataclass(frozen=True)
class Runs:
    """The runs of a container: neighbors that share a key."""

    keys: tuple[RunKey | None, ...]
    starts: tuple[int, ...]
    ends: tuple[int, ...]


def find_runs(container: Container, environment: Environment, *, typed: bool = False) -> Runs:
    """Splits the units of a container into runs of neighbors that share a key, or of one declared type with `typed`."""
    classify = classify_member if container.kind == "class" else lambda node: classify_statement(node, environment)

    if typed:
        classify = classify_typed

    keys = tuple(None if unit.node is None else classify(unit.node) for unit in container.units)
    starts: list[int] = []

    for index, key in enumerate(keys):
        continues = key is not None and index > 0 and keys[index - 1] is not None and keys[index - 1] == key
        starts.append(starts[index - 1] if continues else index)

    ends = [0] * len(keys)

    for index in range(len(keys) - 1, -1, -1):
        continues = index + 1 < len(keys) and starts[index + 1] == starts[index]
        ends[index] = ends[index + 1] if continues else index

    return Runs(keys, tuple(starts), tuple(ends))


def read_boundary_families(runs: Runs, gap_index: int) -> list[Family]:
    """Reads the families of the runs of two or more units that end or begin at a gap."""
    before = runs.keys[gap_index]
    after = runs.keys[gap_index + 1]

    if before is not None and after is not None and before == after:
        return []

    families: list[Family] = []

    if before is not None and runs.starts[gap_index] < gap_index:
        families.append(before.family)

    if after is not None and runs.ends[gap_index + 1] > gap_index + 1:
        families.append(after.family)

    return families
