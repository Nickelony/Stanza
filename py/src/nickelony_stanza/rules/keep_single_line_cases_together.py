"""ZA3113: removes the blank line between two case clauses that each span one line."""

from __future__ import annotations

from nickelony_stanza.rules.base import Rule
from nickelony_stanza.rules.claims import claim_case_gap

RULE = Rule(
    code="ZA3113",
    name="keep-single-line-cases-together",
    level="minimal",
    message="Remove the blank line between these case clauses.",
    description="Remove the blank line between two case clauses of a match statement when each spans one line.",
    claim=claim_case_gap(multi_line=False),
    role="keep",
)
