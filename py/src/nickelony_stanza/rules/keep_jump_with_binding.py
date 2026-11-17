"""ZA3059: removes the blank line between a lone binding and the jump that uses what it binds."""

from __future__ import annotations

import ast
from typing import cast

from nickelony_stanza.analysis.model import STATEMENT_KINDS, Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.analysis.syntax import bound_names, read_value_names
from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import is_stanza_anchor_stable


def _jump_value(node: ast.AST | None) -> list[ast.expr]:
    if isinstance(node, ast.Return):
        return [] if node.value is None else [node.value]

    if isinstance(node, ast.Raise):
        return [value for value in (node.exc, node.cause) if value is not None]

    return []


def _is_candidate(container: Container, index: int, context: ClaimContext) -> bool:
    previous, following = container.units[index], container.units[index + 1]

    if container.kind not in STATEMENT_KINDS or container.gaps[index].has_comment:
        return False

    if previous.is_multi_line or following.is_multi_line:
        return False

    # A native planned boundary can open the lone binding stanza in the same analysis pass.
    if index > 0 and not (container.gaps[index - 1].is_blank or context.is_claimed_by_others(index - 1)):
        return False

    bound = bound_names(previous.node)

    return any(not bound.isdisjoint(read_value_names(value)) for value in _jump_value(following.node))


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    if "jump-binding-gaps" not in context.memo:
        context.memo["jump-binding-gaps"] = frozenset(
            gap_index for gap_index in range(len(container.gaps)) if _is_candidate(container, gap_index, context)
        )

    candidates = cast("frozenset[int]", context.memo["jump-binding-gaps"])
    return index in candidates and is_stanza_anchor_stable(container, index, context, candidates, "jump-upper-runs")


RULE = Rule(
    code="ZA3059",
    name="keep-jump-with-binding",
    level="opt-in",
    message="Remove the blank line between this binding and the jump that uses it.",
    description="Remove the blank line between a lone single-line binding and the return or raise that uses it.",
    claim=_claim,
    role="keep",
    reads_others=True,
    transfers_separation=True,
)
