"""Adjacent single-name bindings connected by eager initializer value reads."""

from __future__ import annotations

import ast
from dataclasses import dataclass

from nickelony_stanza.analysis.model import STATEMENT_KINDS, Container
from nickelony_stanza.analysis.sections import partition_sections
from nickelony_stanza.analysis.syntax import executing_nodes, read_name_binding


def read_simple_binding(node: ast.AST | None) -> tuple[str, frozenset[str]] | None:
    """Reads one initialized plain name and its eager value reads, without entering nested scopes."""
    binding = read_name_binding(node)

    if binding is None:
        return None

    name, value = binding
    names: set[str] = set()

    for current in executing_nodes(value):
        if isinstance(current, ast.NamedExpr):
            return None

        if isinstance(current, ast.Name) and isinstance(current.ctx, ast.Load):
            names.add(current.id)

    return name, frozenset(names)


@dataclass(frozen=True)
class DependentBindingGroups:
    """Inner joins and authored sections shared by keepers and ordinary run readers."""

    kept: frozenset[int]
    sections: frozenset[int]


def find_dependent_binding_groups(container: Container) -> DependentBindingGroups:
    """Reads each initializer once and preserves authored sections inside immediate binding chains."""
    if container.kind not in STATEMENT_KINDS:
        return DependentBindingGroups(frozenset(), frozenset())

    bindings = tuple(None if unit.is_multi_line else read_simple_binding(unit.node) for unit in container.units)

    joined = tuple(
        previous is not None
        and following is not None
        and previous[0] in following[1]
        and not gap.has_comment
        and not gap.is_joined
        and gap.restriction is None
        for previous, following, gap in zip(bindings, bindings[1:], container.gaps, strict=False)
    )

    kept, sections = partition_sections(joined, tuple(gap.is_blank for gap in container.gaps))

    return DependentBindingGroups(kept, sections)
