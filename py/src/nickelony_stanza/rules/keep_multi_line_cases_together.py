"""ZA3112: removes the blank line between two case clauses when either spans several lines."""

from __future__ import annotations

from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_case_gap

RULE = Rule(
    code="ZA3112",
    name="keep-multi-line-cases-together",
    level="opt-in",
    message="Remove the blank line between these case clauses.",
    description="Remove the blank line between two case clauses of a match statement when either spans several lines.",
    claim=claim_case_gap(multi_line=True),
    role="keep",
    opposite="ZA3111",
)
