"""ZA3064: keeps consecutive initialized bindings that consume the preceding bound value."""

from __future__ import annotations

from nickelony_stanza.analysis.model import Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.rules.base import Rule


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    return index in context.dependent_bindings_of(container).kept


RULE = Rule(
    code="ZA3064",
    name="keep-dependent-bindings-together",
    level="opt-in",
    message="Remove the blank lines inside this dependent binding chain.",
    description="Keep consecutive single-line bindings that consume the preceding bound value together.",
    claim=_claim,
    role="keep",
    transfers_separation=True,
    removes_group=True,
)
