"""Finds the gap between the two single-line units of a container, which only a rule that removes a line judges."""

from __future__ import annotations

import ast

from nickelony_stanza.analysis.model import UNIT_KINDS, Container
from nickelony_stanza.analysis.syntax import is_docstring_statement, is_import


def _count_docstring(container: Container) -> int:
    """Counts the docstring of a function that holds two statements below it, which is documentation and no step."""
    first = container.units[0]

    if container.kind == "function" and len(container.units) == 3 and is_docstring_statement(first.node):
        return 1

    return 0


def find_two_unit_gap(container: Container) -> int | None:
    """Reads the index of the gap between two single-line statements with nothing between them but whitespace."""
    if container.kind not in UNIT_KINDS:
        return None

    index = _count_docstring(container)

    if len(container.units) - index != 2:
        return None

    first, second = container.units[index : index + 2]
    gap = container.gaps[index]

    if first.is_multi_line or second.is_multi_line or gap.has_comment or gap.is_joined:
        return None

    if gap.restriction not in (None, "function-docstring"):
        return None

    pair = (first, second)

    return index if all(isinstance(unit.node, ast.stmt) and not is_import(unit.node) for unit in pair) else None
