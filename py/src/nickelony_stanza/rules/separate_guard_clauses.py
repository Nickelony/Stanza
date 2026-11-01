"""ZA3011: separates the guard clauses that open a block from the statements below them."""

from __future__ import annotations

from nickelony_stanza.analysis.model import BODY_KINDS, Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import find_guard_run


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    if container.kind not in BODY_KINDS:
        return False

    # The gap below the last guard of the run that opens the block.
    start, end = find_guard_run(container, context)

    return index >= start and index + 1 == end


RULE = Rule(
    code="ZA3011",
    name="separate-guard-clauses",
    level="strong",
    message="Add a blank line below the guard clauses.",
    description="Separate the guard clauses that open a block from the statements below them.",
    claim=_claim,
)
