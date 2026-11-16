"""Direct receiver identities and the consecutive groups selected by them."""

from __future__ import annotations

import ast
from dataclasses import dataclass

from nickelony_stanza.analysis.model import STATEMENT_KINDS, Container
from nickelony_stanza.analysis.sections import partition_sections
from nickelony_stanza.analysis.syntax import matches_any
from nickelony_stanza.options import Options

RECEIVER_CONSUMERS = frozenset({"ZA3004", "ZA3019", "ZA3063"})


def _direct_path(node: ast.expr) -> list[str]:
    names: list[str] = []

    while isinstance(node, ast.Attribute):
        names.append(node.attr)
        node = node.value

    if not isinstance(node, ast.Name):
        return []

    names.append(node.id)
    names.reverse()

    return names


def normalize_receiver_pattern(pattern: str) -> str:
    """Drops a leading `self.` or `cls.` from a `receiver-roots` pattern, since an owner is read without it."""
    return pattern.split(".", 1)[1] if pattern.startswith(("self.", "cls.")) else pattern


def _matches_receiver(names: list[str], options: Options) -> bool:
    patterns = tuple(normalize_receiver_pattern(pattern) for pattern in options.receiver_roots)
    return matches_any(patterns, ".".join(names), names)


def read_receiver_key(node: ast.AST | None, options: Options, *, writes: bool = False) -> str | None:
    """Reads the selected owner of a single-line direct operation, excluding the method or written member."""
    if not options.receiver_roots or not isinstance(node, ast.stmt) or node.end_lineno != node.lineno:
        return None

    names: list[str] = []

    if isinstance(node, ast.Expr):
        call = node.value.value if isinstance(node.value, ast.Await) else node.value

        if not isinstance(call, ast.Call) or not isinstance(call.func, ast.Attribute):
            return None

        call_names = _direct_path(call.func)
        path = ".".join(call_names)

        if matches_any(options.assertion_roots, path, call_names) or matches_any(options.guard_roots, path, call_names):
            return None

        names = call_names[:-1]
    elif writes:
        target: ast.expr | None = None

        if isinstance(node, ast.Assign) and len(node.targets) == 1:
            target = node.targets[0]
        elif isinstance(node, ast.AnnAssign | ast.AugAssign) and node.value is not None:
            target = node.target

        if isinstance(target, ast.Attribute):
            names = _direct_path(target.value)

    if names and names[0] in {"self", "cls"}:
        names = names[1:]

    if not names or not _matches_receiver(names, options):
        return None

    return names[0] if options.receiver_comparison == "root" else ".".join(names)


@dataclass(frozen=True)
class ReceiverGroups:
    """The outer borders of the groups of selected direct operations, their removable inner gaps and their sections."""

    borders: frozenset[int]
    kept: frozenset[int]
    sections: frozenset[int]


def find_receiver_groups(container: Container, options: Options) -> ReceiverGroups:
    """Reads receiver groups once, leaving multi-line operations, comments and protected gaps outside groups."""
    keys = tuple(
        read_receiver_key(unit.node, options, writes=True)
        if container.kind in STATEMENT_KINDS and not unit.is_multi_line
        else None
        for unit in container.units
    )

    # A gap joins two neighbors of one group; a group of one unit has no joined gap beside it.
    joined = tuple(
        keys[index] is not None
        and keys[index] == keys[index + 1]
        and not gap.has_comment
        and not gap.is_joined
        and gap.restriction is None
        for index, gap in enumerate(container.gaps)
    )

    def joins(index: int) -> bool:
        return 0 <= index < len(joined) and joined[index]

    kept, sections = partition_sections(joined, tuple(gap.is_blank for gap in container.gaps))

    borders = frozenset(
        index
        for index, gap in enumerate(container.gaps)
        if not joined[index]
        and not gap.is_joined
        and gap.restriction is None
        and (joins(index - 1) or joins(index + 1))
    )

    return ReceiverGroups(borders, kept, sections)
