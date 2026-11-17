"""ZA3063: removes the blank line between the bindings that seed accumulators and the loop that builds them."""

from __future__ import annotations

import ast
from typing import cast

from nickelony_stanza.analysis.model import STATEMENT_KINDS, Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.analysis.syntax import bound_names, executing_nodes, read_name_binding, target_names
from nickelony_stanza.rules.base import Rule

Loop = (ast.For, ast.AsyncFor, ast.While)

# The methods of the built-in collections that add to them, so `items.append(x)` builds `items` but `client.send(x)`
# only uses `client`.
_BUILDERS = frozenset({"append", "appendleft", "add", "extend", "extendleft", "insert", "setdefault", "update"})


def _callee_names(value: ast.expr, types: frozenset[str]) -> set[int]:
    """Collects the names a call is made through: `set` in `set()`, or `collections` in `collections.Counter()`."""
    names: set[int] = set()

    for call in (node for node in ast.walk(value) if isinstance(node, ast.Call)):
        root = call.func

        while isinstance(root, ast.Attribute):
            root = root.value

        # A dotted callee names a module or class only through a name the file imports or defines as a class;
        # `self` or a parameter, as in `source.copy()`, is a variable.
        if isinstance(root, ast.Name) and (root is call.func or root.id in types):
            names.add(id(root))

    return names


def _seeded_name(node: ast.AST | None, types: frozenset[str]) -> str | None:
    """Reads the name a binding seeds: one plain name given a value that reads no variable, such as `[]` or `None`."""
    binding = read_name_binding(node)

    if binding is None:
        return None

    name, value = binding
    callees = _callee_names(value, types)
    reads_variable = any(isinstance(node, ast.Name) and id(node) not in callees for node in ast.walk(value))

    return None if reads_variable else name


def _built_names(loop: ast.For | ast.AsyncFor | ast.While) -> set[str]:
    """Reads the names the body of a loop assigns, augments or adds to through a collection method."""
    names: set[str] = set()

    for node in (node for statement in loop.body for node in executing_nodes(statement)):
        names |= bound_names(node)

        if isinstance(node, ast.For | ast.AsyncFor | ast.NamedExpr):
            names |= target_names(node.target)

        if isinstance(node, ast.Expr) and isinstance(node.value, ast.Call):
            method = node.value.func

            if isinstance(method, ast.Attribute) and method.attr in _BUILDERS and isinstance(method.value, ast.Name):
                names.add(method.value.id)

    return names


def _find_accumulator_gaps(container: Container, types: frozenset[str]) -> frozenset[int]:
    """
    Finds the gap above each loop that builds the seed right above it, and the gaps of the seeds above that one.

    A run of single-line seeds that the loop builds is one group with it, blank lines between them included,
    so no separation falls between the seeds.
    """
    units, gaps = container.units, container.gaps
    found: set[int] = set()

    for index in range(1, len(units)):
        loop = units[index].node

        if not isinstance(loop, Loop) or gaps[index - 1].has_comment:
            continue

        built = _built_names(loop)
        seed = index - 1

        while not units[seed].is_multi_line and _seeded_name(units[seed].node, types) in built:
            found.add(seed)

            upper = gaps[seed - 1] if seed > 0 else None

            if upper is None or upper.has_comment or upper.is_joined or upper.restriction is not None:
                break

            seed -= 1

    return frozenset(found)


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    if container.kind not in STATEMENT_KINDS:
        return False

    if "accumulator-gaps" not in context.memo:
        context.memo["accumulator-gaps"] = _find_accumulator_gaps(container, context.environment.type_names)

    return index in cast("frozenset[int]", context.memo["accumulator-gaps"])


RULE = Rule(
    code="ZA3063",
    name="keep-accumulator-with-loop",
    level="opt-in",
    message="Remove the blank line between this accumulator and the loop that builds it.",
    description="Remove the blank line between a binding that seeds a name and the loop below that builds it.",
    claim=_claim,
    role="keep",
    transfers_separation=True,
)
