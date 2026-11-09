"""ZA3203: removes the blank lines between a block header and the first line of its body."""

from __future__ import annotations

from nickelony_stanza.analysis.model import Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.rules.base import Rule


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    return container.kind == "header"


RULE = Rule(
    code="ZA3203",
    name="remove-block-padding",
    level="minimal",
    message="Remove the blank line below this block header.",
    description="Remove the blank lines between a block header and the first line of its body.",
    claim=_claim,
    role="keep",
)
