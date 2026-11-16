"""ZA3063: Keep consecutive direct operations on a selected receiver together, except between sections."""

from __future__ import annotations

from nickelony_stanza.analysis.model import STATEMENT_KINDS, Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.rules.base import Rule


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    return (
        container.kind in STATEMENT_KINDS
        and bool(context.environment.options.receiver_roots)
        and index in context.receivers_of(container).kept
    )


RULE = Rule(
    code="ZA3063",
    name="keep-receiver-groups-together",
    level="opt-in",
    message="Remove the blank lines inside this receiver group.",
    description="Keep consecutive direct operations on a selected receiver together, except between sections.",
    claim=_claim,
    role="keep",
    transfers_separation=True,
    removes_group=True,
)
