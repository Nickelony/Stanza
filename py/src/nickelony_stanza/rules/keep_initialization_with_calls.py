"""ZA3062: keeps an initialization with the single-line receiver call directly below it."""

from __future__ import annotations

import ast
from typing import cast

from nickelony_stanza.analysis.model import STATEMENT_KINDS, Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.analysis.syntax import read_name_binding
from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import is_stanza_anchor_stable


def _is_candidate(container: Container, index: int, context: ClaimContext) -> bool:
    previous, following = container.units[index], container.units[index + 1]

    if container.kind not in STATEMENT_KINDS or container.gaps[index].has_comment:
        return False

    if previous.is_multi_line or following.is_multi_line:
        return False

    if index > 0:
        upper = container.gaps[index - 1]
        opens_stanza = upper.is_blank or context.is_claimed_by_others(index - 1)

        if not opens_stanza or upper.has_comment or upper.is_joined or upper.restriction is not None:
            return False

    binding = read_name_binding(previous.node)

    if binding is None:
        return False

    name, value = binding

    if not isinstance(value.value if isinstance(value, ast.Await) else value, ast.Call):
        return False

    if not isinstance(following.node, ast.Expr):
        return False

    call = following.node.value

    if isinstance(call, ast.Await):
        call = call.value

    return (
        isinstance(call, ast.Call)
        and isinstance(call.func, ast.Attribute)
        and isinstance(call.func.value, ast.Name)
        and call.func.value.id == name
    )


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    if "initialization-gaps" not in context.memo:
        context.memo["initialization-gaps"] = frozenset(
            gap_index for gap_index in range(len(container.gaps)) if _is_candidate(container, gap_index, context)
        )

    candidates = cast("frozenset[int]", context.memo["initialization-gaps"])

    if index not in candidates:
        return False

    return is_stanza_anchor_stable(container, index, context, candidates, "initialization-upper-runs")


RULE = Rule(
    code="ZA3062",
    name="keep-initialization-with-calls",
    level="opt-in",
    message="Remove the blank line between this initialization and its receiver call.",
    description="Keep a standalone initialization with the single-line receiver call directly below it.",
    claim=_claim,
    role="keep",
    reads_others=True,
    transfers_separation=True,
)
