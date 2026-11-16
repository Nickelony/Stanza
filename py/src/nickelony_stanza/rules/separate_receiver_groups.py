"""ZA3019: Separate consecutive direct operations selected by their receiver."""

from __future__ import annotations

from nickelony_stanza.analysis.model import STATEMENT_KINDS, Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.rules.base import Rule


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    return (
        container.kind in STATEMENT_KINDS
        and bool(context.environment.options.receiver_roots)
        and index in context.receivers_of(container).borders
    )


RULE = Rule(
    code="ZA3019",
    name="separate-receiver-groups",
    level="opt-in",
    message="Add a blank line beside this receiver group.",
    description="Separate consecutive direct operations selected by their receiver.",
    claim=_claim,
    role="separate",
)
