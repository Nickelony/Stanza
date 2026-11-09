"""ZA3111: separates two case clauses when either spans several lines."""

from __future__ import annotations

from nickelony_stanza.analysis.model import Container
from nickelony_stanza.analysis.session import ClaimContext
from nickelony_stanza.rules.base import Rule


def _claim(container: Container, index: int, context: ClaimContext) -> bool:
    return container.kind == "match" and (
        container.units[index].is_multi_line or container.units[index + 1].is_multi_line
    )


RULE = Rule(
    code="ZA3111",
    name="separate-multi-line-cases",
    level="moderate",
    message="Add a blank line between these case clauses.",
    description="Separate two case clauses of a match statement when either spans several lines.",
    claim=_claim,
    opposite="ZA3112",
)
